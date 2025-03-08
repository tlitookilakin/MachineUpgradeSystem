using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
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

			TooltipHandler.Patch(monitor, harmony);
		}

		public static bool TryDropInUpgrade(ref bool __result, SObject __instance, Item dropInItem, bool probe, out bool __state, Farmer who)
		{
			var farmer = SObject.autoLoadFrom is null ? who : null;
			Item obj = __instance;

			if (ModUtilities.TryApplyUpgradeTo(ref obj, dropInItem, __instance.Location, farmer, probe, true, out __state, out var notif))
			{
				if (!probe)
				{
					SObject target = (SObject)obj;

					// was replaced with new instance
					if (target.GetType() != __instance.GetType())
					{
						var tile = __instance.TileLocation;
						var where = __instance.Location;

						where.Objects.Remove(tile);

						if(target.placementAction(where, (int)tile.X, (int)tile.Y))
						{
							if (!where.Objects.ContainsKey(tile))
								where.Objects[tile] = target;
						}
						else
						{
							// couldn't be placed, cancel.
							where.Objects[tile] = __instance;
							return true;
						}
					}

					Game1.playSound("axchop");
				}

				__result = true;
				return false;
			}
			else if (notif is not null && !probe && farmer != null)
			{
				__state = false;

				if (notif.Length > 0)
					Game1.showRedMessage(notif);
			}

			return true;
		}

		public static void CheckForInvalidUpgrade(bool __result, SObject __instance, bool __state, Farmer who, bool probe)
		{
			// if accepted or not an upgrade, ignore
			if (!__state || __result || probe)
				return;

			// ignore if automated or a remote player.
			if (SObject.autoLoadFrom != null || who != Game1.player)
				return;

			var id = __instance.QualifiedItemId;
			if (!Assets.UpgradeCache.TryGetValue(id, out var upgrade))
				return;

			var display = Assets.GetIcon(upgrade).DisplayName;

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
				new(OpCodes.Ldloca, 2),
				new(OpCodes.Ldarg, 4),
				new(OpCodes.Call, typeof(Patches).GetMethod(nameof(ApplyUpgradeStack))),
				new(OpCodes.Brfalse, jump),

				// this.actualInventory[slotnumber] = slot;
				new(OpCodes.Ldarg_0),
				new(OpCodes.Ldfld, typeof(InventoryMenu).GetField(nameof(InventoryMenu.actualInventory))),
				new(OpCodes.Ldloc_1),
				new(OpCodes.Ldloc_2),
				new(OpCodes.Callvirt, typeof(IList<Item>).GetMethod("set_Item")),

				// return held;
				new(OpCodes.Ldarg_3),
				new(OpCodes.Stloc_S, ret),
				new(OpCodes.Leave, leaveTarget)
			);

			//var d = il.InstructionEnumeration().ToList();

			return il.InstructionEnumeration();
		}

		public static bool ApplyUpgradeStack(ref Item? held, ref Item? slot, bool playSound)
		{
			if (held is null || slot is null)
				return false;

			var old_slot = slot.getOne();

			if (ModUtilities.TryApplyUpgradeTo(ref slot, held, null, Game1.player, false, false, out bool isUpgrade, out _))
			{
				if (held.Stack < slot.Stack)
				{
					int count = held.Stack;
					held = old_slot;
					held.Stack = slot.Stack - count;
					slot.Stack = count;
				}
				else
				{
					held = held.ConsumeStack(slot.Stack);
				}

				if (playSound)
					Game1.playSound("axchop");

				return true;
			}
			else if (isUpgrade)
			{
				if (playSound)
					Game1.playSound("cancel");
			}

			return false;
		}
	}
}
