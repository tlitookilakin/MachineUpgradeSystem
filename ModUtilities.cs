using StardewValley;
using StardewValley.Extensions;
using StardewValley.GameData.BigCraftables;
using StardewValley.Internal;
using StardewValley.TokenizableStrings;
using SObject = StardewValley.Object;

namespace MachineUpgradeSystem
{
	internal static class ModUtilities
	{
		public static bool TryGetIdByName(string name, string modid, Dictionary<string, BigCraftableData> data, out string id)
		{
			id = $"{modid}_{name}";
			if (data.ContainsKey(id))
				return true;

			name = name.CamelToHuman();

			id = data.FirstOrDefault(p => p.Value.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).Key ?? "";
			return id.Length > 0;
		}

		public static string CamelToHuman(this string source)
		{
			Span<char> chars = stackalloc char[source.Length * 2];
			var src = source.AsSpan();

			int last = 0;
			int cursor = 0;

			for (int i = 0; i < src.Length; i++)
			{
				if (char.IsUpper(src[i]))
				{
					if (last < i)
					{
						src[last..i].CopyTo(chars[cursor..]);
						cursor += i - last;
					}

					if (i > 0)
						chars[cursor++] = ' ';
					chars[cursor++] = char.ToLowerInvariant(src[i]);
					last = i + 1;
				}
			}

			if (last < src.Length)
			{
				src[last..].CopyTo(chars[cursor..]);
				cursor += src.Length - last;
			}

			return new string(chars[..cursor]);
		}

		public static bool TryApplyUpgradeTo(ref SObject target, Item upgrade, GameLocation? where, Farmer? who, bool probe, out bool isUpgrade, out string? notif)
		{
			isUpgrade = false;
			notif = null;

			if (!Assets.Data.TryGetValue(upgrade.QualifiedItemId, out var upgrades))
				return false;

			isUpgrade = true;

			if (!upgrades.TryGetValue(target.QualifiedItemId, out var entry))
				return false;

			if (entry.Condition is string cond && !GameStateQuery.CheckConditions(cond, where, who, upgrade, target))
			{
				notif = entry.FailureMessage is string msg ? 
					TokenParser.ParseText(entry.FailureMessage, player: who) :
					"";

				return false;
			}

			if (!probe)
			{
				var created = ItemQueryResolver.TryResolveRandomItem(entry.ItemId, new(where, who, Game1.random, "Machine Upgrade System"));

				if (created.GetType() == target.GetType() && (target.HasTypeBigCraftable() == ((SObject)created).HasTypeBigCraftable()))
				{
					target.ItemId = created.ItemId;
					target.ResetParentSheetIndex();
				}
				else if (created is SObject obj)
				{
					var id = created.ItemId;
					created.CopyFieldsFrom(target);
					created.ItemId = id;
					created.Stack = target.Stack;
					target = obj;
				}
				else
				{
					return false;
				}
			}

			return true;
		}
	}
}
