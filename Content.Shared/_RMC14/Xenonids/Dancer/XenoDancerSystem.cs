using Content.Shared._RMC14.Actions;
using Content.Shared._RMC14.Xenonids.Dodge;
using Content.Shared._RMC14.Xenonids.Impale;
using Content.Shared._RMC14.Xenonids.Projectile;
using Content.Shared._RMC14.Xenonids.Rest;
using Content.Shared._RMC14.Xenonids.TailTrip;
using Content.Shared.Actions;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.Xenonids.Dancer;

public sealed partial class XenoDancerSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private EntityLookupSystem _entityLookup = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedRMCActionsSystem _rmcActions = default!;
    [Dependency] private XenoSystem _xeno = default!;

    private readonly HashSet<EntityUid> _nearbyTargets = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<XenoDancerReworkComponent, ProjectileReflectAttemptEvent>(OnProjectileReflectAttempt);
        SubscribeLocalEvent<XenoDancerReworkComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<XenoDancerReworkComponent, XenoImpaleActionEvent>(OnYellowImpale, after: new[] { typeof(XenoImpaleSystem) });
        SubscribeLocalEvent<XenoDancerReworkComponent, XenoRestEvent>(OnRest);
        SubscribeLocalEvent<XenoDancerReworkComponent, XenoTailTripActionEvent>(OnYellowTailTrip, after: new[] { typeof(XenoTailTripSystem) });
    }

    private void OnProjectileReflectAttempt(Entity<XenoDancerReworkComponent> xeno, ref ProjectileReflectAttemptEvent args)
    {
        if (args.Cancelled || HasComp<XenoProjectileComponent>(args.ProjUid))
            return;

        if (args.Component.Shooter is { } shooter && !_xeno.CanAbilityAttackTarget(xeno, shooter))
            return;

        var threshold = HasComp<XenoActiveDodgeComponent>(xeno)
            ? xeno.Comp.ActiveProjectileDodgeEvery
            : xeno.Comp.PassiveProjectileDodgeEvery;

        xeno.Comp.ProjectileHitsSeen++;
        Dirty(xeno);

        if (xeno.Comp.ProjectileHitsSeen < threshold)
            return;

        xeno.Comp.ProjectileHitsSeen = 0;
        Dirty(xeno);
        args.Cancelled = true;
        QueueDel(args.ProjUid);
        _popup.PopupClient(Loc.GetString("cm-xeno-dancer-projectile-dodge"), xeno, xeno);
    }

    private void OnMeleeHit(Entity<XenoDancerReworkComponent> xeno, ref MeleeHitEvent args)
    {
        if (!args.IsHit)
            return;

        foreach (var hit in args.HitEntities)
        {
            if (!_xeno.CanAbilityAttackTarget(xeno, hit))
                continue;

            if (HasComp<XenoYellowMarkedComponent>(hit))
                ResetTailCooldowns(xeno);

            if (_timing.CurTime >= xeno.Comp.NextYellowSpreadAt && _mobState.IsCritical(hit))
                SpreadYellowMark(xeno, hit);
        }
    }

    private void OnRest(Entity<XenoDancerReworkComponent> xeno, ref XenoRestEvent args)
    {
        if (!args.Resting)
            return;

        xeno.Comp.ProjectileHitsSeen = 0;
        Dirty(xeno);
    }

    private void OnYellowImpale(Entity<XenoDancerReworkComponent> xeno, ref XenoImpaleActionEvent args)
    {
        if (args.Entity is { } target)
            ConsumeYellowTailMark(xeno, target, args.Handled);
    }

    private void OnYellowTailTrip(Entity<XenoDancerReworkComponent> xeno, ref XenoTailTripActionEvent args)
    {
        ConsumeYellowTailMark(xeno, args.Target, args.Handled);
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var time = _timing.CurTime;
        var query = EntityQueryEnumerator<XenoYellowMarkedComponent>();
        while (query.MoveNext(out var uid, out var mark))
        {
            if (time >= mark.ExpiresAt)
                RemCompDeferred<XenoYellowMarkedComponent>(uid);
        }
    }

    private void ResetTailCooldowns(EntityUid xeno)
    {
        foreach (var action in _rmcActions.GetActionsWithEvent<XenoImpaleActionEvent>(xeno))
            _actions.SetCooldown(action.AsNullable(), TimeSpan.Zero);

        foreach (var action in _rmcActions.GetActionsWithEvent<XenoTailTripActionEvent>(xeno))
            _actions.SetCooldown(action.AsNullable(), TimeSpan.Zero);
    }

    private void SpreadYellowMark(Entity<XenoDancerReworkComponent> dancer, EntityUid center)
    {
        dancer.Comp.NextYellowSpreadAt = _timing.CurTime + dancer.Comp.YellowSpreadCooldown;
        Dirty(dancer);

        _nearbyTargets.Clear();
        _entityLookup.GetEntitiesInRange(Transform(center).Coordinates, dancer.Comp.YellowSpreadRange, _nearbyTargets);

        var marked = 0;
        foreach (var target in _nearbyTargets)
        {
            if (target == dancer.Owner ||
                target == center ||
                HasComp<XenoYellowMarkedComponent>(target) ||
                !_xeno.CanAbilityAttackTarget(dancer, target) ||
                !_mobState.IsAlive(target))
            {
                continue;
            }

            var mark = EnsureComp<XenoYellowMarkedComponent>(target);
            mark.ExpiresAt = _timing.CurTime + dancer.Comp.YellowDuration;
            Dirty(target, mark);

            _popup.PopupEntity(Loc.GetString("cm-xeno-dancer-yellow-spread-target"), target, target, PopupType.MediumCaution);
            marked++;
            if (marked >= dancer.Comp.YellowSpreadMaxTargets)
                break;
        }

        if (marked > 0)
            _popup.PopupClient(Loc.GetString("cm-xeno-dancer-yellow-spread-self"), dancer, dancer);
    }

    private void ConsumeYellowTailMark(EntityUid dancer, EntityUid target, bool actionHandled)
    {
        if (!actionHandled || !HasComp<XenoYellowMarkedComponent>(target))
            return;

        RemCompDeferred<XenoYellowMarkedComponent>(target);
        ResetTailCooldowns(dancer);
    }
}
