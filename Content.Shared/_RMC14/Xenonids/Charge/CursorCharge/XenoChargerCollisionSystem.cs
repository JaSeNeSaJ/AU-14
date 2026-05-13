using System.Numerics;
using Content.Shared._RMC14.Entrenching;
using Content.Shared._RMC14.Stun;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;

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

    private readonly ProtoId<DamageTypePrototype> _blunt = "Blunt";
    private readonly HashSet<(EntityUid Charger, EntityUid Target)> _hits = new();

    private EntityQuery<PhysicsComponent> _physicsQuery;

    public override void Initialize()
    {
        _physicsQuery = GetEntityQuery<PhysicsComponent>();
        SubscribeLocalEvent<XenoChargerComponent, StartCollideEvent>(OnCollide);
    }

    private void OnCollide(Entity<XenoChargerComponent> ent, ref StartCollideEvent args)
    {
        if (ent.Comp.MoveState == XenoChargerMoveState.Idle)
            return;

        _hits.Add((ent.Owner, args.OtherEntity));
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        try
        {
            foreach (var (charger, target) in _hits)
            {
                if (TerminatingOrDeleted(charger) || TerminatingOrDeleted(target))
                    continue;

                if (!TryComp(charger, out XenoChargerComponent? comp))
                    continue;

                // Per-lunge bowling ball dedup.
                if (comp.MoveState == XenoChargerMoveState.Lunging && !comp.HitEntities.Add(target))
                    continue;

                switch (comp.MoveState)
                {
                    case XenoChargerMoveState.Charging:
                        HandleChargingCollision((charger, comp), target);
                        break;
                    case XenoChargerMoveState.Lunging:
                        HandleLungingCollision((charger, comp), target);
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

    private void HandleChargingCollision(Entity<XenoChargerComponent> ent, EntityUid target)
    {
        var comp = ent.Comp;
        var stage = comp.Stage;
        var atMax = stage == comp.MaxStage;

        if (TryComp(target, out MobStateComponent? mobState) && !_mobState.IsDead(target, mobState))
        {
            if (!_xeno.CanAbilityAttackTarget(ent, target))
                return;

            var mult = atMax ? comp.HumanDamageMultiplierMax : comp.HumanDamageMultiplier;
            var damage = new DamageSpecifier();
            damage.DamageDict[_blunt] = stage * mult;
            _damageable.TryChangeDamage(target, damage, origin: ent);

            var knockdown = atMax
                ? TimeSpan.FromSeconds(comp.HumanKnockdownDuration * 2)
                : TimeSpan.FromSeconds(comp.HumanKnockdownDuration);
            _stun.TryParalyze(target, knockdown, false);

            var origin = _transform.GetMapCoordinates(ent);
            _sizeStun.KnockBack(target, origin, 2, 2, knockBackSpeed: stage * 2f);

            if (_net.IsServer)
            {
                _popup.PopupEntity(
                    Loc.GetString("rmc-xeno-charge-knockback-others", ("user", ent.Owner), ("target", target)),
                    target,
                    PopupType.MediumCaution
                );
            }

            comp.Stage = Math.Max(0, comp.Stage - 1);
            Dirty(ent, comp);
            return;
        }

        if (HasComp<BarricadeComponent>(target))
        {
            var damage = new DamageSpecifier();
            damage.DamageDict[_blunt] = stage * comp.BarricadeCollisionDamage;
            _damageable.TryChangeDamage(target, damage);
            _movement.ResetToIdle((ent.Owner, comp));
            return;
        }

        if (HasComp<DamageableComponent>(target))
        {
            var damage = new DamageSpecifier();
            damage.DamageDict[_blunt] = stage * comp.StructureDamageMultiplier;
            _damageable.TryChangeDamage(target, damage);

            if (!TerminatingOrDeleted(target) &&
                !EntityManager.IsQueuedForDeletion(target) &&
                _physicsQuery.TryGetComponent(target, out var tp) &&
                tp.Hard && tp.BodyType == BodyType.Static)
            {
                _movement.ResetToIdle((ent.Owner, comp));
            }
            else
            {
                comp.Stage = Math.Max(0, comp.Stage - 1);
                Dirty(ent, comp);
            }

            return;
        }

        if (_physicsQuery.TryGetComponent(target, out var wallPhysics) &&
            wallPhysics.Hard && wallPhysics.BodyType == BodyType.Static)
        {
            _movement.ResetToIdle((ent.Owner, comp));
        }
    }

    // -------------------------------------------------------------------------
    // Lunging collisions
    // -------------------------------------------------------------------------

    private void HandleLungingCollision(Entity<XenoChargerComponent> ent, EntityUid target)
    {
        var comp = ent.Comp;
        var stage = comp.Stage;
        var isCharged = stage > 0;

        if (TryComp(target, out MobStateComponent? mobState) && !_mobState.IsDead(target, mobState))
        {
            if (!_xeno.CanAbilityAttackTarget(ent, target))
                return;

            float damageAmount;
            float knockbackPower;
            TimeSpan knockdown;

            if (isCharged)
            {
                damageAmount = comp.ChargedDamageBase + stage * comp.ChargedDamagePerStage;
                knockbackPower = comp.ChargedKnockback + stage;
                knockdown = TimeSpan.FromSeconds(comp.ChargedKnockdownDuration);
            }
            else
            {
                damageAmount = comp.StandaloneDamage;
                knockbackPower = comp.StandaloneKnockback;
                knockdown = TimeSpan.FromSeconds(comp.StandaloneKnockdownDuration);
            }

            var damage = new DamageSpecifier();
            damage.DamageDict[_blunt] = damageAmount;
            _damageable.TryChangeDamage(target, damage, origin: ent);
            _stun.TryParalyze(target, knockdown, false);

            var origin = _transform.GetMapCoordinates(ent);
            _sizeStun.KnockBack(target, origin, knockbackPower, knockbackPower + 1f,
                knockBackSpeed: isCharged ? stage * 2f : 4f);

            if (_net.IsServer)
            {
                _popup.PopupEntity(
                    Loc.GetString("rmc-xeno-lunge-hit-others", ("user", ent.Owner), ("target", target)),
                    target,
                    PopupType.MediumCaution
                );
            }

            // Bowling ball — plow through mobs.
            return;
        }

        if (HasComp<BarricadeComponent>(target))
        {
            var damage = new DamageSpecifier();
            damage.DamageDict[_blunt] = (stage + 1) * comp.ChargedDamageBase;
            _damageable.TryChangeDamage(target, damage);

            if (_net.IsServer)
                _audio.PlayPvs(comp.CadeHitSound, target);

            if (stage >= comp.MaxStage)
            {
                // Max stage: unanchor, throw, plow through.
                if (_net.IsServer)
                {
                    _transform.Unanchor(target);
                    _throwing.TryThrow(target, comp.LungeDirection, 5f + stage * 1.5f, compensateFriction: true);
                }

                return;
            }

            _movement.ResetToIdle((ent.Owner, comp));
            return;
        }

        if (HasComp<DamageableComponent>(target))
        {
            var damage = new DamageSpecifier();
            damage.DamageDict[_blunt] = (stage + 1) * comp.ChargedDamageBase * 0.75f;
            _damageable.TryChangeDamage(target, damage);

            if (!TerminatingOrDeleted(target) &&
                !EntityManager.IsQueuedForDeletion(target) &&
                _physicsQuery.TryGetComponent(target, out var tp) &&
                tp.Hard && tp.BodyType == BodyType.Static)
            {
                _movement.ResetToIdle((ent.Owner, comp));
            }

            return;
        }

        if (_physicsQuery.TryGetComponent(target, out var wallPhysics) &&
            wallPhysics.Hard && wallPhysics.BodyType == BodyType.Static)
        {
            _movement.ResetToIdle((ent.Owner, comp));
        }
    }
}
