using Robust.Shared.GameStates;

namespace Content.Shared._AU14.Abominations;

/// <summary>
/// Marker on every abomination mob. Also carries the infection chance that
/// every melee hit on a humanoid rolls against.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AbominationComponent : Component
{
    /// <summary>
    /// Probability that a successful melee hit on a humanoid applies
    /// <see cref="AbominationInfectionComponent"/>. 0 = never, 1 = every hit.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float InfectionChance = 0.2f;
}
