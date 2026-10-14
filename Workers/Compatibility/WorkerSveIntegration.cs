using StardewModdingAPI;
using StardewValley;

namespace HireSkilledHelpers.Workers;

/// <summary>Optional destinations; no SVE assembly reference or bundled SVE content.</summary>
internal static class WorkerSveIntegration
{
    public const string ModId = "FlashShifter.StardewValleyExpandedCP";
    public const string ForestWest = "Custom_ForestWest", ShearwaterBridge = "Custom_ShearwaterBridge";
    public static bool Enabled { get; private set; }
    public static readonly WorkerForageArea[] Areas =
    {
        new(ForestWest, "Forest West"), new(ShearwaterBridge, "Shearwater Bridge"),
    };

    public static void Initialize(IModRegistry registry) => Enabled = registry.IsLoaded(ModId);
    public static bool IsKnownArea(string? name) => Areas.Any(area => area.LocationName == name);
    public static bool IsAvailable(string name) => Enabled && Context.IsWorldReady
        && Game1.getLocationFromName(name) is { IsOutdoors: true };
    public static IEnumerable<WorkerForageArea> AvailableAreas => Areas.Where(area => IsAvailable(area.LocationName));
}
