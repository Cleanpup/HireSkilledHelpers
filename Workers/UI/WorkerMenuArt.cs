using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Objects;

namespace HireSkilledHelpers.Workers;

/// <summary>Vanilla menu and item sprites, fitted without changing their aspect ratio.</summary>
internal static class WorkerMenuArt
{
    internal enum Icon
    {
        Sprout, Water, Harvest, Tend, Home, Coin, Ledger, Chest,
        Roster, Skills, Mining, Fishing, Foraging, Combat, Hardwood, Explore, Perk,
    }

    internal static void Draw(SpriteBatch batch, Icon icon, int x, int y, int pixelSize = 3)
        => Draw(batch, icon, new Rectangle(x, y, 12 * pixelSize, 12 * pixelSize));

    internal static void Draw(SpriteBatch batch, Icon icon, Rectangle bounds, float opacity = 1f)
    {
        // SkillsPage and GameMenu are the source of the cursor-sheet rectangles.
        Rectangle source = icon switch
        {
            Icon.Sprout => new Rectangle(10, 428, 10, 10),
            Icon.Mining => new Rectangle(30, 428, 10, 10),
            Icon.Fishing => new Rectangle(20, 428, 10, 10),
            Icon.Foraging => new Rectangle(60, 428, 10, 10),
            Icon.Combat => new Rectangle(120, 428, 10, 10),
            Icon.Coin => new Rectangle(338, 400, 8, 8),
            Icon.Home => new Rectangle(653, 880, 10, 10),
            Icon.Roster => new Rectangle(32, 368, 16, 16),
            Icon.Skills => new Rectangle(16, 368, 16, 16),
            Icon.Explore => new Rectangle(48, 368, 16, 16),
            Icon.Perk => new Rectangle(50, 428, 10, 10),
            Icon.Ledger => new Rectangle(80, 368, 16, 16),
            _ => Rectangle.Empty,
        };
        Texture2D texture = Game1.mouseCursors;
        if (source == Rectangle.Empty)
        {
            string itemId = icon switch
            {
                Icon.Water => "(T)WateringCan",
                Icon.Harvest => "(O)24", // Parsnip.
                Icon.Tend => "(T)Hoe",
                Icon.Hardwood => "(O)709",
                _ => "(BC)130", // Chest.
            };
            var data = ItemRegistry.GetDataOrErrorItem(itemId);
            texture = data.GetTexture();
            source = data.GetSourceRect();
        }
        DrawSprite(batch, texture, source, bounds, opacity);
    }

    /// <summary>Draw a closed chest using the same dye and hardware layers as vanilla Chest.draw.</summary>
    internal static void DrawChest(SpriteBatch batch, Chest chest, Rectangle bounds, float opacity = 1f)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0) return;
        var data = ItemRegistry.GetDataOrErrorItem(chest.QualifiedItemId);
        Texture2D texture = data.GetTexture();
        Color dye = chest.playerChoiceColor.Value;
        bool dyeable = chest.QualifiedItemId is "(BC)130" or "(BC)232" or "(BC)BigChest" or "(BC)BigStoneChest";
        if (!dyeable || dye == Color.Black)
        {
            // Black is the game's sentinel for the original wood/stone colour, not black paint.
            DrawSprite(batch, texture, data.GetSourceRect(), bounds, opacity);
            return;
        }

        int bodyIndex = chest.ParentSheetIndex;
        int lidIndex = chest.startingLidFrame.Value;
        int hardwareIndex = lidIndex + 8;
        if (chest.QualifiedItemId == "(BC)130")
        {
            bodyIndex = 168;
            hardwareIndex = lidIndex + 46;
            lidIndex += 38;
        }
        else if (chest.QualifiedItemId == "(BC)BigChest")
        {
            bodyIndex = 312;
            hardwareIndex = lidIndex + 16;
            lidIndex += 8;
        }

        // Fit all layers together; drawing each separately would recenter the feet overlay.
        // Read the live colour without changing the placed chest's lid, position or state.
        Rectangle body = data.GetSourceRect(0, bodyIndex);
        float scale = Math.Min((float)bounds.Width / body.Width, (float)bounds.Height / body.Height);
        if (scale >= 1f) scale = MathF.Floor(scale);
        Vector2 position = new(bounds.X + (bounds.Width - body.Width * scale) / 2f,
            bounds.Y + (bounds.Height - body.Height * scale) / 2f);
        batch.Draw(texture, position, body, dye * opacity, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        batch.Draw(texture, position, data.GetSourceRect(0, lidIndex), dye * opacity,
            0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        batch.Draw(texture, position + new Vector2(0, 21 * scale),
            new Rectangle(0, bodyIndex / 8 * 32 + 53, 16, 11), Color.White * opacity,
            0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        batch.Draw(texture, position, data.GetSourceRect(0, hardwareIndex), Color.White * opacity,
            0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }

    internal static void DrawSprite(SpriteBatch batch, Texture2D texture, Rectangle source, Rectangle bounds, float opacity = 1f)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0) return;
        float scale = Math.Min((float)bounds.Width / source.Width, (float)bounds.Height / source.Height);
        // Whole-pixel sprite scaling when space permits keeps native art crisp.
        if (scale >= 1f) scale = MathF.Floor(scale);
        int width = Math.Max(1, (int)(source.Width * scale));
        int height = Math.Max(1, (int)(source.Height * scale));
        Rectangle destination = new(bounds.X + (bounds.Width - width) / 2,
            bounds.Y + (bounds.Height - height) / 2, width, height);
        batch.Draw(texture, destination, source, Color.White * opacity);
    }
}
