namespace HireSkilledHelpers.Workers;

internal sealed class WorkerFishingCatch
{
    public string ItemId { get; set; } = "";
    public int Experience { get; set; }
    // Retain validated custom-farm fish across reloads/removal of their content pack.
    public bool IsLiveDataCatch { get; set; }
    public WorkerFishingCatch Clone() => new() { ItemId = this.ItemId, Experience = this.Experience, IsLiveDataCatch = this.IsLiveDataCatch };
}

/// <summary>Simulation time and completed, undelivered catches; never a live fishing rod.</summary>
internal sealed class WorkerFishingProgress
{
    public int Day { get; set; } = -1;
    public int Minutes { get; set; }
    public int LastObservedMinute { get; set; } = -1;
    public int CompletedAttempts { get; set; }
    public int CompletedCatches { get; set; }
    public bool CastStarted { get; set; }
    public List<WorkerFishingCatch> PendingCatches { get; set; } = new();
    public WorkerFishingProgress Clone() => new()
    {
        Day = this.Day, Minutes = this.Minutes, LastObservedMinute = this.LastObservedMinute,
        CompletedCatches = this.CompletedCatches, CompletedAttempts = this.CompletedAttempts,
        CastStarted = this.CastStarted,
        PendingCatches = (this.PendingCatches ?? new()).Where(item => item is not null).Select(item => item.Clone()).ToList(),
    };
}
