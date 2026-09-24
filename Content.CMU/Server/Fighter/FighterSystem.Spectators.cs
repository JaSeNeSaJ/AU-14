using System.Linq;
using Content.Shared.CMU14.Fighter;
using Content.Shared.GameTicking;
using Robust.Server.Player;
using Robust.Shared.Player;

namespace Content.Server.CMU14.Fighter;

public sealed partial class FighterSystem
{
    [Dependency] private FighterViewSystem _fighterView = default!;
    [Dependency] private IPlayerManager _spectatorPlayers = default!;
    private readonly Dictionary<ICommonSession, SpectatorViews> _spectatorViews = [];
    private readonly HashSet<ICommonSession> _activeSpectators = [];
    private TimeSpan _nextSpectatorUpdate;

    private readonly record struct SpectatorViews(EntityUid Aircraft, EntityUid Camera, EntityUid Exterior)
    {
        public bool Contains(EntityUid uid) => Aircraft == uid || Camera == uid || Exterior == uid;
    }

    private void OnFighterRoundCleanup(RoundRestartCleanupEvent ev)
    {
        ClearSpectatorViews();
        _manpadCharts.Clear();
        _manpadRadarMaps.Clear();
        _nextSpectatorUpdate = _nextManpadRadarUpdate = TimeSpan.Zero;
    }

    public override void Shutdown()
    {
        ClearSpectatorViews();
        base.Shutdown();
    }

    private void ClearSpectatorViews()
    {
        foreach (var session in _spectatorViews.Keys.ToArray())
            RemoveSpectatorViews(session);
        _activeSpectators.Clear();
    }

    private void RemoveSpectatorViews(ICommonSession session, SpectatorViews? keep = null)
    {
        if (!_spectatorViews.Remove(session, out var previous)) return;
        foreach (var uid in new[] { previous.Aircraft, previous.Camera, previous.Exterior })
        {
            // Subscriptions are sets, not reference counted. Boarding must retain
            // any camera that the new crew seat already subscribed to.
            if (keep?.Contains(uid) != true)
                _views.RemoveViewSubscriber(uid, session);
        }
    }

    private void UpdateFighterSpectators()
    {
        if (_timing.CurTime < _nextSpectatorUpdate) return;
        _nextSpectatorUpdate = _timing.CurTime + TimeSpan.FromSeconds(.25);
        _activeSpectators.Clear();
        foreach (var session in _spectatorPlayers.Sessions)
        {
            if (!_fighterView.TryGetView(session.AttachedEntity, out var seat, out var spectator) ||
                seat.Comp.Aircraft is not { } aircraft || seat.Comp.Camera is not { } camera ||
                seat.Comp.ExteriorCamera is not { } exterior || TerminatingOrDeleted(aircraft) ||
                TerminatingOrDeleted(camera) || TerminatingOrDeleted(exterior)) continue;

            var views = new SpectatorViews(aircraft, camera, exterior);
            if (!spectator)
            {
                RemoveSpectatorViews(session, views);
                continue;
            }
            _activeSpectators.Add(session);
            if (_spectatorViews.TryGetValue(session, out var previous) && previous == views) continue;
            RemoveSpectatorViews(session, views);
            _spectatorViews[session] = views;
            _views.AddViewSubscriber(aircraft, session);
            _views.AddViewSubscriber(camera, session);
            _views.AddViewSubscriber(exterior, session);
        }
        foreach (var session in _spectatorViews.Keys.ToArray())
        {
            if (!_activeSpectators.Contains(session)) RemoveSpectatorViews(session);
        }
    }
}
