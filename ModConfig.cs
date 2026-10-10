using StardewModdingAPI;

namespace HireSkilledHelpers;

internal sealed class ModConfig
{
    public ModConfig() { }

    public SButton OpenBusinessMenuKey { get; set; } = SButton.B;

    public bool BypassWorkerUnlockEvent { get; set; }
}
