using Robust.Shared.GameObjects;

namespace Content.Shared.AU14;

[RegisterComponent]
public sealed partial class DestroyOnPresetComponent : Component
{
    [DataField("preset")]
    public string Preset { get; set; }
}
