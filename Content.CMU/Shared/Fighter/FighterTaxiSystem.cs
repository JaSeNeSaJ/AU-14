using Content.Shared.Vehicle;
using Content.Shared.Vehicle.Components;

namespace Content.Shared.CMU14.Fighter;

/// <summary>The ground fighter uses the same movement and collision controller as wheeled vehicles.</summary>
public sealed class FighterTaxiSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<FighterGroundComponent, VehicleDriveInputEvent>(OnDriveInput);
        SubscribeLocalEvent<FighterGroundComponent, VehicleCanRunEvent>(OnCanRun);
    }

    private void OnCanRun(Entity<FighterGroundComponent> ground, ref VehicleCanRunEvent args)
    {
        if (ground.Comp.State != FighterGroundState.Grounded)
            args.CanRun = false;
    }

    private void OnDriveInput(Entity<FighterGroundComponent> ground, ref VehicleDriveInputEvent args)
    {
        // Block driving and pushing throughout VTOL transitions, including an empty cockpit.
        args.Handled = true;
        if (ground.Comp.State != FighterGroundState.Grounded ||
            !TryComp(ground.Comp.Aircraft, out FighterAircraftComponent? aircraft) ||
            !TryComp(aircraft.FrontSeat, out FighterSeatComponent? pilot) || pilot.Occupant == null ||
            !TryComp(ground, out VehicleComponent? vehicle) || vehicle.Operator != pilot.Occupant)
            return;

        if (ground.Comp.TaxiInput != FighterInput.None)
        {
            args.Throttle = FighterFlight.Axis(ground.Comp.TaxiInput, FighterInput.Forward, FighterInput.Back);
            args.Steering = FighterFlight.Axis(ground.Comp.TaxiInput, FighterInput.Left, FighterInput.Right);
            return;
        }
        if (pilot.CameraControl) return;

        if (ground.Comp.TaxiPad is { } pad && !TerminatingOrDeleted(pad))
        {
            var delta = _transform.GetWorldPosition(pad) - _transform.GetWorldPosition(ground);
            if (delta.LengthSquared() <= .2f * .2f) return;
            var angle = Angle.FromWorldVec(delta) - _transform.GetWorldRotation(ground);
            var turn = (float) Math.Atan2(Math.Sin(angle.Theta), Math.Cos(angle.Theta));
            // Use reverse when the pad is behind us rather than circling across the lift.
            var reverse = MathF.Abs(turn) > MathF.PI / 2;
            if (reverse) turn = (float) Math.Atan2(Math.Sin(turn + MathF.PI), Math.Cos(turn + MathF.PI));
            args.Steering = Math.Clamp(turn * (reverse ? -2 : 2), -1, 1);
            args.Throttle = (reverse ? -1 : 1) * Math.Clamp(delta.Length() * .35f, .18f, .6f);
            return;
        }

        // Zero input means braking. Never fall through to character movement:
        // the crew is buckled to a child seat, not the vehicle's mover entity.
    }
}
