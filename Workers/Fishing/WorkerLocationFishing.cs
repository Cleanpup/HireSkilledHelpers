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
        Candidate? result = Candidates(location, shore, bobber, depth, level, completedAttempts, random, 0, false, false).FirstOrDefault();
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

    /// <summary>Exhaustively selects the highest base-price fish permitted by this cast's live gates.</summary>
    public static WorkerFishingCatch? BestCatch(GameLocation location, Point shore, Point bobber, int level,
        int completedAttempts, Random random, Func<string, int> price)
    {
        int depth = FishingRod.distanceToLand(bobber.X, bobber.Y, location);
        IEnumerable<Candidate> pool = Candidates(location, shore, bobber, depth, level, completedAttempts, random, 0, false, true);
        if (location.NameOrUniqueName == WorkerFishingAreaCatalog.Farm)
        {
            string[] fields = location.getMapProperty("FarmFishLocationOverride")?.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                ?? Array.Empty<string>();
            if (fields.Length >= 2 && float.TryParse(fields[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float chance)
                && chance > 0 && Game1.getLocationFromName(fields[0]) is { } target)
            {
                var alternate = Candidates(target, shore, bobber, depth, level, completedAttempts, random, 0, false, true);
                pool = chance >= 1 ? alternate : pool.Concat(alternate);
            }
        }
        Candidate? best = pool.Where(item => item.Rewardable).OrderByDescending(item => price(item.ItemId))
            .ThenBy(item => item.ItemId, StringComparer.Ordinal).FirstOrDefault();
        if (best is null) return null;
        WorkerFishingCatchCatalog.RegisterLiveFish(best.ItemId, best.Experience);
        return new() { ItemId = best.ItemId, Experience = best.Experience, IsLiveDataCatch = true };
    }

    private static IEnumerable<Candidate> Candidates(GameLocation location, Point shore, Point bobber, int depth,
        int level, int completedAttempts, Random random, int nesting, bool inherited, bool guaranteed)
    {
        if (nesting >= 8) yield break;
        var locations = DataLoader.Locations(Game1.content);
        var fishData = DataLoader.Fish(Game1.content);
        IEnumerable<SpawnFishData> entries = locations.TryGetValue("Default", out var defaults)
            ? defaults.Fish ?? new() : Enumerable.Empty<SpawnFishData>();
        if (location.GetData()?.Fish is { } local) entries = entries.Concat(local);
        location.TryGetFishAreaForTile(bobber.ToVector2(), out string fishArea, out _);
        Farmer host = Game1.MasterPlayer;
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
            Random spawnRandom = spawn.UseFishCaughtSeededRandom
                ? Utility.CreateRandom(Game1.uniqueIDForThisGame, completedAttempts * 859) : random;
            if ((guaranteed ? chance <= 0 : spawnRandom.NextDouble() >= chance)
                || !GameStateQuery.CheckConditions(spawn.Condition, location, host, random: random)) continue;
            IEnumerable<string> ids = spawn.RandomItemId is { Count: > 0 } choices
                ? guaranteed ? choices : new[] { choices[random.Next(choices.Count)] } : new[] { spawn.ItemId };
            foreach (string? rawId in ids)
            {
                if (string.IsNullOrWhiteSpace(rawId)) continue;
                IEnumerable<Candidate> candidates;
                if (rawId.StartsWith("LOCATION_FISH ", StringComparison.Ordinal))
                {
                    if (!TryInheritedPool(rawId, bobber, depth, out var target, out Point targetBobber, out int inheritedDepth)) continue;
                    candidates = Candidates(target!, shore, targetBobber, inheritedDepth,
                        level, completedAttempts, random, nesting + 1, true, guaranteed);
                    if (!guaranteed) candidates = candidates.Take(1);
                }
                else candidates = new[] { new Candidate(rawId.Any(char.IsWhiteSpace) ? "" : rawId, 0, true) };
                foreach (Candidate candidate in candidates)
                {
                    if (candidate.ItemId.Length == 0)
                    { if (!guaranteed) { yield return candidate; yield break; } continue; }
                    string id = candidate.ItemId.StartsWith('(') ? candidate.ItemId : "(O)" + candidate.ItemId;
                    if (ItemRegistry.GetData(id) is null) continue;
                    if (spawn.CatchLimit >= 0 && host.fishCaught.TryGetValue(id, out int[]? previous)
                        && previous.Length > 0 && previous[0] >= spawn.CatchLimit) continue;
                    string? raw = id.StartsWith("(O)", StringComparison.Ordinal) ? fishData.GetValueOrDefault(id[3..]) : null;
                    bool rodFish = WorkerFishDataRequirements.TryParse(raw, out var requirements);
                    if (raw is not null && !rodFish && raw.Split('/').ElementAtOrDefault(1) != "trap") continue;
                    if (rodFish && !spawn.IgnoreFishDataRequirements)
                    {
                        if (!requirements.IsEligible(Game1.timeOfDay, location.IsRainingHere(), level)) continue;
                        float bite = requirements.Chance(level, depth);
                        if (spawn.ApplyDailyLuck) bite += (float)host.team.sharedDailyLuck.Value;
                        bite = Utility.ApplyQuantityModifiers(bite, spawn.ChanceModifiers, spawn.ChanceModifierMode,
                            location, host, random: random);
                        if (guaranteed ? bite <= 0 : random.NextDouble() >= bite) continue;
                    }
                    bool rewardable = candidate.Rewardable && rodFish && !spawn.IsBossFish && spawn.CatchLimit < 0
                        && string.IsNullOrEmpty(spawn.SetFlagOnCatch) && !spawn.IsRecipe
                        && !WorkerFishingCatchCatalog.IsSpecialFish(id);
                    if (!guaranteed || rewardable) yield return new(id, rodFish ? requirements.Experience : 0, rewardable);
                    if (!guaranteed) yield break;
                }
            }
        }
    }
}
