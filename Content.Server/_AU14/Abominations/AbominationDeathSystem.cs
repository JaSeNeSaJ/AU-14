using Content.Shared._AU14.Abominations;
using Content.Shared.Body.Systems;
using Content.Shared.Coordinates;
using Content.Shared.Mobs;
using Robust.Shared.Prototypes;

namespace Content.Server._AU14.Abominations;

/// <summary>
/// When any abomination dies, gib them and seed a patch of flesh kudzu at
/// their feet.
/// </summary>
public sealed class AbominationDeathSystem : EntitySystem
{
    public static readonly EntProtoId FleshKudzuSource = "AU14AbominationFleshKudzuSource";

    [Dependency] private readonly SharedBodySystem _body = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<AbominationComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnMobStateChanged(Entity<AbominationComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        Spawn(FleshKudzuSource, ent.Owner.ToCoordinates());
        _body.GibBody(ent.Owner);
    }
}
