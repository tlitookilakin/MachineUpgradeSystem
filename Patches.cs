using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Extensions;
using StardewValley.Inventories;
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

		internal static void Patch(IManifest manifest, IMonitor monitor, IModHelper helper)
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

			if (!OperatingSystem.IsAndroid())
			{
				harmony.Patch(
					typeof(InventoryMenu).GetMethod(nameof(InventoryMenu.rightClick)),
					transpiler: new(typeof(Patches), nameof(InjectInventoryUpgrade))
				);
			}
			else
			{
				monitor.Log("Some features are not supported on android and will be disabled!", LogLevel.Info);
			}

			TooltipHandler.Patch(monitor, helper, harmony);
		}

		// TODO handle transformation to other types
		public static bool TryDropInUpgrade(ref bool __result, SObject __instance, Item dropInItem, bool probe, out bool __state, Farmer who)
		{
			var farmer = SObject.autoLoadFrom is null ? who : null;
			Item obj = __instance;

			if (ModUtilities.TryApplyUpgradeTo(ref obj, dropInItem, __instance.Location, farmer, probe, true, out __state, out var notif))
			{
				if (!probe)
				{
					// not an object, drop on floor
					if (obj is not SObject target)
					{
						Game1.createItemDebris(obj, __instance.TileLocation * 64f + new Vector2(32f), -1, __instance.Location);
						__instance.Location.Objects.Remove(__instance.TileLocation);
						return false;
					}

					// was replaced with new instance
					if (target.GetType() != __instance.GetType() || !target.HasTypeId(__instance.GetItemTypeId()))
					{
						var tile = __instance.TileLocation;
						var where = __instance.Location;

						where.Objects.Remove(tile);

						if(target.placementAction(where, (int)tile.X * 64, (int)tile.Y * 64))
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

					if (SObject.autoLoadFrom is IInventory autoload)
					{
						autoload.ReduceId(dropInItem.QualifiedItemId, 1);
						autoload.RemoveEmptySlots();
					}
					else
					{
						Game1.playSound("axchop");
					}
				}

				__result = true;
				return false;
			}
			else if (notif is not null && !probe && farmer != null)
			{
				__state = false;
				Game1.playSound("cancel");
				ErrorModal.PushMessage(notif);
				ModUtilities.DisplayErrorSprite(__instance.Location, __instance.TileLocation);
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
			if (!Assets.TryGetIcon(id, out var icon))
				return;

			Game1.playSound("cancel");
			ModUtilities.DisplayUpgradeSprite(__instance.Location, __instance.TileLocation, icon);
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

			if (ModUtilities.TryApplyUpgradeTo(ref slot, held, null, Game1.player, false, false, out bool isUpgrade, out var notif))
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

				if (notif is not null)
					ErrorModal.PushMessage(notif);
			}

			return false;
		}
	}
}
