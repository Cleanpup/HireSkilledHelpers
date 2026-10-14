namespace HireSkilledHelpers.Workers;

/// <summary>Mining-only hourly expedition rewards. These are worker balance rates,
/// informed by native ore/gem progression, NOT vanilla per-rock drop chances.</summary>
internal static class WorkerMiningLootCatalog
{
    private readonly record struct Drop(string Id, int Weight);
    private static readonly Drop[] CopperOre = { new("378", 1) };
    private static readonly Drop[] EarlyIronOre = { new("378", 9), new("380", 1) };
    private static readonly Drop[] IronOre = { new("378", 1), new("380", 3) };
    private static readonly Drop[] EarlyGoldOre = { new("378", 9), new("380", 27), new("384", 4) };
    private static readonly Drop[] GoldOre = { new("378", 1), new("380", 3), new("384", 12) };
    private static readonly Drop[] VolcanoOre = { new("378", 2), new("380", 4), new("384", 6), new("848", 7), new("386", 1) };
    private static readonly Drop[] EarlyGems = { new("66", 1), new("68", 1) };
    private static readonly Drop[] MiddleGems = { new("66", 1), new("68", 1), new("62", 1), new("70", 1) };
    private static readonly Drop[] MiddleDiamondGems = { new("66", 19), new("68", 19), new("62", 19), new("70", 19), new("72", 4) };
    private static readonly Drop[] DeepGems = { new("66", 3), new("68", 3), new("62", 3), new("70", 3), new("60", 3), new("64", 3), new("72", 2) };

    /// <summary>Delivery validates mining provenance and source area, without
    /// reapplying progression gates to rewards that have already been earned.</summary>
    public static bool IsAllowed(string area, string itemId)
    {
        if (!WorkerExplorationAreaCatalog.IsValid(area) || string.IsNullOrEmpty(itemId)) return false;
        string id = itemId.StartsWith("(O)", StringComparison.Ordinal) ? itemId[3..] : itemId;
        if (id is "378" or "380" or "384" or "382" or "66" or "68" or "62" or "70" or "60" or "64" or "72" or "74" or "749") return true;
        return area switch
        {
            WorkerExplorationAreaCatalog.Mines => id is "535" or "536" or "537",
            WorkerExplorationAreaCatalog.SkullCavern => id is "386",
            WorkerExplorationAreaCatalog.Volcano => id is "386" or "848",
            _ => false,
        };
    }

    public static double ClampDailyLuck(double dailyLuck)
        => double.IsFinite(dailyLuck) ? Math.Clamp(dailyLuck, -.1, .1) : 0;

    private static string Choose(Drop[] pool, Random random)
    {
        int roll = random.Next(pool.Sum(item => item.Weight));
        foreach (Drop item in pool)
        {
            roll -= item.Weight;
            if (roll < 0) return item.Id;
        }
        throw new InvalidOperationException("Mining reward pool has no eligible item.");
    }

    public static List<WorkerExplorationLoot> Roll(string area, Random random, int deepestMineLevel, double dailyLuck)
    {
        if (!WorkerExplorationAreaCatalog.IsValid(area)) throw new ArgumentOutOfRangeException(nameof(area));
        ArgumentNullException.ThrowIfNull(random);
        // Native deepestMineLevel includes Skull Cavern as 120 + its floor.
        // Mines rewards must never acquire iridium merely by reaching floor 120.
        int mineDepth = Math.Clamp(deepestMineLevel, 0, 119);
        int skullDepth = Math.Max(0, Math.Max(0, deepestMineLevel) - 120);
        double luck = ClampDailyLuck(dailyLuck);
        double luckFactor = 1 + 2 * luck;
        Drop[] ore;
        if (area == WorkerExplorationAreaCatalog.Mines)
            ore = mineDepth < 20 ? CopperOre : mineDepth < 40 ? EarlyIronOre
                : mineDepth < 60 ? IronOre : mineDepth < 80 ? EarlyGoldOre : GoldOre;
        else if (area == WorkerExplorationAreaCatalog.SkullCavern)
        {
            int iridiumPercent = skullDepth < 25 ? 10 : skullDepth < 50 ? 20 : skullDepth < 100 ? 30 : 40;
            int remaining = 100 - iridiumPercent;
            ore = new[] { new Drop("378", remaining), new Drop("380", remaining * 3), new Drop("384", remaining * 12), new Drop("386", iridiumPercent * 16) };
        }
        else ore = VolcanoOre;
        Drop[] gems = area != WorkerExplorationAreaCatalog.Mines || mineDepth >= 80 ? DeepGems
            : mineDepth > 50 ? MiddleDiamondGems : mineDepth >= 40 ? MiddleGems : EarlyGems;
        List<WorkerExplorationLoot> result = new();
        void Add(string id, int stack = 1) => result.Add(new()
        {
            Area = area, ItemId = "(O)" + id, Stack = stack, IsMining = true,
        });
        int units = random.Next(3, 6);
        if (random.NextDouble() < .2 + luck) units++;
        Add(Choose(ore, random), units);
        if (random.NextDouble() < .5 * luckFactor) Add("382", random.Next(1, 3));
        if (random.NextDouble() < .2 * luckFactor) Add(Choose(gems, random));
        if (random.NextDouble() < .2 * luckFactor)
        {
            string geode = area != WorkerExplorationAreaCatalog.Mines ? "749"
                : mineDepth < 40 ? "535" : mineDepth < 80 ? "536" : "537";
            if (area == WorkerExplorationAreaCatalog.Mines && mineDepth > 20 && random.NextDouble() < .1) geode = "749";
            Add(geode);
        }
        double shardChance = area switch
        {
            WorkerExplorationAreaCatalog.Mines => deepestMineLevel >= 120 ? .0004 : 0,
            WorkerExplorationAreaCatalog.SkullCavern => .001,
            WorkerExplorationAreaCatalog.Volcano => .0005,
            _ => 0,
        };
        if (shardChance > 0 && random.NextDouble() < shardChance * luckFactor) Add("74");
        return result;
    }
}
