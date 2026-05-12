using Content.Shared.Coordinates;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared._AU14.Abominations;

public sealed class AbominationKudzuSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;

    public static readonly EntProtoId KudzuSource = "CMUXenoKudzuSource";

    public override void Initialize()
    {
        SubscribeLocalEvent<AbominationComponent, AbominationPlantKudzuActionEvent>(OnPlantKudzu);
    }

    private void OnPlantKudzu(Entity<AbominationComponent> ent, ref AbominationPlantKudzuActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (_net.IsClient)
            return;

        Spawn(KudzuSource, ent.Owner.ToCoordinates());
    }
}
