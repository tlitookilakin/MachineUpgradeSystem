using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Extensions;
using StardewValley.Menus;
using StardewValley.Objects;
using System.Reflection.Emit;
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
				prefix: new(typeof(Patches), nameof(TryDropInUpgrade)),
				postfix: new(typeof(Patches), nameof(CheckForInvalidUpgrade))
			);
			harmony.Patch(
				typeof(CrabPot).GetMethod(nameof(CrabPot.performObjectDropInAction)),
				prefix: new(typeof(Patches), nameof(TryDropInUpgrade)),
				postfix: new(typeof(Patches), nameof(CheckForInvalidUpgrade))
			);
			harmony.Patch(
				typeof(InventoryMenu).GetMethod(nameof(InventoryMenu.rightClick)),
				transpiler: new(typeof(Patches), nameof(InjectInventoryUpgrade))
			);
		}

		public static bool TryDropInUpgrade(ref bool __result, SObject __instance, Item dropInItem, bool probe, out bool __state)
		{
			__state = false;

			// no conversions for this kit
			if (!Assets.Data.TryGetValue(dropInItem.QualifiedItemId, out var entries))
				return true;

			// no conversion for this machine
			if (!entries.TryGetValue(__instance.ItemId, out var convertTo))
			{
				// mark as non-matching upgrade item
				__state = true;
				return true;
			}

			__result = true;

			if (!probe)
			{
				__instance.ItemId = convertTo;
				__instance.ResetParentSheetIndex();
				Game1.playSound("axchop");
			}

			return false;
		}

		public static void CheckForInvalidUpgrade(bool __result, SObject __instance, bool __state, Farmer who, bool probe)
		{
			// if accepted or not an upgrade, ignore
			if (!__state || __result || probe)
				return;

			// ignore if automated or a remote player.
			if (SObject.autoLoadFrom != null || who != Game1.player)
				return;

			var id = __instance.ItemId;
			var requiredUpgrade = Assets.Data.FirstOrDefault(p => p.Value.ContainsKey(id)).Key;

			// no matches
			if (requiredUpgrade is null)
				return;

			var display = ItemRegistry.GetData(requiredUpgrade).DisplayName;

			// TODO translate me
			Game1.addHUDMessage(new($"Required upgrade: {display}"));
		}

		public static IEnumerable<CodeInstruction> InjectInventoryUpgrade(IEnumerable<CodeInstruction> source, ILGenerator gen)
		{
			var il = new CodeMatcher(source, gen);
			var slot = gen.DeclareLocal(typeof(Item));
			var held = gen.DeclareLocal(typeof(Item));

			LocalBuilder ret;

			// find return point
			il.End()
			.MatchStartBackwards(new CodeMatch(OpCodes.Ret))
			.MatchStartBackwards(new CodeMatch(i => i.operand is LocalBuilder));
			ret = (LocalBuilder)il.Operand;
			il.Start();

			il.MatchStartForward(
				new CodeMatch(OpCodes.Callvirt, typeof(Tool).GetMethod(nameof(Tool.attach)))
			).MatchStartForward(
				new CodeMatch(OpCodes.Leave)
			);

			var leaveTarget = il.Instruction.operand;

			il.MatchEndBackwards(
				new(OpCodes.Ldloc_2),
				new(OpCodes.Brfalse)
			);

			il.Advance(1)
			.CreateLabel(out var jump)
			.InsertAndAdvance(

				// if (ApplyUpgradeStack(ref held, slot, playSound))
				new(OpCodes.Ldarga, 3),
				new(OpCodes.Ldloc_2),
				new(OpCodes.Ldarg_S, 4),
				new(OpCodes.Call, typeof(Patches).GetMethod(nameof(ApplyUpgradeStack))),
				new(OpCodes.Brfalse, jump),

				// return held;
				new(OpCodes.Ldarg_3),
				new(OpCodes.Stloc_S, ret),
				new(OpCodes.Leave, leaveTarget)
			);

			//var d = il.InstructionEnumeration().ToList();

			return il.InstructionEnumeration();
		}

		public static bool ApplyUpgradeStack(ref Item? held, Item? slot, bool playSound)
		{
			if (held is null || slot is not SObject sobj || !sobj.HasTypeBigCraftable())
				return false;

			if (!Assets.Data.TryGetValue(held.QualifiedItemId, out var entries))
				return false;

			if (!entries.TryGetValue(slot.ItemId, out var convertTo))
				return false;
			
			if (held.Stack < slot.Stack)
			{
				int count = held.Stack;
				held = slot.getOne();
				held.Stack = slot.Stack - count;
				slot.Stack = count;
			}
			else
			{
				held = held.ConsumeStack(slot.Stack);
			}

			slot.ItemId = convertTo;
			slot.ResetParentSheetIndex();

			if (playSound)
				Game1.playSound("axchop");

			return true;
		}
	}
}
