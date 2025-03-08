using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Graphics.PackedVector;
using StardewValley;
using StardewValley.Extensions;
using StardewValley.GameData.BigCraftables;
using StardewValley.Internal;
using StardewValley.ItemTypeDefinitions;
using StardewValley.TokenizableStrings;
using System.Reflection.Emit;
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

		public static void Draw(this ParsedItemData data, SpriteBatch batch, Vector2 position, float scale, float alpha)
		{
			batch.Draw(data.GetTexture(), position, data.GetSourceRect(), Color.White * alpha, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
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

		public static void DrawShadowText(this SpriteBatch b, SpriteFont font, string text, Vector2 position, Color color, Color shadow)
		{
			b.DrawString(font, text, position + new Vector2(2f, 2f), shadow);
			b.DrawString(font, text, position + new Vector2(0f, 2f), shadow);
			b.DrawString(font, text, position + new Vector2(2f, 0f), shadow);
			b.DrawString(font, text, position, color);
		}

		public static int GetLocalIndex(this CodeInstruction code)
		{
			var op = code.opcode;

			if (
				op == OpCodes.Stloc || op == OpCodes.Stloc_S ||
				op == OpCodes.Ldloca || op == OpCodes.Ldloca_S || 
				op == OpCodes.Ldloc_S || op == OpCodes.Ldloc
			)
				return ((LocalBuilder)code.operand).LocalIndex;

			if (op == OpCodes.Ldloc_0 || op == OpCodes.Stloc_0)
				return 0;
			if (op == OpCodes.Ldloc_1 || op == OpCodes.Stloc_1)
				return 1;
			if (op == OpCodes.Ldloc_2 || op == OpCodes.Stloc_2)
				return 2;
			if (op == OpCodes.Ldloc_3 || op == OpCodes.Stloc_3)
				return 3;

			throw new ArgumentException();
		}

		public static bool TryApplyUpgradeTo(
			ref Item target, Item upgrade, GameLocation? where, Farmer? who, bool probe, bool objectOnly, out bool isUpgrade, out string? notif
		)
		{
			isUpgrade = false;
			notif = null;

			if (target.IsRecipe)
				return false;

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

				if (created.GetType() == target.GetType() && target.HasTypeId(created.TypeDefinitionId))
				{
					target.ItemId = created.ItemId;
					target.ResetParentSheetIndex();
				}
				else if (!objectOnly || created is SObject)
				{
					var id = created.ItemId;
					var name = created.Name;
					created.CopyFieldsFrom(target);
					created.ItemId = id;
					created.Name = name;
					created.Stack = target.Stack;
					created.resetState();
					target = created;
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
