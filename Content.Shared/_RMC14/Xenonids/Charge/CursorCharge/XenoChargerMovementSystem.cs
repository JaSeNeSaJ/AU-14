using System.Numerics;
using Content.Shared._RMC14.Emote;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.Xenonids.Charge.CursorCharge;

public sealed class XenoChargerMovementSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedRMCEmoteSystem _rmcEmote = default!;

    private EntityQuery<PhysicsComponent> _physicsQuery;

    public override void Initialize()
    {
        _physicsQuery = GetEntityQuery<PhysicsComponent>();

        SubscribeNetworkEvent<XenoCursorSteeringMessage>(OnCursorSteeringMessage);
        SubscribeLocalEvent<XenoChargerActiveComponent, MoveInputEvent>(OnMoveInput);

    }

    // -------------------------------------------------------------------------
    // Public transition API — the only place MoveState changes
    // -------------------------------------------------------------------------

    public void StartCharge(Entity<XenoChargerComponent> ent)
    {
        EnsureComp<XenoChargerActiveComponent>(ent);

        var comp = ent.Comp;
        comp.MoveState = XenoChargerMoveState.Charging;
        comp.Stage = 0;
        comp.DistanceTraveled = 0f;
        comp.SoundDistanceAccumulator = 0f;
        comp.HitEntities.Clear();
        comp.CurrentHeading = comp.TargetHeading;

        // Zero velocity so old player movement doesn't fight the first charge tick
        if (_physicsQuery.TryGetComponent(ent, out var physics))
            _physics.SetLinearVelocity(ent, Vector2.Zero, body: physics);

        Dirty(ent, comp);
    }

    public void StartLunge(Entity<XenoChargerComponent> ent)
    {
        EnsureComp<XenoChargerActiveComponent>(ent);

        var comp = ent.Comp;
        var stage = comp.Stage;

        // Lock lunge direction from current charge heading, or target heading if idle.
        var direction = comp.MoveState == XenoChargerMoveState.Charging
            ? comp.CurrentHeading.ToVec()
            : comp.TargetHeading.ToVec();

        // Zero velocity before taking over — clean handoff.
        if (_physicsQuery.TryGetComponent(ent, out var physics))
            _physics.SetLinearVelocity(ent, Vector2.Zero, body: physics);

        comp.MoveState = XenoChargerMoveState.Lunging;
        comp.LungeDirection = direction;
        comp.LungeDistanceRemaining = comp.LungeDistance + stage * comp.LungeDistancePerStage;
        comp.HitEntities.Clear();

        // Stage is preserved in comp.Stage so collision system can read it.
        Dirty(ent, comp);
    }

    public void ResetToIdle(Entity<XenoChargerComponent> ent)
    {
        RemComp<XenoChargerActiveComponent>(ent);

        var comp = ent.Comp;
        comp.MoveState = XenoChargerMoveState.Idle;
        comp.Stage = 0;
        comp.DistanceTraveled = 0f;
        comp.HitEntities.Clear();

        if (_physicsQuery.TryGetComponent(ent, out var physics))
            _physics.SetLinearVelocity(ent, Vector2.Zero, body: physics);

        Dirty(ent, comp);
    }

    // -------------------------------------------------------------------------
    // Update — single velocity write per tick
    // -------------------------------------------------------------------------

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var query = EntityQueryEnumerator<XenoChargerComponent, PhysicsComponent>();
        while (query.MoveNext(out var uid, out var comp, out var physics))
        {
            switch (comp.MoveState)
            {
                case XenoChargerMoveState.Charging:
                    UpdateCharging((uid, comp), physics, frameTime);
                    break;
                case XenoChargerMoveState.Lunging:
                    UpdateLunging((uid, comp), physics, frameTime);
                    break;
            }
        }
    }

    private void UpdateCharging(Entity<XenoChargerComponent> ent, PhysicsComponent physics, float frameTime)
    {
        var comp = ent.Comp;

        // Speed scales up with stage.
        var speed = comp.BaseSpeed + comp.Stage * comp.SpeedPerStage;
        var vel = comp.CurrentHeading.ToVec() * speed;

        // Accumulate distance and increment stage.
        var distThisFrame = speed * frameTime;
        comp.DistanceTraveled += distThisFrame;

        if (comp.Stage < comp.MaxStage && comp.DistanceTraveled >= comp.DistancePerStage)
        {
            comp.Stage++;
            comp.DistanceTraveled -= comp.DistancePerStage;

            if (comp.Stage == comp.MaxStage)
                _rmcEmote.TryEmoteWithChat(ent, "XenoRoar", cooldown: TimeSpan.FromSeconds(20));
        }

        // Turn rate scales down as stage increases.
        var stageRatio = comp.Stage / (float)comp.MaxStage;
        var maxTurnRate = MathHelper.Lerp(comp.BaseTurnRate, comp.MinTurnRate, stageRatio);

        var angleDelta = (comp.TargetHeading - comp.CurrentHeading).Reduced();
        var delta = angleDelta.Theta;
        if (delta > Math.PI) delta -= 2 * Math.PI;
        else if (delta < -Math.PI) delta += 2 * Math.PI;

        var turnAmount = (float)Math.Clamp(delta, -maxTurnRate * frameTime, maxTurnRate * frameTime);
        comp.CurrentHeading = new Angle(comp.CurrentHeading.Theta + turnAmount);



        _physics.SetAwake((ent.Owner, physics), true);
        _physics.SetLinearVelocity(ent, vel, body: physics);

        _transform.SetWorldRotation(ent, comp.CurrentHeading.GetDir().ToAngle());

        // Stomp sound.
        comp.SoundDistanceAccumulator += distThisFrame;
        var soundInterval = comp.SoundEveryDistance / (1f + comp.Stage * 0.15f);
        if (comp.SoundDistanceAccumulator >= soundInterval && _net.IsServer && comp.ChargeSound != null)
        {
            comp.SoundDistanceAccumulator = 0f;
            _audio.PlayPvs(comp.ChargeSound, ent);
        }

        Dirty(ent, comp);
    }

    private void UpdateLunging(Entity<XenoChargerComponent> ent, PhysicsComponent physics, float frameTime)
    {
        var comp = ent.Comp;
        var speed = comp.LungeSpeed + comp.Stage * comp.LungeSpeedPerStage;

        _physics.SetAwake((ent.Owner, physics), true);
        _physics.SetLinearVelocity(ent, comp.LungeDirection * speed, body: physics);

        comp.LungeDistanceRemaining -= speed * frameTime;
        Dirty(ent, comp);

        if (comp.LungeDistanceRemaining <= 0f)
            ResetToIdle((ent.Owner, comp));
    }

    // -------------------------------------------------------------------------
    // Input / network
    // -------------------------------------------------------------------------

    private void OnCursorSteeringMessage(XenoCursorSteeringMessage msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } controlled)
            return;

        if (!TryComp(controlled, out XenoChargerComponent? comp))
            return;

        if (comp.MoveState == XenoChargerMoveState.Idle)
            return;

        var playerPos = _transform.GetMapCoordinates(controlled).Position;
        var diff = msg.CursorWorldPosition - playerPos;
        if (diff.LengthSquared() < 0.01f)
            return;

        comp.TargetHeading = diff.ToAngle();
    }

    private void OnMoveInput(Entity<XenoChargerActiveComponent> ent, ref MoveInputEvent args)
    {
        args.Entity.Comp.HeldMoveButtons &= ~MoveButtons.AnyDirection;
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

}
