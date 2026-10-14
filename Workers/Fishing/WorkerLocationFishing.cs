using System.Globalization;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.GameData.Locations;
using StardewValley.Tools;

namespace HireSkilledHelpers.Workers;

/// <summary>One unbaited, normal-quality cast using live 1.6 location and fish assets for farms and optional SVE areas.</summary>
internal static class WorkerLocationFishing
{
    public static WorkerFishingCatch? Roll(GameLocation location, Point shore, Point bobber, int level,
        int completedAttempts, Random random)
    {
        int depth = FishingRod.distanceToLand(bobber.X, bobber.Y, location);
        if (location.NameOrUniqueName == WorkerFishingAreaCatalog.Farm)
        {
            string[] fields = location.getMapProperty("FarmFishLocationOverride")?.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                ?? Array.Empty<string>();
            if (fields.Length >= 2 && float.TryParse(fields[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float chance)
                && chance > 0 && random.NextDouble() < chance)
            {
                GameLocation? target = Game1.getLocationFromName(fields[0]);
                if (target is null) return null;
                location = target;
            }
        }
        Candidate? result = RollCandidate(location, shore, bobber, depth, level, completedAttempts, random, 0, false);
        if (result is not { Rewardable: true }) return null;
        WorkerFishingCatchCatalog.RegisterLiveFish(result.ItemId, result.Experience);
        return new() { ItemId = result.ItemId, Experience = result.Experience, IsLiveDataCatch = true };
    }

    /// <summary>Prefer productive water over decorative/trash ponds; seasonal/time gates still apply per cast.</summary>
    public static bool HasFishPoolAt(GameLocation location, Point shore, Point bobber)
        => HasFishPoolAt(location, shore, bobber, 0, false);

    private static bool HasFishPoolAt(GameLocation location, Point shore, Point bobber, int nesting, bool inherited)
    {
        if (nesting >= 8) return false;
        // An optional map override may provide fish even when the farm's own pool is empty.
        if (!inherited && location.NameOrUniqueName == WorkerFishingAreaCatalog.Farm)
        {
            string[] fields = location.getMapProperty("FarmFishLocationOverride")?.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                ?? Array.Empty<string>();
            if (fields.Length >= 2 && float.TryParse(fields[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float chance)
                && chance > 0 && Game1.getLocationFromName(fields[0]) is { } target
                && HasFishPoolAt(target, shore, bobber, nesting + 1, true)) return true;
        }
        location.TryGetFishAreaForTile(bobber.ToVector2(), out string fishArea, out _);
        var all = DataLoader.Locations(Game1.content);
        var fish = DataLoader.Fish(Game1.content);
        IEnumerable<SpawnFishData> entries = all.TryGetValue("Default", out var defaults) ? defaults.Fish ?? new() : Enumerable.Empty<SpawnFishData>();
        entries = entries.Concat(location.GetData()?.Fish ?? new());
        foreach (var spawn in entries)
        {
            if (inherited && !spawn.CanBeInherited || spawn.RequireMagicBait || spawn.IsBossFish || spawn.CatchLimit >= 0
                || !string.IsNullOrEmpty(spawn.SetFlagOnCatch) || spawn.IsRecipe
                || spawn.FishAreaId is not null && spawn.FishAreaId != fishArea
                || spawn.PlayerPosition.HasValue && !spawn.PlayerPosition.Value.Contains(shore)
                || spawn.BobberPosition.HasValue && !spawn.BobberPosition.Value.Contains(bobber)) continue;
            IEnumerable<string> ids = spawn.RandomItemId is { Count: > 0 } choices ? choices : new[] { spawn.ItemId };
            foreach (string? id in ids)
            {
                if (string.IsNullOrWhiteSpace(id)) continue;
                if (id.StartsWith("LOCATION_FISH ", StringComparison.Ordinal))
                {
                    if (TryInheritedPool(id, bobber, 0, out var target, out Point targetBobber, out _)
                        && HasFishPoolAt(target!, shore, targetBobber, nesting + 1, true)) return true;
                }
                else
                {
                    string qualified = id.StartsWith('(') ? id : "(O)" + id;
                    if (qualified.StartsWith("(O)", StringComparison.Ordinal) && !WorkerFishingCatchCatalog.IsSpecialFish(qualified)
                        && ItemRegistry.GetData(qualified) is not null
                        && WorkerFishDataRequirements.TryParse(fish.GetValueOrDefault(qualified[3..]), out _)) return true;
                }
            }
        }
        return false;
    }

    private static bool TryInheritedPool(string id, Point bobber, int depth, out GameLocation? location,
        out Point targetBobber, out int targetDepth)
    {
        location = null; targetBobber = default; targetDepth = 0;
        string[] query = id.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (query.Length != 5) return false;
        int? Coordinate(string value, string token, int replacement)
            => value == token ? replacement : int.TryParse(value, out int parsed) ? parsed : null;
        int? x = Coordinate(query[2], "BOBBER_X", bobber.X), y = Coordinate(query[3], "BOBBER_Y", bobber.Y);
        int? parsedDepth = Coordinate(query[4], "WATER_DEPTH", depth);
        location = Game1.getLocationFromName(query[1]);
        if (location is null || x is null || y is null || parsedDepth is null) return false;
        targetBobber = new(x.Value, y.Value); targetDepth = parsedDepth.Value;
        return true;
    }

    private sealed record Candidate(string ItemId, int Experience, bool Rewardable);

    private static Candidate? RollCandidate(GameLocation location, Point shore, Point bobber, int depth,
        int level, int completedAttempts, Random random, int nesting, bool inherited)
    {
        // Bounds malformed/circular LOCATION_FISH chains without touching player/global RNG state.
        if (nesting >= 8) return null;
        // Read through the content cache each cast, respecting live Content Patcher changes.
        var locations = DataLoader.Locations(Game1.content);
        var fishData = DataLoader.Fish(Game1.content);
        IEnumerable<SpawnFishData> entries = locations.TryGetValue("Default", out var defaults)
            ? defaults.Fish ?? new() : Enumerable.Empty<SpawnFishData>();
        if (location.GetData()?.Fish is { } local) entries = entries.Concat(local);
        location.TryGetFishAreaForTile(bobber.ToVector2(), out string fishArea, out _);
        Farmer host = Game1.MasterPlayer;
        // Story flags belong to the host. Workers use their own level, no tackle/bait or player skill buffs.
        foreach (SpawnFishData spawn in entries.OrderBy(entry => entry.Precedence).ThenBy(_ => random.Next()))
        {
            if (inherited && !spawn.CanBeInherited || spawn.RequireMagicBait || spawn.MinFishingLevel > level
                || spawn.FishAreaId is not null && spawn.FishAreaId != fishArea
                || spawn.Season.HasValue && spawn.Season != location.GetSeason()
                || spawn.PlayerPosition.HasValue && !spawn.PlayerPosition.Value.Contains(shore)
                || spawn.BobberPosition.HasValue && !spawn.BobberPosition.Value.Contains(bobber)
                || depth < spawn.MinDistanceFromShore
                || spawn.MaxDistanceFromShore >= 0 && depth > spawn.MaxDistanceFromShore) continue;

            float chance = spawn.GetChance(false, host.team.sharedDailyLuck.Value, 0,
                (value, modifiers, mode) => Utility.ApplyQuantityModifiers(value, modifiers, mode,
                    location, host, random: random));
            // Seeded vanilla entries (e.g. jelly) advance with worker attempts, never the host's fish counter.
            Random spawnRandom = spawn.UseFishCaughtSeededRandom
                ? Utility.CreateRandom(Game1.uniqueIDForThisGame, completedAttempts * 859) : random;
            if (spawnRandom.NextDouble() >= chance
                || !GameStateQuery.CheckConditions(spawn.Condition, location, host, random: random)) continue;

            string? id = spawn.RandomItemId is { Count: > 0 } choices
                ? choices[random.Next(choices.Count)] : spawn.ItemId;
            if (string.IsNullOrWhiteSpace(id)) continue;
            bool inheritedRewardable = true;
            if (id.StartsWith("LOCATION_FISH ", StringComparison.Ordinal))
            {
                if (!TryInheritedPool(id, bobber, depth, out var target, out Point targetBobber, out int inheritedDepth)) continue;
                Candidate? nested = RollCandidate(target!, shore, targetBobber, inheritedDepth,
                    level, completedAttempts, random, nesting + 1, true);
                if (nested is null) continue;
                if (nested.ItemId.Length == 0) return nested;
                id = nested.ItemId;
                inheritedRewardable = nested.Rewardable;
                // Native LOCATION_FISH results also pass the parent spawn's generic bite requirements.
            }
            else if (id.Any(char.IsWhiteSpace)) return new("", 0, false);
            if (!id.StartsWith('(')) id = "(O)" + id;
            if (ItemRegistry.GetData(id) is null) continue;
            if (spawn.CatchLimit >= 0 && host.fishCaught.TryGetValue(id, out int[]? previous)
                && previous.Length > 0 && previous[0] >= spawn.CatchLimit) continue;
            string? raw = id.StartsWith("(O)", StringComparison.Ordinal) ? fishData.GetValueOrDefault(id[3..]) : null;
            bool rodFish = WorkerFishDataRequirements.TryParse(raw, out _);
            WorkerFishDataRequirements? requirements = null;
            if (rodFish) WorkerFishDataRequirements.TryParse(raw, out requirements!);
            if (raw is not null && !rodFish && raw.Split('/').ElementAtOrDefault(1) != "trap") continue;
            if (requirements is not null && !spawn.IgnoreFishDataRequirements)
            {
                if (!requirements.IsEligible(Game1.timeOfDay, location.IsRainingHere(), level)) continue;
                float bite = requirements.Chance(level, depth);
                if (spawn.ApplyDailyLuck) bite += (float)host.team.sharedDailyLuck.Value;
                bite = Utility.ApplyQuantityModifiers(bite, spawn.ChanceModifiers, spawn.ChanceModifierMode,
                    location, host, random: random);
                if (random.NextDouble() >= bite) continue;
            }
            // Preserve selected non-rewards across inherited pools; never reroll them into more fish.
            bool rewardable = inheritedRewardable && requirements is not null && !spawn.IsBossFish && spawn.CatchLimit < 0
                && string.IsNullOrEmpty(spawn.SetFlagOnCatch) && !spawn.IsRecipe
                && !WorkerFishingCatchCatalog.IsSpecialFish(id);
            return new(id, requirements?.Experience ?? 0, rewardable);
        }
        return null;
    }
}
