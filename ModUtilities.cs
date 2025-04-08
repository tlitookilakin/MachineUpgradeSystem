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

		public static void DisplayErrorSprite(GameLocation where, Vector2 pos)
		{
			pos *= 64f;
			float depth = (pos.Y + 64f) * .0001f;
			pos.Y -= 128f;

			if (where != null)
			{
				TemporaryAnimatedSprite bubble = new("TileSheets\\emotes", new(0, 0, 16, 16), pos, false, 0f, Color.White)
				{
					animationLength = 4,
					interval = 20,
					layerDepth = depth,
					scale = 4f,
					endFunction = i =>
					{
						where.TemporarySprites.Add(new("TileSheets\\emotes", new(0, 9 * 16, 16, 16), pos, false, 0f, Color.White)
						{
							animationLength = 4,
							totalNumberOfLoops = 2,
							interval = 250,
							layerDepth = depth,
							scale = 4f
						});
					}
				};
				where.TemporarySprites.Add(bubble);
			}
		}

		public static void DisplayUpgradeSprite(GameLocation where, Vector2 pos, ParsedItemData icon)
		{
			const string bubble_texture = "Mods/" + Assets.MOD_ID + "/Bubble";

			pos *= 64f;
			float depth = (pos.Y + 64f) * .0001f;
			pos.Y -= 128f;
			if (where != null)
			{
				TemporaryAnimatedSprite bubble = new(bubble_texture, new(0, 0, 16, 16), pos, false, 0f, Color.White)
				{
					animationLength = 4,
					interval = 20,
					layerDepth = depth,
					scale = 4f,
					endFunction = i =>
					{
						where.TemporarySprites.Add(new(bubble_texture, new(0, 16, 16, 16), pos, false, 0f, Color.White)
						{
							animationLength = 4,
							totalNumberOfLoops = 2,
							interval = 250,
							layerDepth = depth,
							scale = 4f
						});
						where.TemporarySprites.Add(new(icon.GetTextureName(), icon.GetSourceRect(), pos + new Vector2(16, 8), false, 0f, Color.White)
						{
							animationLength = 1,
							interval = 2000,
							layerDepth = MathF.BitIncrement(depth),
							scale = 2f
						});
					}
				};
				where.TemporarySprites.Add(bubble);
			}
		}

		public static void DrawFrame(this SpriteBatch b, Texture2D texture, Rectangle source, Rectangle dest, Rectangle interior, Color c, bool drawInside, int scale)
		{
			int s_right = source.Width - interior.Right;
			int s_bottom = source.Height - interior.Bottom;

			// top row

			int s_y = source.Y;
			int d_y = dest.Y;
			int s_h = interior.Y;
			int d_h = interior.Y * scale;

			b.Draw(texture,
				new Rectangle(dest.X, d_y, interior.X * scale, d_h),
				new Rectangle(source.X, s_y, interior.X, s_h),
			c);
			b.Draw(texture,
				new Rectangle(dest.X + interior.X * scale, d_y, dest.Width - s_right * scale - interior.X * scale, d_h),
				new Rectangle(source.X + interior.X, s_y, interior.Width, s_h),
			c);
			b.Draw(texture,
				new Rectangle(dest.Right - s_right * scale, d_y, s_right * scale, d_h),
				new Rectangle(source.X + interior.Right, s_y, s_right, s_h),
			c);

			// middle row

			s_y = interior.Y + source.Y;
			d_y = dest.Y + interior.Y * scale;
			s_h = interior.Height;
			d_h = dest.Height - interior.Y * scale - s_bottom * scale;
			
			b.Draw(texture,
				new Rectangle(dest.X, d_y, interior.X * scale, d_h),
				new Rectangle(source.X, s_y, interior.X, s_h),
			c);
			if (drawInside)
			{
				b.Draw(texture,
					new Rectangle(dest.X + interior.X * scale, d_y, dest.Width - s_right * scale - interior.X * scale, d_h),
					new Rectangle(source.X + interior.X, s_y, interior.Width, s_h),
				c);
			}
			b.Draw(texture,
				new Rectangle(dest.Right - s_right * scale, d_y, s_right * scale, d_h),
				new Rectangle(source.X + interior.Right, s_y, s_right, s_h),
			c);

			// bottom row

			s_y = interior.Bottom + source.Y;
			d_y = dest.Bottom - s_bottom * scale;
			s_h = s_bottom;
			d_h = s_bottom * scale;

			b.Draw(texture,
				new Rectangle(dest.X, d_y, interior.X * scale, d_h),
				new Rectangle(source.X, s_y, interior.X, s_h),
			c);
			b.Draw(texture,
				new Rectangle(dest.X + interior.X * scale, d_y, dest.Width - s_right * scale - interior.X * scale, d_h),
				new Rectangle(source.X + interior.X, s_y, interior.Width, s_h),
			c);
			b.Draw(texture,
				new Rectangle(dest.Right - s_right * scale, d_y, s_right * scale, d_h),
				new Rectangle(source.X + interior.Right, s_y, s_right, s_h),
			c);
		}

		public static bool TryApplyUpgradeTo(
			ref Item target, Item upgrade, GameLocation? where, Farmer? who, bool probe, bool objectOnly, out bool isUpgrade, out string? notif
		)
		{
			isUpgrade = false;
			notif = null;

			if (target is null || upgrade is null)
				return false;

			if (target.IsRecipe || upgrade.IsRecipe)
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
					ModEntry.I18N.Get("ui.failedUpgrade.text");

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
