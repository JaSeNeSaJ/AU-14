using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Weapons.Ranged.Vulture;

[RegisterComponent]
public sealed partial class VultureSpotterTripodComponent : Component
{
    [DataField]
    public string ScopeSlot = "vulture_spotter_scope";
}

[Serializable, NetSerializable]
public enum VultureSpotterTripodVisuals : byte
{
    HasScope,
}

[Serializable, NetSerializable]
public enum VultureSpotterTripodVisualLayers : byte
{
    Base,
}
