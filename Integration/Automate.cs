using HarmonyLib;
using MachineUpgradeSystem.Framework;
using StardewValley;
using StardewValley.Extensions;
using StardewValley.Inventories;
using System.Linq.Expressions;
using System.Reflection;
using SObject = StardewValley.Object;

namespace MachineUpgradeSystem.Integration
{
	internal class Automate
	{
		const BindingFlags AnyBinding = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

		private static Func<object, IEnumerable<IInventory>> GetInputInventories;
		private static Func<object, IEnumerable<SObject>> GetActualMachines;

		internal static void Patch(Harmony harmony)
		{
			if (!ModUtilities.TryFindAssembly("automate", out var asm))
				return;

			var group = asm.GetType("Pathoschild.Stardew.Automate.Framework.MachineGroup")!;
			GetActualMachines = BuildMachineGetter(group);
			GetInputInventories = BuildInventoryGetter(group);

			harmony.Patch(
				group.GetMethod("Automate"),
				prefix: new(typeof(Automate), nameof(UpgradePrefix))
			);
		}

		internal static void UpgradePrefix(object __instance)
		{
			if (((dynamic)__instance).StorageManager.HasLockedContainers())
				return;

			var machines = GetActualMachines(__instance).ToList();
			foreach (var inv in GetInputInventories(__instance))
			{
				for (int i = 0; i < inv.Count; i++)
				{
					var item = inv[i];

					if (item is null || item.IsRecipe)
						continue;

					if (!Assets.Data.TryGetValue(item.QualifiedItemId, out var upgrades))
						continue;

					foreach (var machine in machines)
					{
						Item m = machine;
						if (ModUtilities.TryApplyUpgradeTo(ref m, item, machine.Location, null, false, true, out _, out var notif))
						{
							var o = (SObject)m;

							// was replaced with new instance
							if (m.GetType() != __instance.GetType() || !m.HasTypeId(machine.GetItemTypeId()))
							{
								var tile = machine.TileLocation;
								var where = machine.Location;
								where.Objects.Remove(tile);

								if (o.placementAction(where, (int)tile.X * 64, (int)tile.Y * 64))
								{
									if (!where.Objects.ContainsKey(tile))
										where.Objects[tile] = o;

									if (--item.Stack <= 0)
									{
										inv[i] = null;
										break;
									}
								}
								else
								{
									// couldn't be placed, cancel.
									where.Objects[tile] = machine;
								}
							}
						}
					}
				}

				inv.RemoveEmptySlots();
			}
		}

		private static Func<object, IEnumerable<IInventory>> BuildInventoryGetter(Type machineGroup)
		{
			var select = typeof(Enumerable).GetMethod(nameof(Enumerable.Select), [typeof(IEnumerable<>), typeof(Func<,>)])!;

			var inp = Expression.Parameter(typeof(object), "input");
			var body = Expression.Call(
				Expression.ConvertChecked(inp, machineGroup)
					.GetValue(machineGroup, "StorageManager", out var tStorage)
					.GetValue(tStorage, "InputContainers", out var tInputs),
				select.MakeGenericMethod([tInputs, tInputs.GenericTypeArguments[0]]),
				Expression.Constant(
					tInputs.GenericTypeArguments[0].GetProperty("Inventory", AnyBinding)!.GetMethod!.CreateDelegate(
						typeof(Func<,>).MakeGenericType([tInputs.GenericTypeArguments[0], typeof(IInventory)])
					)
				)
			);
			return Expression.Lambda<Func<object, IEnumerable<IInventory>>>(body, inp).Compile();
		}

		private static Func<object, IEnumerable<SObject>> BuildMachineGetter(Type machineGroup)
		{
			var select = typeof(Enumerable).GetMethod(nameof(Enumerable.Select), [typeof(IEnumerable<>), typeof(Func<,>)])!;
			var where = typeof(Enumerable).GetMethod(nameof(Enumerable.Where), [typeof(IEnumerable<>), typeof(Func<,>)])!;

			var inp = Expression.Parameter(typeof(object), "input");
			var body = Expression.Call(
				Expression.Call(
					Expression.ConvertChecked(inp, machineGroup)
						.GetValue(machineGroup, "Machines", out var tMachines),
					select.MakeGenericMethod(tMachines, tMachines.GenericTypeArguments[0]),
					Expression.Constant(SelectMachines(tMachines.GenericTypeArguments[0]).Compile())
				),
				where.MakeGenericMethod(typeof(SObject)),
				Expression.Constant((Func<SObject, bool>)FilterNulls)
			);
			return Expression.Lambda<Func<object, IEnumerable<SObject>>>(body, inp).Compile();
		}

		private static bool FilterNulls(SObject? obj)
			=> obj is not null;

		private static LambdaExpression SelectMachines(Type input)
		{
			var target = input.Assembly
				.GetType("Pathoschild.Stardew.Automate.Framework.BaseMachine`1")!
				.MakeGenericType(typeof(SObject))!;

			var inp = Expression.Parameter(input, "input");

			return Expression.Lambda(
				Expression.Condition(
					Expression.Convert(inp, target),
					inp.GetValue(target, "Machine", out _),
					Expression.Constant(null)
				)
			);
		}
	}
}
