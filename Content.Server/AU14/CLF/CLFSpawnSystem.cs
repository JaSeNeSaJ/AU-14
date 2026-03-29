using Content.Server.GameTicking;
using Content.Server.GameTicking;
using Content.Server.Spawners.EntitySystems;
using Content.Server.Station.Systems;
using Content.Shared.AU14;
using Content.Shared.GameTicking;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.AU14.CLF;

/// <summary>
/// Handles CLF spawning at round start (at a chosen safehouse) and additional entity spawning
/// </summary>
public sealed class ClfSpawnSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly StationSpawningSystem _stationSpawning = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;

    private EntityCoordinates? _chosenSafehouseLocation;
    private bool _hasSpawnedAdditionalEntities;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<PlayerSpawningEvent>(OnPlayerSpawning, before: new[] { typeof(SpawnPointSystem) });
        SubscribeLocalEvent<RulePlayerSpawningEvent>(OnRulePlayerSpawning);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _chosenSafehouseLocation = null;
        _hasSpawnedAdditionalEntities = false;
    }

    private void OnRulePlayerSpawning(RulePlayerSpawningEvent ev)
    {
        // After all players have been assigned jobs, spawn additional entities if CLF players exist
        if (_chosenSafehouseLocation != null && !_hasSpawnedAdditionalEntities)
        {
            SpawnAdditionalEntities();
            _hasSpawnedAdditionalEntities = true;
        }
    }

    private void OnPlayerSpawning(PlayerSpawningEvent args)
    {
        // Only handle CLF jobs
        string? jobId = args.Job?.ToString();
        if (string.IsNullOrEmpty(jobId) || !jobId.Contains("CLF", StringComparison.OrdinalIgnoreCase))
            return;

        // Late join CLF should use normal latejoin points (don't handle here)
        if (args.SpawnResult != null)
            return;

        // Only handle round start spawning (late join handled by normal SpawnPointSystem)
        var gameTicker = EntityManager.System<GameTicker>();
        if (gameTicker.RunLevel == GameRunLevel.InRound)
            return;

        // Choose safehouse location if not already chosen
        if (_chosenSafehouseLocation == null)
        {
            var safehouseMarkers = new List<EntityUid>();
            var query = EntityQueryEnumerator<SafehouseMarkerComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out var _, out var _))
            {
                safehouseMarkers.Add(uid);
            }

            if (safehouseMarkers.Count > 0)
            {
                var chosenMarker = _random.Pick(safehouseMarkers);
                _chosenSafehouseLocation = Transform(chosenMarker).Coordinates;
                Log.Info($"CLF Spawn System: Chose safehouse marker {chosenMarker} at {_chosenSafehouseLocation}");

                // Spawn additional entities now that we have chosen a location
                SpawnAdditionalEntities();
                _hasSpawnedAdditionalEntities = true;
            }
            else
            {
                Log.Warning("CLF Spawn System: No SafehouseMarker found for CLF spawning!");
                return;
            }
        }

        // Spawn the player at the chosen safehouse
        args.SpawnResult = _stationSpawning.SpawnPlayerMob(
            _chosenSafehouseLocation.Value,
            args.Job,
            args.HumanoidCharacterProfile,
            args.Station);
    }

    private void SpawnAdditionalEntities()
    {
        if (_chosenSafehouseLocation == null)
            return;

        // Get CLF spawn config
        if (!_prototypeManager.TryIndex<CLFSpawnConfigPrototype>("CLFSpawnConfig", out var config))
        {
            Log.Info("CLF Spawn System: No CLFSpawnConfig found, skipping additional entity spawning");
            return;
        }

        // Spawn each configured entity at the chosen safehouse
        foreach (var protoId in config.additionalItems)
        {
            try
            {
                _entityManager.SpawnEntity(protoId, _chosenSafehouseLocation.Value);
                Log.Info($"CLF Spawn System: Spawned additional entity {protoId} at safehouse");
            }
            catch (Exception ex)
            {
                Log.Error($"CLF Spawn System: Failed to spawn entity {protoId}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Gets the chosen safehouse location for the current round (if any)
    /// </summary>
    public EntityCoordinates? GetChosenSafehouse() => _chosenSafehouseLocation;
}



