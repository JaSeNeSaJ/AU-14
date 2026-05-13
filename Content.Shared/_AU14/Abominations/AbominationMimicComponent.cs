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
    public TimeSpan TransformDuration = TimeSpan.FromSeconds(360);

    /// <summary>
    /// Probability that a successful melee hit on a humanoid infects them with
    /// AbominationInfectionComponent. 0 = never, 1 = every hit.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float InfectionChance = 0.25f;
}
