// Content.Client/_RMC14/Xenonids/Charge/CursorCharge/XenoCursorSteeringClientSystem.cs

using Content.Shared._RMC14.Xenonids.Charge.CursorCharge;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Client._RMC14.Xenonids.Charge.CursorCharge;

public sealed class XenoCursorSteeringClientSystem : EntitySystem
{
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IOverlayManager _overlayManager = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private Angle _lastSentAngle;
    private XenoCursorSteeringOverlay? _overlay;

    public override void Initialize()
    {
        SubscribeLocalEvent<XenoChargerComponent, LocalPlayerAttachedEvent>(OnAttached);
        SubscribeLocalEvent<XenoChargerComponent, LocalPlayerDetachedEvent>(OnDetached);
    }

    private void OnAttached(Entity<XenoChargerComponent> ent, ref LocalPlayerAttachedEvent args)
    {
        _lastSentAngle = default;
        _overlay = new XenoCursorSteeringOverlay(EntityManager);
        _overlayManager.AddOverlay(_overlay);
    }

    private void OnDetached(Entity<XenoChargerComponent> ent, ref LocalPlayerDetachedEvent args)
    {
        _lastSentAngle = default;
        if (_overlay != null)
        {
            _overlayManager.RemoveOverlay(_overlay);
            _overlay = null;
        }
    }

    public override void FrameUpdate(float frameTime)
    {
        if (_player.LocalEntity is not { } controlled)
            return;

        if (!TryComp(controlled, out XenoChargerComponent? comp))
            return;

        var screenPos = _input.MouseScreenPosition;
        var mapCoords = _eye.PixelToMap(screenPos);

        if (mapCoords.MapId == MapId.Nullspace)
            return;

        RaiseNetworkEvent(new XenoCursorSteeringMessage { CursorWorldPosition = mapCoords.Position });
    }
}
