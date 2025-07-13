using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;

namespace SpecificWorkJobGiver.MechFriendly;

[HarmonyPatch]
public static class MechFriendly_JobDriver_BottleFeedBaby
{
	static MethodBase TargetMethod()
	{
		return AccessTools.Method(typeof(JobDriver_BottleFeedBaby), "<FeedBabyFoodFromInventory>b__15_2");
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
