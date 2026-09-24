using Content.Client.UserInterface.Controls;
using Content.Shared.Buckle.Components;
using Content.Shared.CMU14.Fighter;
using Content.Shared.Movement.Systems;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Fighter;

public sealed partial class FighterClientSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    private FighterSensorOverlay? _sensorOverlay;
    private FighterCockpitControl? _display;
    private LayoutContainer? _parent;
    private FighterInput _input;
    private bool _lastCameraControl;
    private EntityUid? _currentSeat;
    private TimeSpan _nextInput;
    private readonly FighterViewportCover _worldCover = new();

    public override void Initialize()
    {
        UpdatesAfter.Add(typeof(EyeSystem));
        _configuration.OnValueChanged(FighterTrialCVars.Enabled, OnTrialRequested, true);
        var binds = CommandBinds.Builder;
        binds.BindBefore(EngineKeyFunctions.MoveUp, new FlightInputHandler(this, FighterInput.Forward), typeof(SharedMoverController));
        binds.BindBefore(EngineKeyFunctions.MoveDown, new FlightInputHandler(this, FighterInput.Back), typeof(SharedMoverController));
        binds.BindBefore(EngineKeyFunctions.MoveLeft, new FlightInputHandler(this, FighterInput.Left), typeof(SharedMoverController));
        binds.BindBefore(EngineKeyFunctions.MoveRight, new FlightInputHandler(this, FighterInput.Right), typeof(SharedMoverController));
        binds.Register<FighterClientSystem>();
    }

    public override void Shutdown()
    {
        _configuration.UnsubValueChanged(FighterTrialCVars.Enabled, OnTrialRequested);
        CommandBinds.Unregister<FighterClientSystem>();
        Hide();
        if (_sensorOverlay != null)
        {
            _overlays.RemoveOverlay(_sensorOverlay);
            _sensorOverlay.Dispose();
            _sensorOverlay = null;
        }
        base.Shutdown();
    }

    private bool TryGetSeat(out Entity<FighterSeatComponent> seat)
    {
        seat = default;
        if (_player.LocalEntity is not { } user || !TryComp(user, out BuckleComponent? buckle) || buckle.BuckledTo is not { } uid ||
            !TryComp(uid, out FighterSeatComponent? component))
            return false;
        seat = (uid, component);
        return true;
    }

    public override void FrameUpdate(float frameTime)
    {
        if (!TryGetSeat(out var seat) || seat.Comp.Aircraft is not { } uid || !TryComp(uid, out FighterAircraftComponent? aircraft) ||
            seat.Comp.Camera is not { } camera || !TryComp(camera, out EyeComponent? eye) ||
            seat.Comp.ExteriorCamera is not { } exterior || !TryComp(exterior, out EyeComponent? exteriorEye) ||
            _ui.ActiveScreen?.GetWidget<MainViewport>() is not { } viewport)
        {
            Hide();
            return;
        }
        var parent = _ui.WindowRoot;
        if (_parent != parent)
        {
            if (_parent != null) Hide();
            _parent = parent;
        }
        if (_currentSeat != seat.Owner)
        {
            _input = FighterInput.None;
            _currentSeat = seat.Owner;
        }
        if (_display == null)
        {
            _display = new FighterCockpitControl(command => RaiseNetworkEvent(new FighterCommandEvent(command)),
                (entry, exit) => RaiseNetworkEvent(new FighterPlanEvent(entry, exit)),
                (height, speed) => RaiseNetworkEvent(new FighterSettingsEvent(height, speed)),
                slot => RaiseNetworkEvent(new FighterSelectWeaponEvent(slot)),
                flare => RaiseNetworkEvent(new FighterSelectTargetEvent(flare)),
                sector => RaiseNetworkEvent(new FighterCoverSectorEvent(sector)));
            parent.AddChild(_display);
        }
        // The cockpit belongs inside the game view. WindowRoot also contains
        // chat and the HUD; stretching across it placed the controls over chat.
        var origin = viewport.GlobalPosition - parent.GlobalPosition;
        LayoutContainer.SetMarginLeft(_display, origin.X);
        LayoutContainer.SetMarginTop(_display, origin.Y);
        LayoutContainer.SetMarginRight(_display, origin.X + viewport.Size.X);
        LayoutContainer.SetMarginBottom(_display, origin.Y + viewport.Size.Y);
        TryComp(uid, out FighterChartComponent? chart);
        // Ground taxiing uses the ordinary game view. The flight display only
        // replaces it once the crew has moved into the abstract airspace.
        var groundScene = FighterFlight.GroundScene(aircraft);
        _worldCover.Update(_ui.ActiveScreen, !groundScene);
        var observer = aircraft.RearSeat is { } rear ? CompOrNull<FighterSeatComponent>(rear) : null;
        var pilotCrew = aircraft.FrontSeat is { } front && TryComp(front, out FighterSeatComponent? pilot) ? pilot.Occupant : null;
        _display.UpdateFlight(aircraft, chart, seat.Comp, eye.Eye, exteriorEye.Eye, -_transform.GetWorldRotation(exterior), observer, pilotCrew, observer?.Occupant,
            CompOrNull<FighterWeaponsComponent>(uid), CompOrNull<FighterAirCombatComponent>(uid), CompOrNull<FighterEffectsComponent>(uid));
        if (_sensorOverlay == null)
        {
            _sensorOverlay = new FighterSensorOverlay();
            _overlays.AddOverlay(_sensorOverlay);
        }
        _sensorOverlay.Viewport = _display.SensorViewport;
        _sensorOverlay.Mode = FighterOptics.Mode(aircraft, _timing.CurTime);
        UpdateTrial(aircraft, seat.Comp);
        var cameraControl = _display.CameraControl;
        if (_timing.CurTime >= _nextInput || cameraControl != _lastCameraControl)
        {
            _nextInput = _timing.CurTime + TimeSpan.FromMilliseconds(200);
            SendInput();
        }
    }

    private void SendInput()
    {
        var cameraControl = _display?.CameraControl == true;
        if (TryGetSeat(out var seat))
        {
            // Consume seated movement before the normal mover can unbuckle us,
            // then feed it directly to the vehicle controller on both sides.
            seat.Comp.CameraControl = cameraControl;
            if (seat.Comp.Pilot && TryComp(seat.Comp.Aircraft, out FighterAircraftComponent? aircraft) &&
                TryComp(aircraft.GroundEntity, out FighterGroundComponent? ground))
                ground.TaxiInput = ground.State == FighterGroundState.Grounded ? _input : FighterInput.None;
        }
        _lastCameraControl = cameraControl;
        RaiseNetworkEvent(new FighterInputEvent(_input, cameraControl));
    }

    private void Hide()
    {
        _worldCover.Dispose();
        if (_sensorOverlay != null)
            _sensorOverlay.Viewport = null;
        _display?.Orphan();
        _display?.Dispose();
        _display = null;
        _parent = null;
        _currentSeat = null;
        _input = FighterInput.None;
        _lastCameraControl = false;
    }

    private sealed class FlightInputHandler(FighterClientSystem system, FighterInput input) : InputCmdHandler
    {
        public override bool FireOutsidePrediction => true;
        private bool _consumed;

        public override bool HandleCmdMessage(IEntityManager entityManager, ICommonSession? session, IFullInputCmdMessage message)
        {
            if (message.State == BoundKeyState.Up)
            {
                var tracked = (system._input & input) != 0;
                system._input &= ~input;
                var consumed = _consumed;
                _consumed = false;
                if (tracked)
                    system.SendInput();
                return consumed;
            }
            if (!system.TryGetSeat(out var seat))
                return false;
            if (system._currentSeat != seat.Owner)
            {
                system._input = FighterInput.None;
                system._currentSeat = seat.Owner;
            }
            _consumed = true;
            system._input |= input;
            system.SendInput();
            return _consumed;
        }
    }
}
