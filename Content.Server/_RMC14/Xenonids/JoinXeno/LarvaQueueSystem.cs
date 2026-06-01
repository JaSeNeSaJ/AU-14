using Content.Server.GameTicking;
using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Shared._RMC14.CCVar;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.JoinXeno;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared.GameTicking;
using Content.Shared.Ghost;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._RMC14.Xenonids.JoinXeno;

public sealed partial class LarvaQueueSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private GhostRoleSystem _ghostRole = default!;
    [Dependency] private SharedXenoHiveSystem _hive = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private ISharedPlayerManager _player = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly EntProtoId LesserDrone = "CMXenoLesserDrone";

    private readonly Dictionary<EntityUid, LarvaQueueState> _queues = [];
    private readonly List<EntityUid> _emptyQueues = [];

    private EntityQuery<GhostComponent> _ghostQuery;
    private EntityQuery<HiveComponent> _hiveQuery;

    public override void Initialize()
    {
        _ghostQuery = GetEntityQuery<GhostComponent>();
        _hiveQuery = GetEntityQuery<HiveComponent>();

        SubscribeLocalEvent<JoinXenoComponent, JoinLarvaQueueEvent>(OnJoinLarvaQueue);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnCleanup);
        SubscribeLocalEvent<BurrowedLarvaAddedEvent>(OnBurrowedLarvaAdded);
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<LarvaQueueableComponent, ComponentStartup>(OnQueueableStartup);
        SubscribeLocalEvent<LarvaQueueableComponent, HiveChangedEvent>(OnQueueableHiveChanged);
        SubscribeLocalEvent<LarvaQueueableComponent, MindRemovedMessage>(OnQueueableMindRemoved);
    }

    private void OnCleanup(RoundRestartCleanupEvent ev)
    {
        _queues.Clear();
    }

    private void OnJoinLarvaQueue(Entity<JoinXenoComponent> ent, ref JoinLarvaQueueEvent args)
    {
        if (!TryComp(ent, out ActorComponent? actor) ||
            !TryComp(ent, out GhostComponent? ghost) ||
            !TryGetEntity(args.Hive, out var hiveUid) ||
            hiveUid is not { Valid: true } hiveId ||
            !_hiveQuery.TryComp(hiveId, out var hiveComp))
        {
            return;
        }

        if (!CanUseQueue(ent, ghost))
            return;

        var userId = actor.PlayerSession.UserId;
        var actorEntity = actor.PlayerSession.AttachedEntity ?? ent.Owner;
        var queue = QueueFor(hiveId);

        if (queue.Remove(userId))
        {
            _popup.PopupEntity(Loc.GetString("rmc-xeno-larva-queue-removed"), actorEntity, actorEntity);
            RemoveIfEmpty(hiveId);
            return;
        }

        RemoveFromAllQueues(userId, hiveId);

        var wait = TimeSpan.FromSeconds(_config.GetCVar(RMCCVars.RMCLarvaQueueWaitSeconds));
        if (HasComp<JoinXenoCooldownIgnoreComponent>(ent) || _timing.CurTime - ghost.TimeOfDeath >= wait)
        {
            queue.AddReady(userId);
            _popup.PopupEntity(
                Loc.GetString("rmc-xeno-larva-queue-added", ("position", queue.ReadyCount)),
                actorEntity,
                actorEntity);

            TryClaimForHive((hiveId, hiveComp));
            return;
        }

        var readyAt = ghost.TimeOfDeath + wait;
        queue.AddWaiting(userId, readyAt);
        _popup.PopupEntity(
            Loc.GetString(
                "rmc-xeno-larva-prequeue-added",
                ("seconds", Math.Max(0, (int) Math.Ceiling((readyAt - _timing.CurTime).TotalSeconds)))),
            actorEntity,
            actorEntity);
    }

    private bool CanUseQueue(EntityUid user, GhostComponent ghost)
    {
        if (HasComp<JoinXenoCooldownIgnoreComponent>(user))
            return true;

        var denyQueuing = _config.GetCVar(RMCCVars.RMCLarvaQueueRoundstartDelaySeconds);
        var remaining = denyQueuing - _gameTicker.RoundDuration().TotalSeconds;
        if (remaining <= 0)
            return true;

        _popup.PopupEntity(
            Loc.GetString("rmc-xeno-larva-queue-round-delay", ("seconds", (int) Math.Ceiling(remaining))),
            user,
            user,
            PopupType.MediumCaution);
        return false;
    }

    private void OnBurrowedLarvaAdded(ref BurrowedLarvaAddedEvent ev)
    {
        if (_hiveQuery.TryComp(ev.Hive, out var hive))
            TryClaimForHive((ev.Hive, hive));
    }

    private void OnPlayerAttached(PlayerAttachedEvent ev)
    {
        if (_ghostQuery.HasComp(ev.Entity))
            return;

        RemoveFromAllQueues(ev.Player.UserId);
    }

    private void OnQueueableStartup(Entity<LarvaQueueableComponent> ent, ref ComponentStartup args)
    {
        TryClaimQueueable(ent.Owner);
    }

    private void OnQueueableHiveChanged(Entity<LarvaQueueableComponent> ent, ref HiveChangedEvent args)
    {
        TryClaimQueueable(ent.Owner);
    }

    private void OnQueueableMindRemoved(Entity<LarvaQueueableComponent> ent, ref MindRemovedMessage args)
    {
        TryClaimQueueable(ent.Owner);
    }

    public override void Update(float frameTime)
    {
        var time = _timing.CurTime;
        _emptyQueues.Clear();

        foreach (var (hiveId, queue) in _queues)
        {
            var promoted = queue.PromoteWaiting(time);
            if (promoted.Count > 0)
            {
                NotifyReadyPositions(hiveId);
                if (_hiveQuery.TryComp(hiveId, out var hive))
                    TryClaimForHive((hiveId, hive));
            }

            if (queue.Empty)
                _emptyQueues.Add(hiveId);
        }

        foreach (var hiveId in _emptyQueues)
        {
            _queues.Remove(hiveId);
        }
    }

    private LarvaQueueState QueueFor(EntityUid hive)
    {
        if (_queues.TryGetValue(hive, out var queue))
            return queue;

        queue = new LarvaQueueState();
        _queues[hive] = queue;
        return queue;
    }

    private bool TryGetQueue(EntityUid hive, out LarvaQueueState queue)
    {
        return _queues.TryGetValue(hive, out queue!);
    }

    private void TryClaimQueueable(EntityUid uid)
    {
        if (TryComp(uid, out HiveMemberComponent? member) &&
            member.Hive is { } hiveId &&
            _hiveQuery.TryComp(hiveId, out var hive))
        {
            TryClaimForHive((hiveId, hive));
        }
    }

    private void TryClaimForHive(Entity<HiveComponent> hive)
    {
        if (!TryGetQueue(hive.Owner, out var queue) || queue.ReadyCount == 0)
            return;

        var claimed = TryClaimQueueableXenos(hive, queue);
        claimed |= TryClaimBurrowedLarva(hive, queue);

        if (claimed)
            NotifyReadyPositions(hive.Owner);

        RemoveIfEmpty(hive.Owner);
    }

    private bool TryClaimQueueableXenos(Entity<HiveComponent> hive, LarvaQueueState queue)
    {
        var claimed = false;
        var query = EntityQueryEnumerator<LarvaQueueableComponent, HiveMemberComponent>();
        while (queue.ReadyCount > 0 && query.MoveNext(out var uid, out _, out var member))
        {
            if (!CanQueueBody(uid, member, hive))
                continue;

            claimed |= TryClaimQueueableXeno(uid, queue);
        }

        return claimed;
    }

    private bool CanQueueBody(EntityUid uid, HiveMemberComponent member, Entity<HiveComponent> hive)
    {
        if (member.Hive != hive.Owner ||
            TerminatingOrDeleted(uid) ||
            HasComp<ActorComponent>(uid) ||
            _mobState.IsDead(uid) ||
            HasComp<XenoParasiteComponent>(uid) ||
            HasComp<DropshipHijackerComponent>(uid) ||
            TryComp(uid, out MindContainerComponent? mind) && mind.HasMind)
        {
            return false;
        }

        return !TryPrototype(uid, out var prototype) || prototype.ID != LesserDrone;
    }

    private bool TryClaimQueueableXeno(EntityUid uid, LarvaQueueState queue)
    {
        while (queue.TryDequeueReady(out var userId))
        {
            if (!TryGetQueuedSession(userId, out var session))
                continue;

            if (TryComp(uid, out GhostRoleComponent? role))
            {
                if (_ghostRole.Takeover(session, role.Identifier))
                    return true;

                queue.AddReadyFirst(userId);
                return false;
            }

            if (TryComp(uid, out MindContainerComponent? mind) && mind.HasMind)
            {
                queue.AddReadyFirst(userId);
                return false;
            }

            var newMind = _mind.CreateMind(session.UserId, MetaData(uid).EntityName);
            _mind.TransferTo(newMind, uid, ghostCheckOverride: true);
            return true;
        }

        return false;
    }

    private bool TryClaimBurrowedLarva(Entity<HiveComponent> hive, LarvaQueueState queue)
    {
        var claimed = false;
        while (hive.Comp.BurrowedLarva > 0 && queue.TryDequeueReady(out var userId))
        {
            if (!TryGetQueuedSession(userId, out var session))
                continue;

            if (!_hive.JoinBurrowedLarva(hive, session))
            {
                queue.AddReadyFirst(userId);
                break;
            }

            claimed = true;
        }

        return claimed;
    }

    private bool TryGetQueuedSession(NetUserId userId, out ICommonSession session)
    {
        if (!_player.TryGetSessionById(userId, out session!))
            return false;

        if (session.AttachedEntity is { } attached && _ghostQuery.HasComp(attached))
            return true;

        RemoveFromAllQueues(userId);
        return false;
    }

    private void NotifyReadyPositions(EntityUid hive)
    {
        if (!TryGetQueue(hive, out var queue))
            return;

        for (var i = 0; i < queue.ReadyUsers.Count; i++)
        {
            var userId = queue.ReadyUsers[i];
            if (!TryGetQueuedSession(userId, out var session) || session.AttachedEntity is not { } attached)
                continue;

            _popup.PopupEntity(
                Loc.GetString("rmc-xeno-larva-queue-position", ("position", i + 1)),
                attached,
                attached);
        }
    }

    private void RemoveFromAllQueues(NetUserId userId, EntityUid? except = null)
    {
        _emptyQueues.Clear();
        foreach (var (hive, queue) in _queues)
        {
            if (hive == except)
                continue;

            queue.Remove(userId);
            if (queue.Empty)
                _emptyQueues.Add(hive);
        }

        foreach (var hive in _emptyQueues)
        {
            _queues.Remove(hive);
        }
    }

    private void RemoveIfEmpty(EntityUid hive)
    {
        if (_queues.TryGetValue(hive, out var queue) && queue.Empty)
            _queues.Remove(hive);
    }
}
