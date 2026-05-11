// Content.Shared/_RMC14/Xenonids/Charge/CursorCharge/XenoChargerLungeSystem.cs

using System.Numerics;
using Content.Shared._RMC14.Entrenching;
using Content.Shared._RMC14.Stun;
using Content.Shared._RMC14.Xenonids.Charge.CursorCharge;
using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.Xenonids.ChargerLunge;

public sealed class XenoChargerLungeSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly XenoSystem _xeno = default!;
    [Dependency] private readonly RMCSizeStunSystem _sizeStun = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;

    private readonly ProtoId<DamageTypePrototype> _blunt = "Blunt";

    // Deferred collision buffer — same pattern as XenoCursorSteeringSystem._cursorHit.
    private readonly HashSet<(EntityUid Lunger, EntityUid Target)> _lungeHit = new();

    private EntityQuery<PhysicsComponent> _physicsQuery;
    private EntityQuery<XenoChargerLungeComponent> _lungeQuery;
    private EntityQuery<ActiveXenoChargerLungeComponent> _activeQuery;
    private EntityQuery<XenoCursorSteeringComponent> _steeringQuery;

    public override void Initialize()
    {
        _physicsQuery = GetEntityQuery<PhysicsComponent>();
        _lungeQuery = GetEntityQuery<XenoChargerLungeComponent>();
        _activeQuery = GetEntityQuery<ActiveXenoChargerLungeComponent>();
        _steeringQuery = GetEntityQuery<XenoCursorSteeringComponent>();

        SubscribeLocalEvent<XenoChargerLungeComponent, XenoChargerLungeActionEvent>(OnLungeAction);
        SubscribeLocalEvent<ActiveXenoChargerLungeComponent, StartCollideEvent>(OnLungeCollide);
    }

    // -------------------------------------------------------------------------
    // Action handler
    // -------------------------------------------------------------------------

    private void OnLungeAction(Entity<XenoChargerLungeComponent> ent, ref XenoChargerLungeActionEvent args)
    {
        if (args.Handled)
            return;

        // Block double-activation.
        if (_activeQuery.HasComp(ent))
            return;

        args.Handled = true;

        // --- Determine whether we are charging and snapshot stage ---
        var isCharged = _steeringQuery.TryComp(ent, out var steering) &&
                        HasComp<ActiveXenoCursorSteeringComponent>(ent) &&
                        steering.Stage > 0;

        var stage = isCharged ? steering!.Stage : 0;

        // --- Lock lunge direction ---
        Vector2 direction;
        if (isCharged)
        {
            // Use momentum direction already built up by the charge.
            direction = AngleToVec(steering!.CurrentHeading);
        }
        else
        {
            // Fall back to cursor direction if available, otherwise world rotation.
            if (_steeringQuery.TryComp(ent, out var steerFallback))
                direction = AngleToVec(steerFallback.TargetHeading);
            else
                direction = AngleToVec(_transform.GetWorldRotation(ent));
        }

        // --- End the charge if one was active ---
        if (isCharged)
        {
            RemComp<ActiveXenoCursorSteeringComponent>(ent);

            // Reset steering state so it doesn't linger.
            if (steering != null)
            {
                steering.Stage = 0;
                steering.DistanceTraveled = 0f;
                Dirty(ent, steering);
            }
        }

        // --- Compute lunge speed ---
        var comp = ent.Comp;
        var speed = comp.LungeSpeed + stage * comp.LungeSpeedPerStage;

        // --- Activate lunge ---
        var active = EnsureComp<ActiveXenoChargerLungeComponent>(ent);
        active.LungeDirection = direction;
        active.DistanceRemaining = comp.LungeDistance + stage * comp.LungeDistancePerStage;
        active.ChargeStageAtLunge = stage;
        active.HitEntities.Clear();
        Dirty(ent, active);

        // Apply immediate velocity impulse.
        if (_physicsQuery.TryGetComponent(ent, out var physics))
        {
            _physics.SetAwake((ent.Owner, physics), true);
            _physics.SetLinearVelocity(ent, direction * speed, body: physics);
        }

        // Set cooldown on the action.
        var cooldown = isCharged ? comp.ChargedCooldown : comp.Cooldown;
        _actions.SetCooldown(args.Action.Owner, cooldown);

        // Visual / audio feedback.
        if (_net.IsServer)
        {
            var msgKey = isCharged
                ? "rmc-xeno-lunge-charged-activate"
                : "rmc-xeno-lunge-activate";

            _popup.PopupEntity(
                Loc.GetString(msgKey, ("xeno", ent.Owner)),
                ent,
                PopupType.Small
            );

            _audio.PlayPvs(new SoundPathSpecifier("/Audio/_RMC14/Xeno/alien_pounce.ogg"), ent);
        }
    }

    // -------------------------------------------------------------------------
    // Collision — deferred into _lungeHit, processed in Update()
    // -------------------------------------------------------------------------

    private void OnLungeCollide(Entity<ActiveXenoChargerLungeComponent> ent, ref StartCollideEvent args)
    {
        _lungeHit.Add((ent.Owner, args.OtherEntity));
    }

    // -------------------------------------------------------------------------
    // Update loop
    // -------------------------------------------------------------------------

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        // --- Process deferred collisions ---
        try
        {
            foreach (var (lunger, target) in _lungeHit)
            {
                if (TerminatingOrDeleted(lunger) || TerminatingOrDeleted(target))
                    continue;

                if (!_activeQuery.TryComp(lunger, out var active))
                    continue;

                // Skip already-hit entities (bowling ball: each target hit once per lunge).
                if (!active.HitEntities.Add(target))
                    continue;

                if (!_lungeQuery.TryComp(lunger, out var lunge))
                    continue;

                HandleLungeCollision(lunger, target, active, lunge);
            }
        }
        finally
        {
            _lungeHit.Clear();
        }

        // --- Drive active lunges ---
        var query = EntityQueryEnumerator<ActiveXenoChargerLungeComponent, XenoChargerLungeComponent, PhysicsComponent>();
        while (query.MoveNext(out var uid, out var active, out var lunge, out var physics))
        {
            var speed = lunge.LungeSpeed + active.ChargeStageAtLunge * lunge.LungeSpeedPerStage;
            var vel = active.LungeDirection * speed;

            _physics.SetAwake((uid, physics), true);
            _physics.SetLinearVelocity(uid, vel, body: physics);

            var distThisFrame = speed * frameTime;
            active.DistanceRemaining -= distThisFrame;
            Dirty(uid, active);

            if (active.DistanceRemaining <= 0f)
                EndLunge(uid, physics, active.ChargeStageAtLunge > 4);
        }
    }

    // -------------------------------------------------------------------------
    // Collision handling
    // -------------------------------------------------------------------------

    private void HandleLungeCollision(
        EntityUid lunger,
        EntityUid target,
        ActiveXenoChargerLungeComponent active,
        XenoChargerLungeComponent lunge)
    {
        var stage = active.ChargeStageAtLunge;
        var isCharged = stage > 0;

        Log.Debug($"LungeCollision: lunger={ToPrettyString(lunger)} target={ToPrettyString(target)}");
        // --- Living mob ---
        if (TryComp(target, out MobStateComponent? mobState) && !_mobState.IsDead(target, mobState))
        {
            if (!_xeno.CanAbilityAttackTarget(lunger, target))
                return;

            float damageAmount;
            float knockbackPower;
            TimeSpan knockdown;

            if (isCharged)
            {
                damageAmount = lunge.ChargedDamageBase + stage * lunge.ChargedDamagePerStage;
                knockbackPower = lunge.ChargedKnockback + stage;
                knockdown = TimeSpan.FromSeconds(lunge.ChargedKnockdownDuration);
            }
            else
            {
                damageAmount = lunge.StandaloneDamage;
                knockbackPower = lunge.StandaloneKnockback;
                knockdown = TimeSpan.FromSeconds(lunge.StandaloneKnockdownDuration);
            }

            var damage = new DamageSpecifier();
            damage.DamageDict[_blunt] = damageAmount;
            _damageable.TryChangeDamage(target, damage, origin: lunger);

            _stun.TryParalyze(target, knockdown, false);

            var origin = _transform.GetMapCoordinates(lunger);
            _sizeStun.KnockBack(target, origin, knockbackPower, knockbackPower + 1f, knockBackSpeed: isCharged ? stage * 2f : 4f);

            if (_net.IsServer)
            {
                _popup.PopupEntity(
                    Loc.GetString("rmc-xeno-lunge-hit-others", ("user", lunger), ("target", target)),
                    target,
                    PopupType.MediumCaution
                );
            }

            // Bowling ball: plows through living targets — do NOT stop the lunge.
            return;
        }

        // --- Barricade: damage and stop ---
        if (HasComp<BarricadeComponent>(target))
        {
            var damage = new DamageSpecifier();
            damage.DamageDict[_blunt] = (stage + 1) * lunge.ChargedDamageBase;
            _damageable.TryChangeDamage(target, damage);

            if (_net.IsServer)
                _audio.PlayPvs(lunge.CadeHitSound, target);

            if (_physicsQuery.TryGetComponent(lunger, out var lungerPhysics))
                EndLunge(lunger, lungerPhysics);

            return;
        }

        // --- Generic damageable structure ---
        if (HasComp<DamageableComponent>(target))
        {
            var damage = new DamageSpecifier();
            damage.DamageDict[_blunt] = (stage + 1) * lunge.ChargedDamageBase * 0.75f;
            _damageable.TryChangeDamage(target, damage);

            // If the structure survived and is a hard static body, stop the lunge.
            if (!TerminatingOrDeleted(target) &&
                !EntityManager.IsQueuedForDeletion(target) &&
                _physicsQuery.TryGetComponent(target, out var targetPhysics) &&
                targetPhysics.Hard &&
                targetPhysics.BodyType == BodyType.Static)
            {
                if (_physicsQuery.TryGetComponent(lunger, out var lungerPhysics))
                    EndLunge(lunger, lungerPhysics, isCharged);
            }

            return;
        }

        // --- Hard static wall with no health: always stop ---
        if (_physicsQuery.TryGetComponent(target, out var wallPhysics) &&
            wallPhysics.Hard &&
            wallPhysics.BodyType == BodyType.Static)
        {
            if (_physicsQuery.TryGetComponent(lunger, out var lungerPhysics))
                EndLunge(lunger, lungerPhysics);
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private void EndLunge(EntityUid uid, PhysicsComponent physics, bool wasCharged = false)
    {
        _physics.SetLinearVelocity(uid, Vector2.Zero, body: physics);
        RemComp<ActiveXenoChargerLungeComponent>(uid);

        if (_net.IsServer)
        {
            _popup.PopupEntity(
                Loc.GetString("rmc-xeno-lunge-end", ("xeno", uid)),
                uid,
                PopupType.SmallCaution
            );
        }
    }

    private static Vector2 AngleToVec(Angle angle)
    {
        return new Vector2((float)Math.Cos(angle.Theta), (float)Math.Sin(angle.Theta));
    }
}
