using System.Globalization;

namespace HireSkilledHelpers.Workers;

/// <summary>Rod-fish requirements from the live Data/Fish asset (the legacy season field is unused).</summary>
internal sealed record WorkerFishDataRequirements(int Difficulty, int[] Times, string Weather,
    int MaximumDepth, float BaseChance, float DepthPenalty, int MinimumLevel)
{
    public int Experience => Math.Max(1, 3 + Difficulty / 3);

    public static bool TryParse(string? raw, out WorkerFishDataRequirements result)
    {
        result = null!;
        string[] fields = raw?.Split('/') ?? Array.Empty<string>();
        if (fields.Length < 13 || !int.TryParse(fields[1], out int difficulty) || difficulty < 0
            || !int.TryParse(fields[9], out int depth) || depth < 0
            || !float.TryParse(fields[10], NumberStyles.Float, CultureInfo.InvariantCulture, out float chance)
            || !float.IsFinite(chance) || chance < 0
            || !float.TryParse(fields[11], NumberStyles.Float, CultureInfo.InvariantCulture, out float penalty)
            || !float.IsFinite(penalty) || penalty < 0
            || !int.TryParse(fields[12], out int level) || level < 0
            || fields[7] is not ("sunny" or "rainy" or "both")) return false;
        string[] times = fields[5].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (times.Length == 0 || times.Length % 2 != 0) return false;
        int[] parsed = new int[times.Length];
        for (int i = 0; i < times.Length; i++)
            if (!int.TryParse(times[i], out parsed[i])) return false;
        for (int i = 0; i < parsed.Length; i += 2)
            if (parsed[i] < 0 || parsed[i] >= parsed[i + 1]) return false;
        result = new(difficulty, parsed, fields[7], depth, chance, penalty, level);
        return true;
    }

    public bool IsEligible(int time, bool raining, int level)
    {
        if (level < MinimumLevel || Weather == "rainy" && !raining || Weather == "sunny" && raining) return false;
        for (int i = 0; i < Times.Length; i += 2)
            if (time >= Times[i] && time < Times[i + 1]) return true;
        return false;
    }

    public float Chance(int level, int depth)
        => Math.Min(.9f, BaseChance - Math.Max(0, MaximumDepth - depth) * DepthPenalty * BaseChance
            + Math.Clamp(level, 0, 10) / 50f);
}
