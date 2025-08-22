using Content.Server.GameTicking;
using Content.Server.Spawners.Components;
using Content.Server.Station.Systems;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server.Spawners.EntitySystems;

public sealed class SpawnPointSystem : EntitySystem
{
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly StationSystem _stationSystem = default!;
    [Dependency] private readonly StationSpawningSystem _stationSpawning = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<PlayerSpawningEvent>(OnPlayerSpawning);
    }

    private void OnPlayerSpawning(PlayerSpawningEvent args)
    {
        if (args.SpawnResult != null)
            return;

        var possiblePositions = new List<EntityCoordinates>();
        var preferredPositions = new List<EntityCoordinates>();

        // --- AU14: Latejoin spawnpoint override for opfor/govfor, with JobClassOverride support ---
        bool isLateJoin = _gameTicker.RunLevel == GameRunLevel.InRound;
        string? jobId = args.Job?.ToString();
        bool isOpfor = false;
        bool isGovfor = false;
        if (isLateJoin && !string.IsNullOrEmpty(jobId))
        {
            isOpfor = jobId.Contains("opfor", StringComparison.OrdinalIgnoreCase);
            isGovfor = jobId.Contains("govfor", StringComparison.OrdinalIgnoreCase);
        }

        // 1. Try opfor/govfor latejoin spawn points with matching job_id first if applicable
        if (isLateJoin && (isOpfor || isGovfor))
        {
            var points = EntityQueryEnumerator<SpawnPointComponent, TransformComponent>();
            while (points.MoveNext(out var uid, out var spawnPoint, out var xform))
            {
                if (args.Station != null && _stationSystem.GetOwningStation(uid, xform) != args.Station)
                    continue;
                // Prefer spawn points with matching job_id (for JobClassOverride)
                if (!string.IsNullOrEmpty(jobId) && spawnPoint.Job != null && string.Equals(spawnPoint.Job.ToString(), jobId, StringComparison.OrdinalIgnoreCase))
                {
                    preferredPositions.Add(xform.Coordinates);
                }
                // Otherwise, fallback to spawn type
                else if (isOpfor && spawnPoint.SpawnType.ToString() == "LateJoinOpfor")
                {
                    possiblePositions.Add(xform.Coordinates);
                }
                else if (isGovfor && spawnPoint.SpawnType.ToString() == "LateJoinGovfor")
                {
                    possiblePositions.Add(xform.Coordinates);
                }
            }
        }

        // 2. If none found, or not opfor/govfor, fall back to normal latejoin (prefer job_id match)
        if (preferredPositions.Count == 0 && possiblePositions.Count == 0)
        {
            var points = EntityQueryEnumerator<SpawnPointComponent, TransformComponent>();
            while (points.MoveNext(out var uid, out var spawnPoint, out var xform))
            {
                if (args.Station != null && _stationSystem.GetOwningStation(uid, xform) != args.Station)
                    continue;
                if (!string.IsNullOrEmpty(jobId) && spawnPoint.Job != null && string.Equals(spawnPoint.Job.ToString(), jobId, StringComparison.OrdinalIgnoreCase))
                {
                    preferredPositions.Add(xform.Coordinates);
                }
                else if (isLateJoin && spawnPoint.SpawnType == SpawnPointType.LateJoin)
                {
                    possiblePositions.Add(xform.Coordinates);
                }
                else if (!isLateJoin &&
                    spawnPoint.SpawnType == SpawnPointType.Job &&
                    (args.Job == null || spawnPoint.Job == args.Job))
                {
                    possiblePositions.Add(xform.Coordinates);
                }
            }
        }

        // 3. Fallback: any spawn point
        if (preferredPositions.Count == 0 && possiblePositions.Count == 0)
        {
            var points2 = EntityQueryEnumerator<SpawnPointComponent, TransformComponent>();
            if (points2.MoveNext(out var _, out var _, out var xform))
            {
                possiblePositions.Add(xform.Coordinates);
            }
            else
            {
                Log.Error("No spawn points were available!");
                return;
            }
        }

        var spawnLoc = preferredPositions.Count > 0
            ? _random.Pick(preferredPositions)
            : _random.Pick(possiblePositions);

        args.SpawnResult = _stationSpawning.SpawnPlayerMob(
            spawnLoc,
            args.Job,
            args.HumanoidCharacterProfile,
            args.Station);
    }
}
