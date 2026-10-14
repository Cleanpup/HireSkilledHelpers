using System.Text.Json;
using HireSkilledHelpers.Workers;

internal static class MiningExpeditionChecks
{
    public static void Run()
    {
        void Check(bool value, string message) { if (!value) throw new Exception(message); }
        WorkerRosterEntry Ready() => new()
        {
            Profession = WorkerProfession.Miner, AssignedTask = WorkerTaskKind.MiningExpedition,
            ExplorationArea = WorkerExplorationAreaCatalog.Mines,
            Exploration = new() { Day = 4, Minutes = 60, LastObservedMinute = 420 },
        };
        Check((int)WorkerTaskKind.MiningExpedition == 14 && (int)WorkerTaskKind.Fish == 13, "Stable task identifiers");
        foreach (var profession in Enum.GetValues<WorkerProfession>())
            Check(WorkerTaskPolicy.IsTaskAllowed(profession, WorkerTaskKind.MiningExpedition) == (profession == WorkerProfession.Miner), "Only Miners produce mining resources");
        var worker = Ready();
        var progress = worker.Exploration.Clone();
        var loot = WorkerMiningLootCatalog.Roll("Mines", new Random(42), 80, 0);
        Check(loot.All(x => x.IsMining && WorkerMiningLootCatalog.IsAllowed("Mines", x.ItemId)), "Mining-only provenance");
        Check(!WorkerExplorationPolicy.TryQueueRun(worker, progress, loot, 4, false, true, true, false), "Client cannot generate loot");
        Check(!WorkerExplorationPolicy.TryQueueRun(worker, progress, loot, 4, true, false, true, false), "No world no loot");
        Check(!WorkerExplorationPolicy.TryQueueRun(worker, progress, loot, 4, true, true, false, false), "Unpaid no loot");
        Check(!WorkerExplorationPolicy.TryQueueRun(worker, progress, loot, 4, true, true, true, true), "Defeated no loot");
        Check(WorkerExplorationPolicy.TryQueueRun(worker, progress, loot, 4, true, true, true, false), "Completed mining hour pays");
        Check(worker.Experience.Mining == 5 && worker.Experience.Combat == 0, "Only Mining XP awarded");
        Check(!WorkerExplorationPolicy.TryQueueRun(worker, progress, loot, 4, true, true, true, false), "No duplicate payout");
        worker = JsonSerializer.Deserialize<WorkerRosterEntry>(JsonSerializer.Serialize(worker))!;
        Check(worker.Exploration.CompletedRuns == 1 && worker.Exploration.PendingLoot.All(WorkerExplorationPolicy.IsAllowedPendingLoot), "Saved mining rewards survive reload");
        worker.Profession = WorkerProfession.Farmer;
        worker.AssignedTask = WorkerTaskKind.Idle;
        worker.ExplorationArea = "Volcano";
        Check(worker.Exploration.PendingLoot.All(WorkerExplorationPolicy.IsAllowedPendingLoot), "Earned loot survives role/area changes");
        var invalid = new List<WorkerExplorationLoot> { new() { IsMining = true, Area = "Mines", ItemId = "(O)766", Stack = 1 } };
        worker = Ready();
        Check(!WorkerExplorationPolicy.TryQueueRun(worker, worker.Exploration.Clone(), invalid, 4, true, true, true, false), "No slime in Miner production");
        invalid[0].ItemId = "(O)378"; invalid[0].IsMining = false;
        Check(!WorkerExplorationPolicy.TryQueueRun(worker, worker.Exploration.Clone(), invalid, 4, true, true, true, false), "No untagged Fighter rewards in mining run");
        worker.Profession = WorkerProfession.CombatWorker; worker.AssignedTask = WorkerTaskKind.ExploreArea;
        invalid[0].IsMining = true;
        Check(!WorkerExplorationPolicy.TryQueueRun(worker, worker.Exploration.Clone(), invalid, 4, true, true, true, false), "No mining batch in Fighter run");
        worker = Ready(); worker.Exploration.CompletedRuns = 16;
        Check(!WorkerExplorationPolicy.TryQueueRun(worker, worker.Exploration.Clone(), loot, 4, true, true, true, false), "Daily cap holds");
        var clock = new WorkerExplorationProgress();
        WorkerExplorationPolicy.ObserveClock(clock, 4, 600, true);
        WorkerExplorationPolicy.ObserveClock(clock, 4, 630, true);
        WorkerExplorationPolicy.ObserveClock(clock, 4, 700, false);
        WorkerExplorationPolicy.ObserveClock(clock, 4, 730, true);
        Check(clock.Minutes == 60, "Paused intervals excluded");
        WorkerExplorationPolicy.ObserveClock(clock, 4, 700, true);
        Check(clock.Minutes == 60, "Rewound clock cannot credit twice");
        clock.PendingLoot.AddRange(loot);
        WorkerExplorationPolicy.ObserveClock(clock, 5, 600, true);
        Check(clock.Minutes == 0 && clock.CompletedRuns == 0 && clock.PendingLoot.Count == loot.Count, "Next day resets timer not rewards");
        foreach (string area in new[] { "Mines", "SkullCavern", "Volcano" })
        foreach (int depth in new[] { 0, 19, 20, 39, 40, 50, 51, 59, 60, 79, 80, 119, 120, 144, 145, 169, 170, 219, 220 })
        foreach (double luck in new[] { -.1, 0, .1 })
        {
            Random random = new(500 + depth);
            for (int i = 0; i < 2000; i++)
            foreach (var drop in WorkerMiningLootCatalog.Roll(area, random, depth, luck))
            {
                Check(drop.Stack > 0 && drop.IsMining && WorkerExplorationPolicy.IsAllowedPendingLoot(drop), "Generated valid saved mining loot");
                Check(drop.ItemId is not ("(O)390" or "(O)73" or "(O)842" or "(O)766" or "(O)910"), "No stone/quest/monster/radioactive loot");
                if (area == "Mines")
                {
                    Check(drop.ItemId != "(O)386", "No Skull iridium bleeds into Mines");
                    if (depth < 20) Check(drop.ItemId != "(O)380", "No iron below gate");
                    if (depth < 60) Check(drop.ItemId != "(O)384", "No gold below gate");
                    if (depth < 120) Check(drop.ItemId != "(O)74", "No early prismatic shards");
                }
            }
        }
        Console.WriteLine("Independent mining: authority, timing, saved provenance, role changes and depth/luck gates pass.");
    }
}
