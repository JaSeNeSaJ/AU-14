using Content.Shared._CMU14.Medical.BodyPart.Events;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Melee;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Mobs.Systems;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Random;

namespace Content.Server._RMC14.Xenonids.Melee;

public sealed class XenoMeleeSeverSystem : EntitySystem
{
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private XenoSystem _xeno = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<XenoMeleeSeverComponent, MeleeHitEvent>(OnMeleeHit);
    }

    private void OnMeleeHit(Entity<XenoMeleeSeverComponent> xeno, ref MeleeHitEvent args)
    {
        if (!args.IsHit)
            return;

        foreach (var target in args.HitEntities)
        {
            if (!_xeno.CanAbilityAttackTarget(xeno, target))
                continue;

            if (_mobState.IsDead(target))
                continue;

            if (!TryComp<BodyComponent>(target, out _))
                continue;

            if (!_random.Prob(xeno.Comp.Chance))
                continue;

            TrySeverRandomLimb(target);
        }
    }

    private void TrySeverRandomLimb(EntityUid body)
    {
        var candidates = new List<(EntityUid Id, BodyPartComponent Part)>();

        foreach (var (partUid, part) in _body.GetBodyChildren(body))
        {
            if (part.PartType is BodyPartType.Arm or BodyPartType.Leg)
                candidates.Add((partUid, part));
        }

        if (candidates.Count == 0)
            return;

        var (severedPartUid, severedPart) = _random.Pick(candidates);
        var ev = new BodyPartSeveredEvent(body, severedPartUid, severedPart.PartType);
        RaiseLocalEvent(severedPartUid, ref ev);
    }
}
