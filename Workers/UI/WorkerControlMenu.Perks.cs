using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace HireSkilledHelpers.Workers;

internal sealed partial class WorkerControlMenu
{
    private const int PerksTabId = 94000, PerkPreviousId = 94001, PerkNextId = 94002, PerkChoiceId = 94010;
    private ClickableComponent perksTabButton = null!, perkPreviousButton = null!, perkNextButton = null!;
    private readonly List<ClickableComponent> perkChoiceButtons = new();
    private readonly List<(Rectangle Bounds, string Text)> perkHoverCards = new();
    private Rectangle perkContent, perkTitle;
    // Applied overview followed by one page per skill. This keeps choices readable in compact windows.
    private int perkPage;

    private void BuildPerkLayout()
    {
        int top = this.ordersBounds.Y + 12;
        this.perkPreviousButton = Button(PerkPreviousId, new(this.ordersBounds.X + 16, top, 42, 36));
        this.perkNextButton = Button(PerkNextId, new(this.ordersBounds.Right - 58, top, 42, 36));
        this.perkTitle = new(this.ordersBounds.X + 64, top, this.ordersBounds.Width - 128, 36);
        this.perkContent = new(this.ordersBounds.X + 16, top + 46, this.ordersBounds.Width - 32,
            Math.Max(1, this.ordersBounds.Bottom - top - 58));
        this.perkChoiceButtons.Clear();
        bool columns = this.perkContent.Height < 240;
        int w = columns ? (this.perkContent.Width - 10) / 2 : this.perkContent.Width;
        int h = columns ? this.perkContent.Height : (this.perkContent.Height - 10) / 2;
        for (int i = 0; i < 2; i++) this.perkChoiceButtons.Add(Button(PerkChoiceId + i,
            new(this.perkContent.X + (columns ? i * (w + 10) : 0), this.perkContent.Y + (columns ? 0 : i * (h + 10)), w, h)));
    }

    private bool HandlePerkClick(int x, int y)
    {
        if (this.perkPreviousButton.containsPoint(x, y) || this.perkNextButton.containsPoint(x, y))
        {
            bool next = this.perkNextButton.containsPoint(x, y);
            this.perkPage = (this.perkPage + (next ? 1 : 5)) % 6;
            this.RebuildLayout(next ? PerkNextId : PerkPreviousId);
            Game1.playSound("smallSelect");
            return true;
        }
        if (this.perkPage == 0)
        {
            for (int i = 3; i < this.perkHoverCards.Count; i++)
                if (this.perkHoverCards[i].Bounds.Contains(x, y))
                {
                    this.perkPage = i - 2;
                    this.RebuildLayout(PerkNextId);
                    Game1.playSound("smallSelect");
                    return true;
                }
            return false;
        }
        for (int i = 0; i < this.perkChoiceButtons.Count; i++)
        {
            if (!this.perkChoiceButtons[i].containsPoint(x, y)) continue;
            if (this.EnsureSelectedHostWorker())
            {
                bool success = this.workerShellManager.TrySelectPerk(this.selectedWorkerId!, (WorkerSkill)(this.perkPage - 1), i + 1, out string message);
                this.SetFeedback(message, !success);
                this.RefreshSnapshots();
            }
            return true;
        }
        return false;
    }

    private void DrawPerksPanel(SpriteBatch b)
    {
        this.perkHoverCards.Clear();
        this.DrawButton(b, this.perkPreviousButton, "<", true, Color.White);
        this.DrawButton(b, this.perkNextButton, ">", true, Color.White);
        var worker = this.GetSelectedWorker();
        if (worker is null)
        {
            this.DrawText(b, "Select a worker to view perks.", this.perkContent, MutedInk, true);
            return;
        }
        string id = worker.Value.WorkerId;
        WorkerSkillExperience xp = this.workerShellManager.GetWorkerExperience(id);
        if (this.perkPage == 0)
        {
            this.DrawSingleLineText(b, "Applied perks  •  1 / 6", this.perkTitle, Ink, true);
            var cards = new List<(string Name, string Description, bool Active)>
            {
                ("Endurance", "Work skill Lv. 5: 540 stamina", WorkerPerkPolicy.HasStaminaPerk(xp)),
                ("Vitality", "Combat Lv. 5: double health", WorkerPerkPolicy.HasHealthPerk(xp)),
                ("Fleet-footed", "Any skill Lv. 7: +75% movement once", WorkerPerkPolicy.HasMovementPerk(xp)),
            };
            foreach (WorkerSkill skill in Enum.GetValues<WorkerSkill>())
            {
                int choice = this.workerShellManager.GetActivePerk(id, skill);
                bool unlocked = WorkerExperiencePolicy.GetLevel(WorkerPerkPolicy.Experience(xp, skill)) >= 10;
                cards.Add((skill.ToString(), choice > 0 ? WorkerPerkPolicy.Name(skill, choice)
                    : unlocked ? "Choose a Lv. 10 perk →" : "Unlocks at Lv. 10", choice > 0));
            }
            int h = Math.Max(1, (this.perkContent.Height - 18) / 4), w = (this.perkContent.Width - 8) / 2;
            for (int i = 0; i < cards.Count; i++)
            {
                Rectangle bounds = new(this.perkContent.X + (i % 2) * (w + 8), this.perkContent.Y + (i / 2) * (h + 6), w, h);
                var card = cards[i];
                this.DrawCard(b, bounds, card.Active ? PaperShade : Paper, card.Active);
                int titleHeight = Math.Max(1, (h - 12) / 2);
                this.DrawSingleLineText(b, card.Name, new(bounds.X + 8, bounds.Y + 4, w - 16, titleHeight), card.Active ? Leaf : Ink, ellipsize: true);
                this.DrawSingleLineText(b, card.Description, new(bounds.X + 8, bounds.Y + 6 + titleHeight, w - 16, titleHeight), MutedInk, ellipsize: true);
                this.perkHoverCards.Add((bounds, card.Name + ": " + card.Description + (card.Active ? " (active)" : " (inactive)")));
            }
            return;
        }
        WorkerSkill selected = (WorkerSkill)(this.perkPage - 1);
        int level = WorkerExperiencePolicy.GetLevel(WorkerPerkPolicy.Experience(xp, selected));
        this.DrawSingleLineText(b, $"{selected} Lv. {level}  •  {this.perkPage + 1} / 6", this.perkTitle, Ink, true);
        int active = this.workerShellManager.GetActivePerk(id, selected);
        for (int i = 0; i < 2; i++)
        {
            int choice = i + 1;
            var button = this.perkChoiceButtons[i];
            string status = active == choice ? "ACTIVE" : level < 10 ? "LOCKED · LEVEL 10" : Context.IsMainPlayer ? "SELECT · FREE SWITCH" : "HOST SELECTS";
            this.DrawCard(b, button.bounds, active == choice ? PaperShade : this.IsControlHighlighted(button) ? new Color(255, 239, 198) : Paper, active == choice);
            int titleHeight = Math.Min(30, Math.Max(16, button.bounds.Height / 4));
            this.DrawSingleLineText(b, WorkerPerkPolicy.Name(selected, choice),
                new(button.bounds.X + 12, button.bounds.Y + 8, button.bounds.Width - 24, titleHeight), Ink, ellipsize: true);
            this.DrawSingleLineText(b, status, new(button.bounds.X + 12, button.bounds.Bottom - titleHeight - 8, button.bounds.Width - 24, titleHeight),
                active == choice ? Leaf : MutedInk, maximumScale: .85f);
            string description = WorkerPerkPolicy.Description(selected, choice);
            int available = button.bounds.Height - titleHeight * 2 - 24;
            if (available >= 24) this.DrawText(b, description,
                new(button.bounds.X + 12, button.bounds.Y + titleHeight + 12, button.bounds.Width - 24, available), MutedInk);
            this.perkHoverCards.Add((button.bounds, description + "\n" + status + ". Only one choice per skill; different skills stack."));
        }
    }

    private void HoverPerks(int x, int y)
    {
        foreach (var card in this.perkHoverCards)
            if (card.Bounds.Contains(x, y)) { this.hoverText = Game1.parseText(card.Text, Game1.smallFont, Math.Max(160, Math.Min(480, Game1.uiViewport.Width - 64))); return; }
    }
}
