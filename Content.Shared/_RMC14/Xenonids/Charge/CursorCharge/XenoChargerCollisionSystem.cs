using System.Numerics;
using Content.Shared._RMC14.Entrenching;
using Content.Shared._RMC14.Explosion;
using Content.Shared._RMC14.Stun;
using Content.Shared._RMC14.Xenonids.Projectile;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;


namespace Content.Shared._RMC14.Xenonids.Charge.CursorCharge;

public sealed class XenoChargerCollisionSystem : EntitySystem
{
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
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly XenoChargerMovementSystem _movement = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly XenoProjectileSystem _projectile = default!;

    private readonly ProtoId<DamageTypePrototype> _blunt = "Blunt";
    private const float HeadOnDotThreshold = 0.707f; // cos(45°)
    private readonly HashSet<(EntityUid Charger, EntityUid Target)> _hits = new();

    private EntityQuery<PhysicsComponent> _physicsQuery;

    public override void Initialize()
    {
        _physicsQuery = GetEntityQuery<PhysicsComponent>();
        SubscribeLocalEvent<XenoChargerComponent, StartCollideEvent>(OnCollide);
    }

    private void OnCollide(Entity<XenoChargerComponent> xeno, ref StartCollideEvent args)
    {
        // Only care if actively moving.
        if (!TryComp(xeno.Owner, out XenoChargerStateComponent? state))
            return;

        if (state.MoveState == XenoChargerMoveState.Idle)
            return;

        _hits.Add((xeno.Owner, args.OtherEntity));
    }

    public void ProcessHits()
    {
        if (_net.IsClient)
            return;

        try
        {
            foreach (var (charger, target) in _hits)
            {
                if (TerminatingOrDeleted(charger) || TerminatingOrDeleted(target))
                    continue;

                if (!TryComp(charger, out XenoChargerComponent? xeno))
                    continue;

                if (!TryComp(charger, out XenoChargerStateComponent? state))
                    continue;

                if (state.MoveState == XenoChargerMoveState.Lunging && !state.HitEntities.Add(target))
                    continue;

                switch (state.MoveState)
                {
                    case XenoChargerMoveState.Charging:
                        HandleChargingCollision(charger, xeno, state, target);
                        break;
                    case XenoChargerMoveState.Lunging:
                        HandleLungingCollision(charger, xeno, state, target);
                        break;
                }
            }
        }
        finally
        {
            _hits.Clear();
        }
    }

    // -------------------------------------------------------------------------
    // Charging collisions
    // -------------------------------------------------------------------------

    private void HandleChargingCollision(EntityUid charger, XenoChargerComponent xeno, XenoChargerStateComponent state, EntityUid target)
    {
        var stage = state.Stage;
        var atMax = stage == xeno.MaxStage;

        if (TryComp(target, out MobStateComponent? mobState) && !_mobState.IsDead(target, mobState))
        {
            if (!_xeno.CanAbilityAttackTarget(charger, target))
                return;

            var mult = atMax ? xeno.HumanDamageMultiplierMax : xeno.HumanDamageMultiplier;
            var damage = new DamageSpecifier();
            damage.DamageDict[_blunt] = stage * mult;
            _damageable.TryChangeDamage(target, damage, origin: charger);

            var knockdown = atMax
                ? TimeSpan.FromSeconds(xeno.HumanKnockdownDuration * 2)
                : TimeSpan.FromSeconds(xeno.HumanKnockdownDuration);
            _stun.TryParalyze(target, knockdown, false);

            var origin = _transform.GetMapCoordinates(charger);
            _sizeStun.KnockBack(target, origin, 2, 2, knockBackSpeed: stage);

            if (_net.IsServer)
            {
                _popup.PopupEntity(
                    Loc.GetString("rmc-xeno-charge-knockback-others", ("user", charger), ("target", target)),
                    target,
                    PopupType.MediumCaution
                );
            }

            state.Stage = Math.Max(0, state.Stage - 1);
            Dirty(charger, state);
            return;
        }

        if (HasComp<BarricadeComponent>(target))
        {
            var damage = new DamageSpecifier();
            damage.DamageDict[_blunt] = stage * xeno.BarricadeCollisionDamage;
            _damageable.TryChangeDamage(target, damage);
            _movement.ResetToIdle(charger);
            return;
        }

        if (HasComp<DamageableComponent>(target))
        {
            var damage = new DamageSpecifier();
            damage.DamageDict[_blunt] = stage * xeno.StructureDamageMultiplier;
            _damageable.TryChangeDamage(target, damage);

            if (!TerminatingOrDeleted(target) &&
                !EntityManager.IsQueuedForDeletion(target) &&
                _physicsQuery.TryGetComponent(target, out var tp) &&
                tp.Hard && tp.BodyType == BodyType.Static)
            {
                _movement.ResetToIdle(charger);
            }
            else
            {
                state.Stage = Math.Max(0, state.Stage - 1);
                Dirty(charger, state);
            }

            return;
        }

        // Raw wall — check stage and impact angle.
        if (_physicsQuery.TryGetComponent(target, out var wallPhysics) &&
            wallPhysics.Hard && wallPhysics.BodyType == BodyType.Static)
        {
            if (stage <= 4)
                return;

            var chargeDir = state.CurrentHeading.ToVec();
            var wallNormal = GetWallNormal(charger, target);
            var dot = Vector2.Dot(chargeDir, wallNormal);

            if (dot < -HeadOnDotThreshold)
                _movement.ResetToIdle(charger);
        }
    }

    private void HandleLungingCollision(EntityUid charger, XenoChargerComponent xeno, XenoChargerStateComponent state, EntityUid target)
    {
        var stage = state.Stage;
        var isCharged = stage > 4;

        if (TryComp(target, out MobStateComponent? mobState) && !_mobState.IsDead(target, mobState))
        {
            if (!_xeno.CanAbilityAttackTarget(charger, target))
                return;

            float damageAmount;
            float knockbackPower;
            TimeSpan knockdown;

            if (isCharged)
            {
                damageAmount = xeno.ChargedDamageBase + stage * xeno.ChargedDamagePerStage;
                knockbackPower = xeno.ChargedKnockback;
                knockdown = TimeSpan.FromSeconds(xeno.ChargedKnockdownDuration);
            }
            else
            {
                damageAmount = xeno.StandaloneDamage;
                knockbackPower = xeno.StandaloneKnockback;
                knockdown = TimeSpan.FromSeconds(xeno.StandaloneKnockdownDuration);
            }

            var damage = new DamageSpecifier();
            damage.DamageDict[_blunt] = damageAmount;
            _damageable.TryChangeDamage(target, damage, origin: charger);
            _stun.TryParalyze(target, knockdown, false);

            var origin = _transform.GetMapCoordinates(charger);
            _sizeStun.KnockBack(target, origin, knockbackPower, knockbackPower + 1f,
                knockBackSpeed: stage);

            if (_net.IsServer)
            {
                _popup.PopupEntity(
                    Loc.GetString("rmc-xeno-lunge-hit-others", ("user", charger), ("target", target)),
                    target,
                    PopupType.MediumCaution
                );
            }

            return;
        }

        if (HasComp<BarricadeComponent>(target))
        {
            var damage = new DamageSpecifier();
            damage.DamageDict[_blunt] = (stage + 1) * xeno.ChargedDamageBase;
            _damageable.TryChangeDamage(target, damage);

            if (_net.IsServer)
                _audio.PlayPvs(xeno.CadeHitSound, target);

            if (stage >= 7)
            {
                if (_net.IsServer)
                {
                    _transform.Unanchor(target);
                    _throwing.TryThrow(target, state.LungeDirection, 5f + stage * 1.5f, compensateFriction: true);
                }

                state.Stage = Math.Max(0, state.Stage - 1);
                Dirty(charger, state);
                return;
            }

            _movement.ResetToIdle(charger);
            return;
        }

        // Walls — same penetration logic as barricades.
        if (_physicsQuery.TryGetComponent(target, out var wallPhysics) &&
            wallPhysics.Hard && wallPhysics.BodyType == BodyType.Static)
        {
            if (!HasComp<DamageableComponent>(target))
            {
                _movement.ResetToIdle(charger);
            }

            if (HasComp<DamageableComponent>(target))
            {
                var damage = new DamageSpecifier();
                damage.DamageDict[_blunt] = (stage + 1) * xeno.ChargedDamageBase;
                _damageable.TryChangeDamage(target, damage);
            }

            if (stage >= 7)
            {
                //if (_net.IsServer)
                //    SpawnWallDebris(charger, target, state.LungeDirection);

                state.Stage = Math.Max(0, state.Stage - 1);
                Dirty(charger, state);

                if (_physicsQuery.TryGetComponent(charger, out var physics))
                {
                    var speed = xeno.LungeSpeed + state.Stage * xeno.LungeSpeedPerStage;
                    _physics.SetLinearVelocity(charger, state.LungeDirection * speed, body: physics);
                }
                return;
            }

            _movement.ResetToIdle(charger);
        }
    }

    private void SpawnWallDebris(EntityUid charger, EntityUid wall, Vector2 lungeDirection)
    {
        //WIP, very inconsistent and largely just hoping for some sovl, leave it for now.

        if (!_net.IsServer)
            return;

        EntityManager.DeleteEntity(wall);

        Timer.Spawn(500, () =>
        {
            if (TerminatingOrDeleted(charger))
                return;

            _projectile.TryShoot(
                charger,
                new EntityCoordinates(charger, lungeDirection * 1.5f),
                FixedPoint2.Zero,
                "XenoHedgehogSpikeProjectileSpread",
                null,
                _random.Next(14, 20),
                new Angle(2 * Math.PI),
                9f,
                projectileHitLimit: 6
            );
        });
    }

    private Vector2 GetWallNormal(EntityUid charger, EntityUid wall)
    {
        var chargerPos = _transform.GetMapCoordinates(charger).Position;
        var wallPos = _transform.GetMapCoordinates(wall).Position;
        var diff = chargerPos - wallPos;
        return diff.LengthSquared() > 0.001f ? Vector2.Normalize(diff) : Vector2.UnitX;
    }
}
