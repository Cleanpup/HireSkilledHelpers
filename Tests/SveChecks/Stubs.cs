using Microsoft.Xna.Framework;
using StardewValley.GameData;
using StardewValley.GameData.Locations;
namespace StardewModdingAPI
{
    public interface IModRegistry { bool IsLoaded(string id); }
    public interface IMonitor { }
    public static class Context { public static bool IsWorldReady = true; }
}
namespace StardewValley
{
    public sealed class NetValue<T> { public T Value = default!; }
    public sealed class Team { public NetValue<double> sharedDailyLuck = new(); }
    public sealed class Farmer
    {
        public Team team = new();
        public HashSet<string> Events = new();
        public Dictionary<string, int[]> fishCaught = new();
        public HashSet<string> Mail = new();
        public bool hasOrWillReceiveMail(string id) => Mail.Contains(id);
    }
    public static class Game1
    {
        public static object content = new();
        public static Farmer MasterPlayer = new();
        public static int timeOfDay = 1200;
        public static ulong uniqueIDForThisGame = 123;
        public static Dictionary<string, GameLocation> Locations = new();
        public static GameLocation? getLocationFromName(string name) => Locations.GetValueOrDefault(name);
    }
    public sealed class GameLocation
    {
        public bool IsOutdoors = true;
        public string NameOrUniqueName = "Test";
        public Dictionary<string, string> Properties = new();
        public string? getMapProperty(string name) => Properties.GetValueOrDefault(name);
        public LocationData Data = new() { Fish = new() };
        public Season Season = Season.Spring;
        public bool Rain;
        public string Area = "River";
        public int Depth = 3;
        public bool UseDataAreas;
        public LocationData GetData() => Data;
        public Season GetSeason() => Season;
        public bool IsRainingHere() => Rain;
        public bool TryGetFishAreaForTile(Vector2 tile, out string id, out object? ignored)
        {
            ignored=null; id=Area;
            if(!UseDataAreas)return true;
            string? fallback=null;
            foreach(var entry in Data.FishAreas ?? new())
            {
                if(entry.Value.Position?.Contains((int)tile.X,(int)tile.Y)==true){id=entry.Key;return true;}
                if(entry.Value.Position is null)fallback??=entry.Key;
            }
            id=fallback!;return fallback is not null;
        }
    }
    public static class DataLoader
    {
        public static Dictionary<string, LocationData> LocationAsset = new();
        public static Dictionary<string, string> FishAsset = new();
        public static Dictionary<string, LocationData> Locations(object _) => LocationAsset;
        public static Dictionary<string, string> Fish(object _) => FishAsset;
    }
    public static class GameStateQuery
    {
        // Fixture evaluator, not a replacement for the native GSQ engine used by the mod.
        public static bool CheckConditions(string? condition, GameLocation location, Farmer player, Random? random = null)
        {
            if (condition is null) return true;
            foreach (string part in condition.Split(','))
            {
                bool negated=part.Trim().StartsWith('!');
                string[] tokens = part.Trim().TrimStart('!').Split(' ');
                bool result = tokens[0].ToUpperInvariant() switch
                {
                    "LOCATION_SEASON" => tokens.Skip(2).Contains(location.Season.ToString(),StringComparer.OrdinalIgnoreCase),
                    "PLAYER_HAS_MAIL" => player.Mail.Contains(tokens[2]),
                    "PLAYER_SPECIAL_ORDER_RULE_ACTIVE" or "IS_FESTIVAL_DAY" or "IS_PASSIVE_FESTIVAL_OPEN" => false,
                    "WEATHER" => location.Rain == tokens.Skip(2).Contains("Rain"),
                    "TIME" => Game1.timeOfDay >= int.Parse(tokens[1]) && Game1.timeOfDay < int.Parse(tokens[2]),
                    "PLAYER_HAS_SEEN_EVENT" => player.Events.Contains(tokens[2]),
                    _ => throw new Exception("Unmodeled fixture condition: " + part),
                };
                if (negated) result=!result;
                if (!result) return false;
            }
            return true;
        }
    }
    public static class Utility
    {
        public static Random CreateRandom(ulong save, int attempts) => new(unchecked((int)save + attempts));
        public static float ApplyQuantityModifiers(float value, IList<QuantityModifier>? modifiers,
            QuantityModifier.QuantityModifierMode mode, GameLocation location, Farmer player, Random? random = null)
        {
            if (modifiers?.Count > 0) throw new Exception("Modifier fixture not configured");
            return value;
        }
    }
    public sealed class Item { public string Id = ""; public int Stack = 1; }
    public static class ItemRegistry
    {
        public static HashSet<string> Known = new();
        public static object? GetData(string id) => Known.Contains(id) ? new object() : null;
        public static Item Create(string id) => new() { Id = id };
    }
}
namespace StardewValley.Tools
{
    public static class FishingRod { public static int distanceToLand(int x, int y, StardewValley.GameLocation location) => location.Depth; }
}
namespace HireSkilledHelpers.Workers
{
    internal sealed class WorkerHarvestDestination { }
    internal static class WorkerItemStorage
    {
        public static List<StardewValley.Item> Delivered = new();
        public static void Store(StardewValley.Item item, WorkerHarvestDestination? destination, StardewModdingAPI.IMonitor monitor) => Delivered.Add(item);
    }
}
