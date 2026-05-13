using System.Numerics;
using Content.Shared.Damage;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Stunnable;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Shared._AU14.Abominations.Abilities;

public sealed class AbominationLeapSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<AbominationLeapComponent, AbominationLeapActionEvent>(OnLeapAction);
        SubscribeLocalEvent<AbominationLeapingComponent, StartCollideEvent>(OnLeapingCollide);
    }

    private void OnLeapAction(Entity<AbominationLeapComponent> ent, ref AbominationLeapActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<PhysicsComponent>(ent, out var physics))
            return;

        args.Handled = true;

        if (_net.IsClient)
            return;

        var origin = _transform.GetMapCoordinates(ent);
        var target = _transform.ToMapCoordinates(args.Target);
        if (origin.MapId != target.MapId)
            return;

        var direction = target.Position - origin.Position;
        if (direction == Vector2.Zero)
            return;

        var distance = Math.Clamp(direction.Length(), 0.1f, ent.Comp.Range);
        var velocity = Vector2.Normalize(direction) * ent.Comp.Strength;

        _physics.SetLinearVelocity(ent, Vector2.Zero, body: physics);
        _physics.ApplyLinearImpulse(ent, velocity * physics.Mass, body: physics);

        var leaping = EnsureComp<AbominationLeapingComponent>(ent);
        leaping.EndsAt = _timing.CurTime + ent.Comp.FlightDuration;
        leaping.KnockdownTime = ent.Comp.KnockdownTime;
        leaping.Damage = ent.Comp.Damage;
        Dirty(ent, leaping);

        if (ent.Comp.LeapSound != null)
            _audio.PlayPvs(ent.Comp.LeapSound, ent);
    }

    private void OnLeapingCollide(Entity<AbominationLeapingComponent> ent, ref StartCollideEvent args)
    {
        var target = args.OtherEntity;
        if (target == ent.Owner)
            return;

        // Only react to actual mobs, and not other abominations.
        if (!HasComp<MobStateComponent>(target) || HasComp<AbominationComponent>(target))
            return;

        if (_mobState.IsDead(target))
            return;

        if (_net.IsServer)
        {
            _stun.TryParalyze(target, ent.Comp.KnockdownTime, true);
            _damageable.TryChangeDamage(target, ent.Comp.Damage, origin: ent.Owner);
        }

        StopLeap(ent);
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<AbominationLeapingComponent>();
        while (query.MoveNext(out var uid, out var leaping))
        {
            if (leaping.EndsAt > now)
                continue;

            StopLeap((uid, leaping));
        }
    }

    private void StopLeap(Entity<AbominationLeapingComponent> ent)
    {
        if (TryComp<PhysicsComponent>(ent, out var physics))
            _physics.SetLinearVelocity(ent, Vector2.Zero, body: physics);

        RemCompDeferred<AbominationLeapingComponent>(ent);
    }
}
