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

        // AU14: opfor/govfor should prefer mapped job points, then side latejoin points if no mapping exists.
        bool isLateJoin = _gameTicker.RunLevel == GameRunLevel.InRound;
        string? jobId = args.Job?.ToString();
        bool isOpfor = !string.IsNullOrEmpty(jobId) && jobId.Contains("opfor", StringComparison.OrdinalIgnoreCase);
        bool isGovfor = !string.IsNullOrEmpty(jobId) && jobId.Contains("govfor", StringComparison.OrdinalIgnoreCase);

        // 1. For opfor/govfor, look for mapped faction job spawn points first.
        // Intentionally do not filter by args.Station here: faction jobs can spawn on attached ship stations.
        if (isOpfor || isGovfor)
        {
            var points = EntityQueryEnumerator<SpawnPointComponent, TransformComponent>();
            while (points.MoveNext(out var _, out var spawnPoint, out var xform))
            {
                if ((spawnPoint.SpawnType == SpawnPointType.Job || spawnPoint.SpawnType == SpawnPointType.Unset) &&
                    (args.Job == null || spawnPoint.Job == args.Job))
                {
                    preferredPositions.Add(xform.Coordinates);
                }
            }
        }

        // 2. If there are no mapped job points, use faction-specific latejoin points (ready + latejoin).
        if (preferredPositions.Count == 0 && (isOpfor || isGovfor))
        {
            var points = EntityQueryEnumerator<SpawnPointComponent, TransformComponent>();
            while (points.MoveNext(out var _, out var spawnPoint, out var xform))
            {
                if (isOpfor && spawnPoint.SpawnType == SpawnPointType.LateJoinOpfor)
                {
                    possiblePositions.Add(xform.Coordinates);
                }
                else if (isGovfor && spawnPoint.SpawnType == SpawnPointType.LateJoinGovfor)
                {
                    possiblePositions.Add(xform.Coordinates);
                }
            }
        }


        if (!isOpfor && !isGovfor && isLateJoin)
        {
            var points = EntityQueryEnumerator<SpawnPointComponent, TransformComponent>();
            while (points.MoveNext(out var uid, out var spawnPoint, out var xform))
            {
                // Keep spawnpoints restricted to the target station for colonists
                if (args.Station != null && _stationSystem.GetOwningStation(uid, xform) != args.Station)
                    continue;

                if (spawnPoint.SpawnType == SpawnPointType.Job && (args.Job == null || spawnPoint.Job == args.Job))
                {
                    preferredPositions.Add(xform.Coordinates);
                }
            }
        }

        // 3. Fall back to normal station spawnpoint behavior.
        if (preferredPositions.Count == 0 && possiblePositions.Count == 0)
        {
            var points = EntityQueryEnumerator<SpawnPointComponent, TransformComponent>();
            while (points.MoveNext(out var uid, out var spawnPoint, out var xform))
            {
                if (args.Station != null && _stationSystem.GetOwningStation(uid, xform) != args.Station)
                    continue;

                if (isLateJoin && spawnPoint.SpawnType == SpawnPointType.LateJoin)
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

        // 4. Last resort: any spawn point.
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
