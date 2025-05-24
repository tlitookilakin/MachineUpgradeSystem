using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Tools;
using System.Reflection.Emit;
using System.Text;

namespace MachineUpgradeSystem.Framework
{
	internal static class TooltipHandler
	{
		private static IMonitor Monitor;
		private static IModHelper Helper;

		internal static void Patch(IMonitor monitor, IModHelper helper, Harmony harmony)
		{
			Monitor = monitor;
			Helper = helper;

			harmony.Patch(
				typeof(Item).GetMethod(nameof(Item.drawTooltip)),
				postfix: new(typeof(TooltipHandler), nameof(AddToTooltip))
			);

			harmony.Patch(
				typeof(IClickableMenu).GetMethod(nameof(IClickableMenu.drawHoverText), [
					typeof(SpriteBatch), typeof(StringBuilder), typeof(SpriteFont), typeof(int), typeof(int), typeof(int), typeof(string), typeof(int),
					typeof(string[]), typeof(Item), typeof(int), typeof(string), typeof(int), typeof(int), typeof(int), typeof(float),
					typeof(CraftingRecipe), typeof(IList<Item>), typeof(Texture2D), typeof(Rectangle?), typeof(Color?), typeof(Color?),
					typeof(float), typeof(int), typeof(int)
				]),
				transpiler: new(typeof(TooltipHandler), nameof(InjectSizeChange))
			);
		}

		private static void AddToTooltip(Item __instance, SpriteBatch spriteBatch, ref int x, ref int y, SpriteFont font, float alpha)
		{
			if (!Assets.TryGetIcon(__instance.QualifiedItemId, out var icon))
				return;

			icon.Draw(spriteBatch, new(x + 16, y + 16), 2f, alpha);
			var color = Utility.Get2PhaseColor(Game1.textColor, Color.Lime);
			var text = Helper.Translation.Get("ui.upgradable.text", new { upgrade = icon.DisplayName });
			spriteBatch.DrawShadowText(font, text, new(x + 48, y + 18), color * alpha, Game1.textShadowColor * alpha);
			y += 32 + 4;
		}

		public static void ModifyTooltipSize(ref int height, Item hovered)
		{
			if (Assets.UpgradeCache.ContainsKey(hovered.QualifiedItemId))
				height += 32 + 4;
		}

		private static IEnumerable<CodeInstruction> InjectSizeChange(IEnumerable<CodeInstruction> source, ILGenerator gen)
		{
			var il = new CodeMatcher(source, gen);

			il.MatchStartForward(
				new(OpCodes.Ldarg_S),
				new(OpCodes.Isinst, typeof(MeleeWeapon))
			);
			var hovered = new CodeInstruction(OpCodes.Ldarg_S, il.Operand);
			int pos = il.Pos;
			il.MatchStartBackwards(
				new CodeMatch(i => i.IsStloc())
			);
			int index = il.Instruction.GetLocalIndex();
			il.Advance(pos - il.Pos);
			il.InsertAndAdvance(
				new(OpCodes.Ldloca, index),
				hovered,
				new(OpCodes.Call, typeof(TooltipHandler).GetMethod(nameof(ModifyTooltipSize)))
			);

			return il.InstructionEnumeration();
		}
	}
}
