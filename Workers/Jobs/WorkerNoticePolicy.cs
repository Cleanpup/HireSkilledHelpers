namespace HireSkilledHelpers.Workers;

/// <summary>Observe real transitions without load-time or repeated-tick notifications.</summary>
internal sealed class WorkerNoticePolicy
{
    private readonly Dictionary<string, WorkerTaskKind> previous = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, bool> idle = new(StringComparer.OrdinalIgnoreCase);
    public bool Observe(string id, WorkerTaskKind task)
    {
        if (!previous.TryGetValue(id, out var old))
        { previous[id] = task; idle[id] = task == WorkerTaskKind.Idle; return false; }
        if (old == task) return false;
        previous[id] = task;
        return ObserveRuntime(id, task == WorkerTaskKind.Idle);
    }
    public bool ObserveRuntime(string id, bool isIdle)
    {
        bool notify = idle.TryGetValue(id, out bool old) && !old && isIdle;
        idle[id] = isIdle;
        return notify;
    }
    public void Retain(IEnumerable<string> ids)
    {
        var live = new HashSet<string>(ids, StringComparer.OrdinalIgnoreCase);
        foreach (var key in previous.Keys.Where(key => !live.Contains(key)).ToArray()) { previous.Remove(key); idle.Remove(key); }
    }
    public void Clear() { previous.Clear(); idle.Clear(); }
}
