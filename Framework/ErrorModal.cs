using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Mods;

namespace MachineUpgradeSystem.Framework
{
    public static class ErrorModal
    {
        private static string displayText = "";
        private static int startTick = 0;
        private static int holdTime = 0;
        private static float fadeTime = 30f;
        private static Vector2 textSize = Vector2.Zero;

        public static bool Active { get; private set; } = false;

        internal static void Init(IModHelper helper)
        {
            helper.Events.Display.RenderedStep += RenderStep;
        }

        private static void RenderStep(object? sender, RenderedStepEventArgs e)
        {
            if (e.Step is RenderSteps.Overlays && Active)
                Draw(e.SpriteBatch);
        }

        public static void PushMessage(string message)
        {
            displayText = Game1.parseText(message, Game1.smallFont, 400);
            startTick = Active ? Game1.ticks : Game1.ticks + (int)fadeTime;
            holdTime = Math.Max(30, 3 * message.Length);
            Active = true;
            textSize = Game1.smallFont.MeasureString(displayText);
        }

        public static void Draw(SpriteBatch batch)
        {
            float a =
                Game1.ticks < startTick ? 1f - (startTick - Game1.ticks) / fadeTime :
                Game1.ticks > startTick + holdTime ? 1f - (Game1.ticks - (startTick + holdTime)) / fadeTime :
                1f;

            if (a < 0f)
            {
                Active = false;
                return;
            }

            var size = textSize;
            var port = Game1.uiViewport.Size;
            Vector2 pos = new(port.Width / 2 - size.X / 2, port.Height / 2 - size.Y / 2);

            batch.Draw(Game1.staminaRect, new Rectangle((int)pos.X - 8, (int)pos.Y - 4, (int)size.X + 16, (int)size.Y + 8), Color.Black * a * .5f);
            batch.DrawFrame(
                Game1.menuTexture, new(0, 256, 60, 60), new((int)pos.X - 20, (int)pos.Y - 16, (int)size.X + 40, (int)size.Y + 32),
                new(12, 12, 36, 36), Color.White * a, false, 1
            );
            if (a == 1f)
                batch.DrawShadowText(Game1.smallFont, displayText, pos, Color.White * a, Color.Black * a);
            else
                batch.DrawString(Game1.smallFont, displayText, pos, Color.White * a);
        }
    }
}
