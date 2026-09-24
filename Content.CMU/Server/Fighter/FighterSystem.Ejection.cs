using System.Numerics;
using Content.Shared.CMU14.Fighter;
using Content.Shared.ParaDrop;
using Content.Shared.Popups;
using Robust.Shared.Map;
using Robust.Shared.Audio;

namespace Content.Server.CMU14.Fighter;

public sealed partial class FighterSystem
{
    [Dependency] private SharedParaDropSystem _paraDrop = default!;

    public bool TryRequestEjection(EntityUid? user)
    {
        if (!TryGetSeat(user, out var seat, out var aircraft) || aircraft.Comp.GroundState == FighterGroundState.Grounded)
            return false;
        seat.Comp.EjectConfirmUntil = _timing.CurTime + TimeSpan.FromSeconds(10);
        Dirty(seat);
        return true;
    }

    public bool TryConfirmEjection(EntityUid? user)
    {
        if (!TryGetSeat(user, out var seat, out var aircraft) || aircraft.Comp.GroundState == FighterGroundState.Grounded ||
            seat.Comp.EjectConfirmUntil <= _timing.CurTime) return false;
        var a = aircraft.Comp;
        var point = a.Position;
        var map = a.TerrainMap;
        // Off-map holding points have no ground beneath them. Recover over the
        // nearest valid battlefield tile, or the launch site on a carrier map.
        if (!HasGroundTile(map, point))
        {
            point = Vector2.Clamp(point, a.Battlefield.BottomLeft + Vector2.One, a.Battlefield.TopRight - Vector2.One);
            var found = false;
            for (var radius = 0; radius <= 128 && !found; radius += 4)
            for (var side = 0; side < 16 && !found; side++)
            {
                var candidate = point + new Angle(side * Math.PI / 8).RotateVec(new Vector2(radius, 0));
                if (!HasGroundTile(map, candidate)) continue;
                point = candidate;
                found = true;
            }
            if (!found && a.GroundEntity is { } hull && TryComp(hull, out FighterGroundComponent? ground) &&
                _transform.GetMap(ground.LaunchCoordinates) is { } launchMap)
            {
                map = launchMap;
                point = _transform.ToMapCoordinates(ground.LaunchCoordinates).Position + new Vector2(3, 0);
            }
            else if (!found) return false;
        }
        var crewSeats = seat.Comp.Pilot ? new[] { a.FrontSeat, a.RearSeat } : new EntityUid?[] { seat.Owner };
        if (a.Flyby is { } flyby && !TerminatingOrDeleted(flyby))
            _fighterAudio.PlayGround(new SoundPathSpecifier("/Audio/CMU14/Fighter/ejection.ogg"), Transform(flyby).Coordinates, 35, -5);
        foreach (var uid in crewSeats)
        {
            if (uid is not { } crewSeat || !TryComp(crewSeat, out FighterSeatComponent? component) || component.Occupant is not { } crew)
                continue;
            component.EjectConfirmUntil = TimeSpan.Zero;
            // Keep the release audible as the ejected crew leaves the cockpit map.
            _audio.PlayGlobal(new SoundPathSpecifier("/Audio/CMU14/Fighter/ejection.ogg"), crew);
            _popup.PopupEntity(Loc.GetString("cmu-fighter-eject-fired"), crew, crew, PopupType.LargeCaution);
            // The null user is the server's forced unbuckle, after the confirmation
            // above. The normal unbuckle path remains blocked while airborne.
            _buckle.Unbuckle(crew, null);
            _paraDrop.DoParaDrop(crew, new EntityCoordinates(map, point), .7f, 3.5f, null, dropScatter: 3);
        }
        if (seat.Comp.Pilot && a.GroundState == FighterGroundState.Airborne) ReturnToGround(aircraft);
        return true;
    }
}
