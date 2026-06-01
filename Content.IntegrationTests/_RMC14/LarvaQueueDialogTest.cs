using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Pair;
using Content.Server._RMC14.Xenonids.Parasite;
using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Shared._RMC14.Dialog;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.JoinXeno;
using Content.Shared.Ghost;
using Content.Shared.Mind;
using Content.Shared.Tag;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._RMC14;

[TestFixture]
public sealed class LarvaQueueJoinXenoUiTest
{
    private static readonly ProtoId<TagPrototype> LarvaTag = "RMCXenoLarva";

    [Test]
    public async Task LarvaQueueOffersGhostedXenoBeforeLarva()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
            DummyTicker = false,
        });

        var server = pair.Server;
        var map = await pair.CreateTestMap();

        var entMan = server.EntMan;
        var hiveSystem = entMan.System<SharedXenoHiveSystem>();
        var mind = entMan.System<MindSystem>();
        var tags = entMan.System<TagSystem>();
        var player = server.PlayerMan.Sessions.Single();

        EntityUid ghost = default;
        EntityUid hive = default;
        EntityUid runner = default;
        EntityUid larva = default;
        NetEntity ghostNet = default;
        string runnerName = string.Empty;
        await server.WaitAssertion(() =>
        {
            ghost = entMan.SpawnEntity(GameTicker.ObserverPrototypeName, map.GridCoords);
            hive = entMan.SpawnEntity("CMXenoHive", map.GridCoords.Offset(new Vector2(1, 0)));
            runner = entMan.SpawnEntity("CMXenoRunner", map.GridCoords.Offset(new Vector2(2, 0)));
            tags.AddTag(runner, LarvaTag);
            hiveSystem.SetHive(runner, hive);
            larva = entMan.SpawnEntity("CMXenoLarva", map.GridCoords.Offset(new Vector2(3, 0)));
            hiveSystem.SetHive(larva, hive);
            runnerName = entMan.GetComponent<MetaDataComponent>(runner).EntityName;

            var mindId = mind.CreateMind(player.UserId, "Observer");
            mind.TransferTo(mindId, ghost);
            mind.SetUserId(mindId, player.UserId);
            ghostNet = entMan.GetNetEntity(ghost);

            entMan.EventBus.RaiseLocalEvent(ghost, new JoinLarvaQueueEvent(entMan.GetNetEntity(hive)));
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(player.AttachedEntity, Is.EqualTo(ghost));
            AssertConfirmDialog(entMan, ghost, runnerName);
        });

        await ConfirmDialog(pair, ghostNet);
        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(player.AttachedEntity, Is.EqualTo(runner));
            Assert.That(player.AttachedEntity, Is.Not.EqualTo(larva));
            Assert.That(mind.TryGetMind(player.UserId, out _, out var mindComp), Is.True);
            Assert.That(mindComp!.CurrentEntity, Is.EqualTo(runner));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LarvaQueueClaimsGhostedAdultWhenNoLarvaAvailable()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
            DummyTicker = false,
        });

        var server = pair.Server;
        var map = await pair.CreateTestMap();

        var entMan = server.EntMan;
        var hiveSystem = entMan.System<SharedXenoHiveSystem>();
        var mind = entMan.System<MindSystem>();
        var player = server.PlayerMan.Sessions.Single();

        EntityUid ghost = default;
        EntityUid hive = default;
        EntityUid runner = default;
        NetEntity ghostNet = default;
        string runnerName = string.Empty;
        await server.WaitAssertion(() =>
        {
            ghost = entMan.SpawnEntity(GameTicker.ObserverPrototypeName, map.GridCoords);
            hive = entMan.SpawnEntity("CMXenoHive", map.GridCoords.Offset(new Vector2(1, 0)));
            runner = entMan.SpawnEntity("CMXenoRunner", map.GridCoords.Offset(new Vector2(2, 0)));
            hiveSystem.SetHive(runner, hive);
            runnerName = entMan.GetComponent<MetaDataComponent>(runner).EntityName;

            var mindId = mind.CreateMind(player.UserId, "Observer");
            mind.TransferTo(mindId, ghost);
            mind.SetUserId(mindId, player.UserId);
            ghostNet = entMan.GetNetEntity(ghost);

            entMan.EventBus.RaiseLocalEvent(ghost, new JoinLarvaQueueEvent(entMan.GetNetEntity(hive)));
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(player.AttachedEntity, Is.EqualTo(ghost));
            AssertConfirmDialog(entMan, ghost, runnerName);
        });

        await ConfirmDialog(pair, ghostNet);
        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(player.AttachedEntity, Is.EqualTo(runner));
            Assert.That(mind.TryGetMind(player.UserId, out _, out var mindComp), Is.True);
            Assert.That(mindComp!.CurrentEntity, Is.EqualTo(runner));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LarvaQueueConfirmationTimeoutRemovesQueueSpot()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
            DummyTicker = false,
        });

        var server = pair.Server;
        var map = await pair.CreateTestMap();

        var entMan = server.EntMan;
        var hiveSystem = entMan.System<SharedXenoHiveSystem>();
        var mind = entMan.System<MindSystem>();
        var player = server.PlayerMan.Sessions.Single();

        EntityUid ghost = default;
        EntityUid hive = default;
        EntityUid runner = default;
        string runnerName = string.Empty;
        await server.WaitAssertion(() =>
        {
            ghost = entMan.SpawnEntity(GameTicker.ObserverPrototypeName, map.GridCoords);
            hive = entMan.SpawnEntity("CMXenoHive", map.GridCoords.Offset(new Vector2(1, 0)));
            runner = entMan.SpawnEntity("CMXenoRunner", map.GridCoords.Offset(new Vector2(2, 0)));
            hiveSystem.SetHive(runner, hive);
            runnerName = entMan.GetComponent<MetaDataComponent>(runner).EntityName;

            var mindId = mind.CreateMind(player.UserId, "Observer");
            mind.TransferTo(mindId, ghost);
            mind.SetUserId(mindId, player.UserId);

            entMan.EventBus.RaiseLocalEvent(ghost, new JoinLarvaQueueEvent(entMan.GetNetEntity(hive)));
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(player.AttachedEntity, Is.EqualTo(ghost));
            AssertConfirmDialog(entMan, ghost, runnerName);
        });

        await pair.RunSeconds(31);

        await server.WaitAssertion(() =>
        {
            Assert.That(player.AttachedEntity, Is.EqualTo(ghost));
            Assert.That(entMan.HasComponent<DialogComponent>(ghost), Is.False);

            OpenJoinXenoUi(entMan, ghost);
            var state = GetJoinXenoState(entMan, ghost);
            var entry = state.Entries.Single(e => e.Hive == entMan.GetNetEntity(hive));
            Assert.That(entry.Status, Is.EqualTo(JoinXenoQueueStatus.NotQueued));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ParasiteRoleCanBeTakenImmediatelyByGhost()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
            DummyTicker = false,
        });

        var server = pair.Server;
        var map = await pair.CreateTestMap();

        var entMan = server.EntMan;
        var parasiteRoles = entMan.System<XenoEggRoleSystem>();

        EntityUid ghost = default;
        await server.WaitAssertion(() =>
        {
            ghost = entMan.SpawnEntity(GameTicker.ObserverPrototypeName, map.GridCoords);
            Assert.That(entMan.HasComponent<GhostComponent>(ghost), Is.True);
            Assert.That(parasiteRoles.UserCheck(ghost), Is.True);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task JoinXenoUiShowsJoinOrLeaveWithQueuePosition()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
            DummyTicker = false,
        });

        var server = pair.Server;
        var map = await pair.CreateTestMap();

        var entMan = server.EntMan;
        var mind = entMan.System<MindSystem>();
        var player = server.PlayerMan.Sessions.Single();

        EntityUid ghost = default;
        EntityUid hive = default;

        await server.WaitAssertion(() =>
        {
            ghost = entMan.SpawnEntity(GameTicker.ObserverPrototypeName, map.GridCoords);
            hive = entMan.SpawnEntity("CMXenoHive", map.GridCoords.Offset(new Vector2(1, 0)));

            var mindId = mind.CreateMind(player.UserId, "Observer");
            mind.TransferTo(mindId, ghost);
            mind.SetUserId(mindId, player.UserId);

            Assert.That(entMan.HasComponent<GhostComponent>(ghost), Is.True);
            OpenJoinXenoUi(entMan, ghost);

            var state = GetJoinXenoState(entMan, ghost);
            var entry = state.Entries.Single(e => e.Hive == entMan.GetNetEntity(hive));
            Assert.That(entry.Status, Is.EqualTo(JoinXenoQueueStatus.NotQueued));
            Assert.That(entry.Position, Is.EqualTo(0));
        });

        await server.WaitAssertion(() =>
        {
            entMan.EventBus.RaiseLocalEvent(ghost, new JoinLarvaQueueEvent(entMan.GetNetEntity(hive)));
            OpenJoinXenoUi(entMan, ghost);

            var state = GetJoinXenoState(entMan, ghost);
            var entry = state.Entries.Single(e => e.Hive == entMan.GetNetEntity(hive));
            Assert.That(entry.Status, Is.EqualTo(JoinXenoQueueStatus.Queued));
            Assert.That(entry.Position, Is.EqualTo(1));
        });

        await pair.CleanReturnAsync();
    }

    private static void OpenJoinXenoUi(IEntityManager entMan, EntityUid ghost)
    {
        var ev = new JoinXenoActionEvent
        {
            Performer = ghost,
        };

        entMan.EventBus.RaiseLocalEvent(ghost, ev);
    }

    private static void AssertConfirmDialog(IEntityManager entMan, EntityUid ghost, string xenoName)
    {
        Assert.That(entMan.TryGetComponent<DialogComponent>(ghost, out var dialog), Is.True);
        Assert.That(dialog!.Title, Is.EqualTo("Join as Xeno"));
        Assert.That(dialog.Message.Text, Does.Contain(xenoName));
        Assert.That(dialog.Options.Select(o => o.Text), Is.EqualTo(new[] { "Click here to confirm", "Decline" }));
    }

    private static async Task ConfirmDialog(TestPair pair, NetEntity ghostNet)
    {
        await pair.Client.WaitAssertion(() =>
        {
            var clientEntMan = pair.Client.EntMan;
            var clientGhost = clientEntMan.GetEntity(ghostNet);
            Assert.That(clientEntMan.TryGetComponent<UserInterfaceComponent>(clientGhost, out var ui), Is.True);
            Assert.That(ui!.ClientOpenInterfaces.ContainsKey(DialogUiKey.Key), Is.True);
        });

        await pair.Client.WaitPost(() =>
        {
            var clientEntMan = pair.Client.EntMan;
            var clientGhost = clientEntMan.GetEntity(ghostNet);
            var ui = clientEntMan.GetComponent<UserInterfaceComponent>(clientGhost);
            ui.ClientOpenInterfaces[DialogUiKey.Key].SendPredictedMessage(new DialogOptionBuiMsg(0));
        });
    }

    private static JoinXenoBuiState GetJoinXenoState(IEntityManager entMan, EntityUid ghost)
    {
        var ui = entMan.System<SharedUserInterfaceSystem>();
        Assert.That(ui.TryGetUiState<JoinXenoBuiState>(ghost, JoinXenoUIKey.Key, out var state), Is.True);
        return (JoinXenoBuiState) state!;
    }
}
