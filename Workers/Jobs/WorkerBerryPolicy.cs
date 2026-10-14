namespace HireSkilledHelpers.Workers;

internal static class WorkerBerryPolicy
{
    public static bool CanHarvest(int size, bool townBush, bool inPot, bool ready, bool inBloom, string? itemId)
        => size == 1 && !townBush && !inPot && ready && inBloom && itemId is "(O)296" or "(O)410";
    public static int Yield(int foragingLevel) => 1 + Math.Clamp(foragingLevel, 0, 10) / 4;
    // Vanilla repair covers bridge columns 58–61; the eastern tidal-pool land starts at 62.
    public static bool BehindBeachBridge(bool repaired, int x, int y) => !repaired && x >= 62 && y is >= 0 and <= 25;
}
