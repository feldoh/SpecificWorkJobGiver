using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;

namespace SpecificWorkJobGiver.MechFriendly;

/**
 * This class is heavily inspired by the Porio.TunnulerFix mod and as such if this mod is loaded will not make any additional changes.
 */
[HarmonyPatch]
public static class MechFriendly_JobDriver_OperateDeepDrill
{
	// The targeted method is a delegate so we use AccessTools.Inner
	static MethodBase TargetMethod()
	{
		return AccessTools.Method(AccessTools.Inner(typeof(JobDriver_OperateDeepDrill), "<>c__DisplayClass1_0"),
			"<MakeNewToils>b__1");
	}


	static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator il)
	{
		bool shouldModify = ModLister.GetActiveModWithIdentifier("Porio.TunnulerFix") == null;

		var learnMethod = AccessTools.Method(typeof(Pawn_SkillTracker), "Learn");
		Label postEditLabel = il.DefineLabel();
		Label postLearnLabel = il.DefineLabel();
		bool edited = false;
		bool postLearn = false;
		byte edits = 0;

		foreach (var instruction in instructions)
		{
			if (edited)
			{
				instruction.labels.Add(postEditLabel);
				edited = false;
				edits++;
			}
			else if (postLearn)
			{
				instruction.labels.Add(postLearnLabel);
				postLearn = false;
				edits++;
			}

			if (!shouldModify)
			{
				yield return instruction;
			}
			else if (instruction.opcode == OpCodes.Ldfld &&
			         instruction.operand.ToString().Equals("RimWorld.Pawn_SkillTracker skills"))
			{
				yield return instruction;

				// This pattern is what happens when you compare the relevant line using null-safe vs non-null-safe options.
				yield return new CodeInstruction(OpCodes.Dup);
				yield return new CodeInstruction(OpCodes.Brtrue_S, postEditLabel);
				yield return new CodeInstruction(OpCodes.Pop);
				yield return new CodeInstruction(OpCodes.Br_S, postLearnLabel);
				edited = true; // Add the postEdit label to the next instruction
			}
			else if (instruction.opcode == OpCodes.Callvirt && instruction.Calls(learnMethod))
			{
				yield return instruction;
				postLearn = true; // Add the postLearn label to the next instruction
			}
			else
			{
				yield return instruction;
			}
		}

		if (shouldModify && edits != 2)
		{
			Log.Warning("Making Deep-drills Mech friendly failed");
		}
	}
}

[HarmonyPatch]
public static class MechFriendly_JobDriver_BottleFeedBaby
{
	static MethodBase TargetMethod()
	{
		return AccessTools.Method(typeof(JobDriver_BottleFeedBaby), "<FeedBabyFoodFromInventory>b__11_2");
	}

	public static void TryGainMemorySafe(Pawn pawn, ThoughtDef def, Pawn otherPawn, Precept precept)
	{
		if (otherPawn == null) return;
		pawn.needs?.mood?.thoughts?.memories?.TryGainMemory(def, otherPawn, precept);
	}

	static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
	{
		bool maybeReplace = false;
		List<CodeInstruction> bufferCodes = new List<CodeInstruction>();
		foreach (var t in instructions)
		{
			bool readingNeeds = t.LoadsField(AccessTools.Field(typeof(Pawn), "needs"));
			if (!maybeReplace && !readingNeeds)
			{
				yield return t;
				continue;
			}

			if (readingNeeds && maybeReplace)
			{
				foreach (CodeInstruction bufferCode in bufferCodes)
				{
					yield return bufferCode;
				}

				maybeReplace = false;
			}

			if (!maybeReplace)
			{
				bufferCodes.Clear();
				maybeReplace = true;
				bufferCodes.Add(t);
				continue;
			}

			if (t.Calls(AccessTools.Method(typeof(MemoryThoughtHandler), "TryGainMemory", new[]
			    {
				    typeof(ThoughtDef), typeof(Pawn), typeof(Precept)
			    })))
			{
				bool loadedThought = false;
				foreach (CodeInstruction bufferCode in bufferCodes)
				{
					if (!loadedThought)
					{
						if (bufferCode.operand is FieldInfo fi && fi.FieldType == typeof(ThoughtDef))
						{
							loadedThought = true;
						}
						else
						{
							continue;
						}
					}

					yield return bufferCode;
				}

				yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MechFriendly_JobDriver_BottleFeedBaby), "TryGainMemorySafe"));
				Log.Message("Making Baby feeding Mech friendly succeeded");
				bufferCodes.Clear();
				maybeReplace = false;
				continue;
			}

			bufferCodes.Add(t);
		}

		foreach (CodeInstruction bufferCode in bufferCodes)
		{
			yield return bufferCode;
		}
	}
}
