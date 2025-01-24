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

		public static bool TryDropInUpgrade(ref bool __result, SObject __instance, Item dropInItem, bool probe, out bool __state, Farmer who)
		{
			var farmer = SObject.autoLoadFrom is null ? who : null;
			var target = __instance;

			if (ModUtilities.TryApplyUpgradeTo(ref target, dropInItem, __instance.Location, farmer, probe, out __state, out var notif))
			{
				if (!probe)
				{
					// was replaced with new instance
					if (target.GetType() != __instance.GetType())
					{
						var tile = __instance.TileLocation;
						var where = __instance.Location;

						where.Objects.Remove(tile);

						target.placementAction(where, (int)tile.X, (int)tile.Y);
					}

					Game1.playSound("axchop");
				}

				return true;
			}
			else if (notif is not null && !probe && farmer != null)
			{
				__state = false;
				Game1.showRedMessage(notif);
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

			// TODO replace with bubble
			Game1.addHUDMessage(new($"Required upgrade: {display}") { noIcon = true });
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
			if (held is null || slot is not SObject sobj)
				return false;

			if (ModUtilities.TryApplyUpgradeTo(ref sobj, held, null, Game1.player, false, out _, out _))
			{
				if (held.Stack < slot.Stack)
				{
					int count = held.Stack;
					held = slot.getOne();
					held.Stack = slot.Stack - count;
					sobj.Stack = count;
				}
				else
				{
					held = held.ConsumeStack(slot.Stack);
				}

				// TODO update transpiler
				slot = sobj;

				if (playSound)
					Game1.playSound("axchop");

				return true;
			}
			else
			{
				if (playSound)
					Game1.playSound("cancel");

				return false;
			}
		}
	}
}
