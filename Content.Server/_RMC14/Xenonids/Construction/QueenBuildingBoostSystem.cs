using Content.Server.GameTicking;
using Content.Shared._RMC14.QueenSpawned;
using Content.Shared._RMC14.Xenonids.Construction;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.GameTicking;

namespace Content.Server._RMC14.Xenonids.Construction;

public sealed class QueenBuildingBoostSystem : EntitySystem
{
    [Dependency] private readonly GameTicker _gameTicker = default!;

    private static readonly TimeSpan QueenBoostDuration = TimeSpan.FromMinutes(30);

    private bool _boostExpired;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<QueenSpawnedEvent>(OnQueenSpawned);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
    }

    private void OnQueenSpawned(QueenSpawnedEvent args)
    {
        if (_boostExpired)
            return;

        ApplyQueenBoost(args.Queen);
    }

    private void ApplyQueenBoost(EntityUid queen)
    {
        var construction = EntityManager.System<SharedXenoConstructionSystem>();

        construction.GiveQueenBoost(
            queen,
            1.5f,
            10f);

        Log.Info($"Queen building boost applied to {queen}");
    }

    public override void Update(float frameTime)
    {
        if (_boostExpired)
            return;

        if (_gameTicker.RunLevel != GameRunLevel.InRound)
            return;

        if (_gameTicker.RoundDuration() < QueenBoostDuration)
            return;

        RemoveQueenBoosts();
        _boostExpired = true;
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent args)
    {
        _boostExpired = false;
    }

    private void RemoveQueenBoosts()
    {
        var construction = EntityManager.System<SharedXenoConstructionSystem>();

        var queens = EntityQueryEnumerator<QueenBuildingBoostComponent>();

        while (queens.MoveNext(out var queen, out _))
        {
            construction.RemoveQueenBoost(queen);

            Log.Info($"Removed queen building boost from {queen}");
        }
    }
}
