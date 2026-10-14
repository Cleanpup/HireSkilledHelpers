using Microsoft.Xna.Framework;
using StardewValley;

namespace HireSkilledHelpers.Workers;

internal static class WorkerFishingAreaCatalog
{
    public const string Farm = "Farm", Forest = "Forest", Mountain = "Mountain", Town = "Town", Beach = "Beach", Island = "IslandSouth";
    private static readonly string[] VanillaAreas = { Forest, Mountain, Town, Beach, Island, Farm };
    public static IReadOnlyList<string> Areas => VanillaAreas.Concat(WorkerSveIntegration.AvailableAreas.Select(area => area.LocationName)).ToArray();
    public static bool IsKnown(string area) => VanillaAreas.Contains(area) || WorkerSveIntegration.IsKnownArea(area);
    public static bool IsValid(string area) => Areas.Contains(area);
    public static string Label(string area) => area switch
    {
        Farm => "Your farm", Forest => "Cindersap Forest", Mountain => "Mountain", Town => "Town", Beach => "Beach",
        Island => "Ginger Island beach",
        WorkerSveIntegration.ForestWest => "Forest West", WorkerSveIntegration.ShearwaterBridge => "Shearwater Bridge",
        _ => "Unknown area",
    };
    public static Point Anchor(string area) => area switch
    {
        Forest => new(66, 50), Mountain => new(49, 20), Town => new(69, 81), Beach => new(35, 36),
        Island => new(19, 35), _ => Point.Zero,
    };
    public static string? AccessReason(string area) => WorkerSveIntegration.IsKnownArea(area) && !WorkerSveIntegration.IsAvailable(area)
        ? "This area requires SVE and its map to be available"
        : !IsValid(area) ? "Unknown fishing area"
        : area == Island && !Game1.MasterPlayer.hasOrWillReceiveMail("willyBoatFixed")
            ? "Repair Willy's boat before fishing on Ginger Island" : null;
}
