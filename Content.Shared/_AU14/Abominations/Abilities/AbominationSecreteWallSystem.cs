using Content.Shared.Coordinates;
using Robust.Shared.Network;

namespace Content.Shared._AU14.Abominations.Abilities;

public sealed class AbominationSecreteWallSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<AbominationSecreteWallComponent, AbominationSecreteWallActionEvent>(OnSecreteAction);
    }

    private void OnSecreteAction(Entity<AbominationSecreteWallComponent> ent, ref AbominationSecreteWallActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (_net.IsClient)
            return;

        Spawn(ent.Comp.Wall, ent.Owner.ToCoordinates());
    }
}
