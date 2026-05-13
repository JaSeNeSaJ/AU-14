using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._AU14.Abominations.Abilities;

/// <summary>
/// Instant ability: spawn a flesh wall on the abomination's current tile.
/// No resources; cooldown is enforced by the action's useDelay.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AbominationSecreteWallComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntProtoId Wall = "AU14AbominationFleshWall";
}

public sealed partial class AbominationSecreteWallActionEvent : InstantActionEvent;
