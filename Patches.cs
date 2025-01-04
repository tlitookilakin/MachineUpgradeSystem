using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Objects;
using SObject = StardewValley.Object;

namespace MachineUpgradeSystem
{
	internal static class Patches
	{
		private static IMonitor Monitor;
		private static Harmony harmony;

		internal static void Patch(IManifest manifest, IMonitor monitor)
		{
			Monitor = monitor;
			harmony = new(manifest.UniqueID);

			harmony.Patch(
				typeof(SObject).GetMethod(nameof(SObject.performObjectDropInAction)),
				prefix: new(typeof(Patches), nameof(TryDropInUpgrade))
			);
			harmony.Patch(
				typeof(CrabPot).GetMethod(nameof(CrabPot.performObjectDropInAction)),
				prefix: new(typeof(Patches), nameof(TryDropInUpgrade))
			);
		}

		public static bool TryDropInUpgrade(ref bool __result, SObject __instance, Item dropInItem, bool probe)
		{
			// no conversions for this kit
			if (!Assets.Data.TryGetValue(dropInItem.QualifiedItemId, out var entries))
				return true;

			// no conversion for this machine
			if (!entries.TryGetValue(__instance.ItemId, out var convertTo))
				return true;

			// currently processing
			/*
			if (__instance.MinutesUntilReady > 0)
			{
				var machine = __instance.GetMachineData();
				if (machine is null || !machine.AllowLoadWhenFull)
					return true;
			}
			*/

			__result = true;

			if (!probe)
			{
				__instance.ItemId = convertTo;
				__instance.ResetParentSheetIndex();
				Game1.playSound("axchop");
			}

			return false;
		}
	}
}
