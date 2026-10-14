using StardewModdingAPI;
using StardewValley;

namespace HireSkilledHelpers.Workers;

internal static class WorkerFishingStorage
{
    public static bool TryDeliver(WorkerFishingProgress progress, WorkerHarvestDestination? destination,
        IMonitor monitor, Action persist, out string error)
    {
        error = "";
        while (progress.PendingCatches.Count > 0)
        {
            WorkerFishingCatch caught = progress.PendingCatches[0];
            if (!WorkerFishingCatchCatalog.IsPersistable(caught))
            {
                progress.PendingCatches.RemoveAt(0);
                persist();
                continue;
            }
            Item? item = null;
            try
            {
                if (ItemRegistry.GetData(caught.ItemId) is null)
                {
                    error = $"Item {caught.ItemId} is unavailable; restore its content mod. The catch is preserved.";
                    return false;
                }
                item = ItemRegistry.Create(caught.ItemId);
                WorkerItemStorage.Store(item, destination, monitor);
                progress.PendingCatches.RemoveAt(0);
                persist();
            }
            catch (Exception ex)
            {
                if (item is not null && item.Stack <= 0) progress.PendingCatches.RemoveAt(0);
                persist();
                error = ex.Message;
                return false;
            }
        }
        return true;
    }
}
