using Content.Shared._RMC14.Stun;
using Content.Shared.DoAfter;
using Content.Shared.IdentityManagement;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Verbs;
using Robust.Shared.Network;

namespace Content.Shared._RMC14.Xenonids.Charge.ChargerJockey;

public sealed partial class XenoChargerJockeySystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private MobStateSystem _mobState = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<XenoChargerJockeyComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<XenoChargerJockeyComponent, XenoJockeyDoAfterEvent>(OnDoAfter);

        SubscribeLocalEvent<XenoChargerRidingComponent, MoveInputEvent>(OnRiderMoveInput);
        SubscribeLocalEvent<XenoChargerRidingComponent, ComponentShutdown>(OnRiderShutdown);

        SubscribeLocalEvent<XenoChargerJockeyComponent, ComponentShutdown>(OnChargerShutdown);
        SubscribeLocalEvent<XenoChargerJockeyComponent, MobStateChangedEvent>(OnChargerStateChanged);

        SubscribeLocalEvent<XenoChargerRidingComponent, StunnedEvent>(OnRiderStunned);
        SubscribeLocalEvent<XenoChargerJockeyComponent, StunnedEvent>(OnChargerStunned);
    }

    private void OnGetVerbs(Entity<XenoChargerJockeyComponent> charger, ref GetVerbsEvent<AlternativeVerb> args)
    {
        var user = args.User;
        if (!args.CanAccess || !args.CanInteract || !CanMount(user, charger))
            return;

        var verb = new AlternativeVerb
        {
            Text = Loc.GetString("rmc-xeno-jockey-verb"),
            Priority = 1,
            Act = () =>
            {
                var ev = new XenoJockeyDoAfterEvent();
                var doAfter = new DoAfterArgs(EntityManager, user, charger.Comp.MountDoAfter, ev, charger.Owner, charger.Owner)
                {
                    BreakOnMove = true,
                    BreakOnDamage = false,
                    DuplicateCondition = DuplicateConditions.SameTarget | DuplicateConditions.SameEvent,
                    NeedHand = false,
                };

                if (!_doAfter.TryStartDoAfter(doAfter))
                    return;

                var chargerName = Identity.Entity(charger.Owner, EntityManager);
                var riderName = Identity.Entity(user, EntityManager);
                var selfMessage = Loc.GetString("rmc-xeno-jockey-start-self", ("charger", chargerName));
                var othersMessage = Loc.GetString("rmc-xeno-jockey-start-others", ("rider", riderName), ("charger", chargerName));
                _popup.PopupPredicted(selfMessage, othersMessage, user, user);
            }
        };

        args.Verbs.Add(verb);
    }

    private bool CanMount(EntityUid rider, Entity<XenoChargerJockeyComponent> charger)
    {
        if (rider == charger.Owner)
            return false;

        if (!TryComp(rider, out RMCSizeComponent? userSize) ||
            userSize.Size is not (RMCSizes.VerySmallXeno or RMCSizes.SmallXeno))
        {
            return false;
        }

        if (HasComp<XenoChargerRidingComponent>(rider))
            return false;

        if (charger.Comp.Riders.Count >= charger.Comp.MaxRiders)
            return false;

        if (_mobState.IsDead(charger) || _mobState.IsDead(rider))
            return false;

        return true;
    }

    private void OnDoAfter(Entity<XenoChargerJockeyComponent> charger, ref XenoJockeyDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        var rider = args.User;

        args.Handled = true;

        if (!CanMount(rider, charger))
            return;

        Mount(rider, charger.Owner, charger.Comp);
    }

    private void Mount(EntityUid rider, EntityUid charger, XenoChargerJockeyComponent comp)
    {
        if (!_net.IsServer)
            return;

        var riding = EnsureComp<XenoChargerRidingComponent>(rider);
        riding.Charger = charger;
        Dirty(rider, riding);

        comp.Riders.Add(rider);
        Dirty(charger, comp);

        // Parent the rider to the charger so they move together.
        _transform.SetParent(rider, charger);

        if (_net.IsServer)
        {
            var chargerName = Identity.Entity(charger, EntityManager);
            var riderName = Identity.Entity(rider, EntityManager);
            _popup.PopupEntity(Loc.GetString("rmc-xeno-jockey-mount", ("rider", riderName), ("charger", chargerName)), rider, PopupType.Small);
        }
    }

    private void Dismount(EntityUid rider, EntityUid charger)
    {
        if (!_net.IsServer)
            return;

        if (TryComp(charger, out XenoChargerJockeyComponent? comp))
        {
            comp.Riders.Remove(rider);
            Dirty(charger, comp);
        }

        RemComp<XenoChargerRidingComponent>(rider);

        // Unparent — drop rider at current world position.
        _transform.AttachToGridOrMap(rider);
    }

    private void OnRiderMoveInput(Entity<XenoChargerRidingComponent> rider, ref MoveInputEvent args)
    {
        // Any directional input dismounts.
        if ((args.Entity.Comp.HeldMoveButtons & MoveButtons.AnyDirection) == 0)
            return;

        Dismount(rider.Owner, rider.Comp.Charger);
    }

    private void OnRiderShutdown(Entity<XenoChargerRidingComponent> rider, ref ComponentShutdown args)
    {
        // Clean up charger side if rider component is removed for any reason.
        if (TryComp(rider.Comp.Charger, out XenoChargerJockeyComponent? comp))
        {
            comp.Riders.Remove(rider.Owner);
            Dirty(rider.Comp.Charger, comp);
        }

        if (_net.IsServer)
            _transform.AttachToGridOrMap(rider.Owner);
    }

    private void OnChargerShutdown(Entity<XenoChargerJockeyComponent> charger, ref ComponentShutdown args)
    {
        // Dismount all riders if charger is deleted.
        foreach (var rider in charger.Comp.Riders)
        {
            if (TerminatingOrDeleted(rider))
                continue;

            RemComp<XenoChargerRidingComponent>(rider);
        }
    }

    private void OnChargerStateChanged(Entity<XenoChargerJockeyComponent> charger, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        foreach (var rider in charger.Comp.Riders)
        {
            if (TerminatingOrDeleted(rider))
                continue;

            Dismount(rider, charger.Owner);
        }
    }

    private void OnRiderStunned(Entity<XenoChargerRidingComponent> rider, ref StunnedEvent args)
    {
        Dismount(rider.Owner, rider.Comp.Charger);
    }

    private void OnChargerStunned(Entity<XenoChargerJockeyComponent> charger, ref StunnedEvent args)
    {
        foreach (var rider in charger.Comp.Riders)
        {
            if (TerminatingOrDeleted(rider))
                continue;

            Dismount(rider, charger.Owner);
        }
    }
}
