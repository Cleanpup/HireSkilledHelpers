using System.Text.Json;
using HireSkilledHelpers.Workers;

internal static class PerkChecks
{
    private static void Check(bool value, string scenario)
    {
        if (!value) throw new Exception(scenario);
    }

    public static void Run()
    {
        int threshold = WorkerExperiencePolicy.GetThreshold(5);
        Check(WorkerPerkPolicy.MaximumStamina(null) == 270 && WorkerPerkPolicy.MaximumHealth(null) == 100,
            "Missing/legacy XP has base stamina and health");
        foreach (var setSkill in new Action<WorkerSkillExperience, int>[] {
            (s, xp) => s.Farming = xp, (s, xp) => s.Mining = xp,
            (s, xp) => s.Fishing = xp, (s, xp) => s.Foraging = xp })
        {
            var skills = new WorkerSkillExperience();
            setSkill(skills, threshold - 1);
            Check(WorkerPerkPolicy.MaximumStamina(skills) == 270, "Level four has no stamina perk");
            setSkill(skills, threshold);
            Check(WorkerPerkPolicy.MaximumStamina(skills) == 540 && WorkerPerkPolicy.MaximumHealth(skills) == 100,
                "Each work skill unlocks doubled stamina at exactly level five, without health");
            setSkill(skills, int.MaxValue);
            Check(WorkerPerkPolicy.MaximumStamina(skills) == 540, "Higher levels retain the same stamina perk");
        }
        var all = new WorkerSkillExperience { Farming = threshold, Mining = threshold, Fishing = threshold, Foraging = threshold, Combat = threshold };
        Check(WorkerPerkPolicy.MaximumStamina(all) == 540 && WorkerPerkPolicy.MaximumHealth(all) == 200,
            "Multiple work skills do not repeatedly double the shared stamina pool");
        var combat = new WorkerSkillExperience { Combat = threshold - 1 };
        Check(WorkerPerkPolicy.MaximumHealth(combat) == 100, "Combat level four keeps base health");
        combat.Combat = threshold;
        Check(WorkerPerkPolicy.MaximumHealth(combat) == 200 && WorkerPerkPolicy.MaximumStamina(combat) == 270,
            "Combat level five doubles health, not stamina");
        Check(WorkerTaskPolicy.GetProfessionLabel(WorkerProfession.CombatWorker) == "Fighter"
            && (int)WorkerProfession.CombatWorker == 2, "Fighter label preserves saved profession identity");

        var worker = new WorkerRosterEntry { Experience = all, Stamina = new() { Day = 100, Current = 135 } };
        Check(WorkerStaminaPolicy.SynchronizeMaximum(worker.Stamina, WorkerPerkPolicy.MaximumStamina(all))
            && worker.Stamina.Current == 270 && worker.Stamina.Maximum == 540,
            "Unlock preserves the remaining stamina fraction instead of refilling");
        Check(!WorkerStaminaPolicy.SynchronizeMaximum(worker.Stamina, 540) && worker.Stamina.Current == 270,
            "Repeated unlock observation cannot grant extra energy");
        var saved = JsonSerializer.Deserialize<WorkerRosterEntry>(JsonSerializer.Serialize(worker))!;
        Check(saved.Clone().Stamina.Current == 270 && saved.Clone().Stamina.Maximum == 540,
            "Save and clone preserve boosted maximum");
        saved.Stamina.Current = 500;
        Check(saved.Clone().Stamina.Current == 500, "Cloning no longer truncates boosted stamina to 270");
        WorkerStaminaPolicy.SynchronizeMaximum(saved.Stamina, WorkerPerkPolicy.MaximumStamina(saved.Experience));
        WorkerStaminaPolicy.Observe(saved.Stamina, 100, 600, false);
        Check(saved.Stamina.Current == 500, "Same-day reload cannot refill boosted stamina");
        WorkerStaminaPolicy.Observe(saved.Stamina, 101, 600, false);
        Check(saved.Stamina.Current == 540, "New day refills the boosted pool");
        saved.Stamina.Current = 50; saved.Stamina.Resting = true; saved.Stamina.WasRecovering = true;
        WorkerStaminaPolicy.Observe(saved.Stamina, 101, 650, true);
        Check(saved.Stamina.Current == 100 && saved.Stamina.Resting, "Boosted pool rests below its 20-percent threshold");
        WorkerStaminaPolicy.Observe(saved.Stamina, 101, 700, true);
        Check(saved.Stamina.Current == 110 && !saved.Stamina.Resting, "Boosted pool resumes at 108 stamina");
        WorkerStaminaPolicy.Observe(saved.Stamina, 101, 2300, true);
        Check(saved.Stamina.Current == 540, "Idle recovery caps at boosted maximum");
        float before = saved.Stamina.Current;
        Check(WorkerStaminaPolicy.TryUse(saved, WorkerStaminaAction.Pickaxe, true, true, true)
            && Math.Abs(saved.Stamina.Current - (before - 1.5f)) < 0.0001f,
            "Stamina perk leaves vanilla level-five action cost unchanged");
        var client = new WorkerRosterEntry { Experience = all };
        Check(!WorkerStaminaPolicy.TryUse(client, WorkerStaminaAction.Axe, false, true, true)
            && client.Stamina.Maximum == 270 && client.Stamina.Current == 270,
            "Client execution cannot apply or spend a perk");
        worker.Stamina.Current = 0;
        WorkerStaminaPolicy.SynchronizeMaximum(worker.Stamina, 540);
        Check(worker.Stamina.Current == 0, "Stamina perk does not restore an exhausted pool");

        Check(WorkerCombatPolicy.NormalizeHealth(50, 100, 200) == 100, "HP unlock preserves missing-health fraction");
        Check(WorkerCombatPolicy.NormalizeHealth(0, 100, 200) == 0, "Health perk cannot revive a defeated worker");
        Check(WorkerCombatPolicy.NormalizeHealth(150, 200, 200) == 150, "Boosted health survives same-day normalization");
        Check(WorkerCombatPolicy.HealthAfterContact(200, 8, 200) == 192, "Damage starts from boosted health without clamping to 100");
        Check(WorkerCombatPolicy.ClampHealth(500, 200) == 200, "Boosted health still caps at maximum");
        var second = new WorkerRosterEntry();
        Check(WorkerPerkPolicy.MaximumStamina(second.Experience) == 270 && WorkerPerkPolicy.MaximumHealth(second.Experience) == 100,
            "Perks remain independent between workers");
        OverhaulChecks();
        Console.WriteLine("Level-five, seven and ten perk checks passed: stacking, authority, switching, HP, persistence and production.");
    }
    private static void OverhaulChecks()
    {
        var entry = new WorkerRosterEntry();
        foreach (int level in new[] { 10, 7, 5, 10 })
        {
            int prior = entry.Experience.Farming;
            Check(!WorkerExperiencePolicy.TrySetTestLevel(entry, level, false, true)
                && entry.Experience.Farming == prior, "Client cannot change testing levels");
            Check(!WorkerExperiencePolicy.TrySetTestLevel(entry, level, true, false), "Testing level requires loaded world");
            entry.Perks.Combat = 2;
            Check(WorkerExperiencePolicy.TrySetTestLevel(entry, level, true, true), "Host can set supported test levels");
            Check(Enum.GetValues<WorkerSkill>().All(skill => WorkerExperiencePolicy.GetLevel(WorkerPerkPolicy.Experience(entry.Experience, skill)) == level), "Every worker skill has exact requested level including decreases");
            Check(entry.Perks.Combat == 2 && WorkerPerkPolicy.Active(entry, WorkerSkill.Combat) == (level == 10 ? 2 : 0), "Test level retains choices but respects unlock gates");
        }
        Check(!WorkerExperiencePolicy.TrySetTestLevel(entry, 6, true, true), "Unsupported testing level rejected");
        entry = new();
        foreach (WorkerSkill skill in Enum.GetValues<WorkerSkill>())
        {
            Check(!WorkerPerkPolicy.TrySelect(entry, skill, 1, true, true), "Locked selection rejected");
            entry.Perks.Set(skill, 2);
            Check(WorkerPerkPolicy.Active(entry, skill) == 0, "Saved/forged choice cannot bypass XP gate");
        }
        entry.Experience.Combat = WorkerExperiencePolicy.GetThreshold(7) - 1;
        Check(!WorkerPerkPolicy.HasMovementPerk(entry.Experience), "Level six has no speed bonus");
        entry.Experience.Combat++;
        Check(WorkerPerkPolicy.HasMovementPerk(entry.Experience), "Combat alone unlocks shared speed at seven");
        var notice = new WorkerNoticePolicy();
        Check(!notice.Observe("one", WorkerTaskKind.Idle), "Initial idle load is silent");
        Check(!notice.Observe("one", WorkerTaskKind.CollectForage), "Starting work is silent");
        Check(notice.Observe("one", WorkerTaskKind.Idle), "Finishing work announces idle");
        Check(!notice.Observe("one", WorkerTaskKind.Idle), "Repeated idle observation does not spam");
        notice.Observe("one", WorkerTaskKind.Fish);
        Check(notice.ObserveRuntime("one", true), "Night return announces idle even if daily job remains assigned");
        Check(!notice.Observe("one", WorkerTaskKind.Fish) && !notice.ObserveRuntime("one", true), "Unchanged assignment save cannot repeat night notice");
        notice.ObserveRuntime("one", false);
        Check(notice.ObserveRuntime("one", true), "Another completed work interval can notify again");
        notice.Clear();
        Check(!notice.Observe("one", WorkerTaskKind.Idle), "New save cannot inherit old notices");
        foreach (string berry in new[] { "(O)296", "(O)410" })
        {
            Check(WorkerBerryPolicy.CanHarvest(1, false, false, true, true, berry), "Ready salmon/blackberry bush eligible");
            Check(!WorkerBerryPolicy.CanHarvest(1, false, false, false, true, berry), "Picked berry bush not eligible");
            Check(!WorkerBerryPolicy.CanHarvest(1, false, false, true, false, berry), "Out-of-season berry bush not eligible");
            Check(!WorkerBerryPolicy.CanHarvest(1, true, false, true, true, berry), "Town/decorative bush not eligible");
        }
        Check(!WorkerBerryPolicy.CanHarvest(3, false, false, true, true, "(O)815")
            && !WorkerBerryPolicy.CanHarvest(4, false, false, true, true, "(O)73"), "Tea/walnut harvests excluded");
        Check(WorkerBerryPolicy.Yield(0) == 1 && WorkerBerryPolicy.Yield(7) == 2 && WorkerBerryPolicy.Yield(10) == 3, "Berries use worker level for vanilla yield");
        Check(WorkerBerryPolicy.BehindBeachBridge(false, 65, 18)
            && !WorkerBerryPolicy.BehindBeachBridge(true, 65, 18)
            && !WorkerBerryPolicy.BehindBeachBridge(false, 50, 18), "Beach access blocks only locked eastern forage");
        int ten = WorkerExperiencePolicy.GetThreshold(10);
        entry.Experience = new() { Farming = ten, Mining = ten, Fishing = ten, Foraging = ten, Combat = ten };
        entry.Perks = new();
        foreach (WorkerSkill skill in Enum.GetValues<WorkerSkill>())
        {
            Check(!WorkerPerkPolicy.TrySelect(entry, skill, 1, false, true), "Farmhand cannot select");
            Check(!WorkerPerkPolicy.TrySelect(entry, skill, 1, true, false), "Selection requires world");
            Check(!WorkerPerkPolicy.TrySelect(entry, skill, 3, true, true), "Only two choices per tree");
            Check(WorkerPerkPolicy.TrySelect(entry, skill, 1, true, true), "Host can select unlocked perk");
        }
        Check(Enum.GetValues<WorkerSkill>().All(skill => WorkerPerkPolicy.Active(entry, skill) == 1), "All five skills stack across profession");
        Check(WorkerPerkPolicy.TrySelect(entry, WorkerSkill.Foraging, 2, true, true)
            && WorkerPerkPolicy.Active(entry, WorkerSkill.Foraging) == 2
            && WorkerPerkPolicy.Active(entry, WorkerSkill.Farming) == 1, "Switch replaces only chosen tree");
        Check(WorkerPerkPolicy.HarvestMultiplier(WorkerPerkPolicy.Active(entry, WorkerSkill.Foraging), true) == 1
            && WorkerPerkPolicy.IridiumHarvest(WorkerPerkPolicy.Active(entry, WorkerSkill.Foraging), true), "Forage quality excludes quantity");
        Check(WorkerPerkPolicy.HarvestMultiplier(1, true) == 5 && !WorkerPerkPolicy.IridiumHarvest(1, true)
            && WorkerPerkPolicy.HarvestMultiplier(2, false) == 3 && !WorkerPerkPolicy.IridiumHarvest(2, false), "Harvest perks are exclusive");
        var clone = JsonSerializer.Deserialize<WorkerRosterEntry>(JsonSerializer.Serialize(entry))!.Clone();
        clone.Perks.Farming = 2;
        Check(entry.Perks.Farming == 1 && clone.Perks.Foraging == 2, "Choices persist and clone independently");
        var legacy = JsonSerializer.Deserialize<WorkerRosterEntry>("{}")!.Clone();
        Check(Enum.GetValues<WorkerSkill>().All(skill => legacy.Perks.Get(skill) == 0), "Legacy roster starts unselected");
        legacy.Perks.Farming = 999;
        Check(legacy.Clone().Perks.Farming == 0, "Invalid saved choices normalize");

        entry.Perks.Combat = 2;
        Check(WorkerPerkPolicy.MaximumHealth(entry.Experience, entry.Perks) == 800, "Juggernaut stacks with level five");
        entry.CombatHealth = 397; entry.CombatMaxHealth = 800;
        for (int i = 0; i < 20; i++)
        {
            entry.Perks.Combat = 1; WorkerPerkPolicy.SynchronizeHealth(entry);
            entry = JsonSerializer.Deserialize<WorkerRosterEntry>(JsonSerializer.Serialize(entry))!.Clone();
            entry.Perks.Combat = 2; WorkerPerkPolicy.SynchronizeHealth(entry);
            Check(entry.CombatHealth == 397, "Repeated switching and save/reload cannot heal through rounding");
        }
        entry.CombatHealth = 0;
        entry.Perks.Combat = 1; WorkerPerkPolicy.SynchronizeHealth(entry);
        entry.Perks.Combat = 2; WorkerPerkPolicy.SynchronizeHealth(entry);
        Check(entry.CombatHealth == 0, "Switch cannot revive defeated worker");

        foreach (string area in new[] { "Mines", "SkullCavern", "Volcano" })
        {
            var seen = new HashSet<string>();
            for (int seed = 0; seed < 1000; seed++)
            {
                var normal = WorkerMiningLootCatalog.Roll(area, new Random(seed), 0, 0);
                var doubled = WorkerMiningLootCatalog.Roll(area, new Random(seed), 0, 0, 1);
                Check(normal.Count == doubled.Count && normal.Zip(doubled).All(pair => pair.First.ItemId == pair.Second.ItemId
                    && pair.Second.Stack == pair.First.Stack * 2), "Double mining changes quantity, preserves normal gates");
                var geodes = WorkerMiningLootCatalog.Roll(area, new Random(seed), 0, 0, 2);
                Check(geodes.All(item => item.ItemId is "(O)535" or "(O)536" or "(O)537" or "(O)749"), "Specialist produces only geodes");
                Check(geodes.Sum(item => item.Stack) == normal.Sum(item => item.Stack) * 2, "Specialist doubles total production rate");
                foreach (var drop in geodes)
                {
                    Check(WorkerExplorationPolicy.IsAllowedPendingLoot(drop), "Specialist loot retains valid provenance/storage");
                    if (drop.ItemId is "(O)535" or "(O)536" or "(O)537" or "(O)749")
                    { seen.Add(drop.ItemId); Check(drop.Stack > 0, "Specialist produces positive geode units"); }
                }
            }
            Check(seen.Count == 4, "All four geode types available at depth zero in every expedition");
        }
    }

}
