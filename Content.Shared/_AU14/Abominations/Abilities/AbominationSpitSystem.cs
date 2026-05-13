using System.Numerics;
using Content.Shared.Projectiles;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Shared._AU14.Abominations.Abilities;

public sealed class AbominationSpitSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<AbominationSpitComponent, AbominationSpitActionEvent>(OnSpitAction);
    }

    private void OnSpitAction(Entity<AbominationSpitComponent> ent, ref AbominationSpitActionEvent args)
    {
        if (args.Handled)
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

        var velocity = Vector2.Normalize(direction) * ent.Comp.Speed;

        var projectile = Spawn(ent.Comp.Projectile, origin);
        if (TryComp<ProjectileComponent>(projectile, out var proto))
            proto.Shooter = ent.Owner;
        if (TryComp<PhysicsComponent>(projectile, out var physics))
            _physics.SetLinearVelocity(projectile, velocity, body: physics);

        if (ent.Comp.Sound != null)
            _audio.PlayPvs(ent.Comp.Sound, ent);
    }
}
