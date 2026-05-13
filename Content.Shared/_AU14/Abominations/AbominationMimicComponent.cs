using Robust.Shared.GameStates;

namespace Content.Shared._AU14.Abominations;

/// <summary>
/// Lives on every mimic abomination. Holds the pool of assimilated identities it can
/// transform into and the parameters for the transform.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AbominationMimicComponent : Component
{
    [DataField, AutoNetworkedField]
    public List<AbominationAssimilationProfile> AssimilatedPool = new();

    [DataField, AutoNetworkedField]
    public TimeSpan TransformDuration = TimeSpan.FromSeconds(270);

    /// <summary>
    /// Cooldown between transforms. Starts when a disguise ends; the mimic must
    /// wait this long before opening the picker again.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan TransformCooldown = TimeSpan.FromSeconds(300);

    [DataField, AutoNetworkedField]
    public TimeSpan? NextTransformAt;
}
