using HarmonyLib;
using Verse;

namespace SpecificWorkJobGiver
{
	public class Mod : Verse.Mod
	{
		public Mod(ModContentPack content) : base(content)
		{
			Log.Message("Hello world from SpecificWorkJobGiver");
			Harmony harmony = new Harmony(content.PackageId);
			harmony.PatchAll();
		}
	}
}
