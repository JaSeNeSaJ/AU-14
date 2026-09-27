#nullable enable
using System.Linq;
using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Server.Ghost.Roles.Components;
using Content.Shared._RMC14.Roles;
using Content.Shared._RMC14.Xenonids.Evolution;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.ManageHive.Boons;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Prototypes;

[TestFixture]
public sealed class RolePlaytimeRequirementsTest : GameTest
{
    private static readonly ProtoId<JobPrototype> Administrator = "AU14JobCivilianColonyAdministrator";

    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task LoadedRolesHaveNoPlaytimeGatesAndKeepCharacterRequirements()
    {
        await Server.WaitAssertion(() =>
        {
            var roles = SEntMan.System<SharedRoleSystem>();
            Assert.Multiple(() =>
            {
                foreach (var job in SProtoMan.EnumeratePrototypes<JobPrototype>())
                    AssertNoPlaytimeGate(job.ID, roles.GetRoleRequirements(job));
                foreach (var antag in SProtoMan.EnumeratePrototypes<AntagPrototype>())
                    AssertNoPlaytimeGate(antag.ID, roles.GetRoleRequirements(antag));
                foreach (var overrides in SProtoMan.EnumeratePrototypes<JobRequirementOverridePrototype>())
                {
                    foreach (var (job, requirements) in overrides.Jobs)
                        AssertNoPlaytimeGate($"{overrides.ID}/{job}", requirements);
                    foreach (var (antag, requirements) in overrides.Antags)
                        AssertNoPlaytimeGate($"{overrides.ID}/{antag}", requirements);
                }
                foreach (var (proto, ghost) in Pair.GetPrototypesWithComponent<GhostRoleComponent>())
                    AssertNoPlaytimeGate(proto.ID, ghost.Requirements);
                foreach (var humanoid in SProtoMan.EnumeratePrototypes<RandomHumanoidSettingsPrototype>())
                {
                    if (humanoid.Components?.TryGetComponent("GhostRole", out var ghost) == true)
                        AssertNoPlaytimeGate(humanoid.ID, ((GhostRoleComponent) ghost).Requirements);
                }
            });

            var administrator = SProtoMan.Index(Administrator);
            var profile = HumanoidCharacterProfile.DefaultWithSpecies().WithAge(30);
            var noPlaytime = new Dictionary<string, TimeSpan>();
            Assert.That(JobRequirements.TryRequirementsMet(administrator, noPlaytime, out _,
                SEntMan, SProtoMan, profile), Is.True, "A new player can meet the role's character requirements.");
            Assert.That(JobRequirements.TryRequirementsMet(administrator, noPlaytime, out _,
                SEntMan, SProtoMan, profile.WithAge(18)), Is.False, "The character age requirement still applies.");
            Assert.That(administrator.Whitelisted, Is.True, "The command whitelist remains required.");
        });
    }

    [Test]
    public async Task NewPlayerCanReceiveKingVotesButHiveAndCasteRulesStillApply()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var hive = SEntMan.SpawnEntity("CMUAlphaHive", map.GridCoords);
            var otherHive = SEntMan.SpawnEntity("CMUCorruptedHive", map.GridCoords);
            var candidate = SEntMan.SpawnEntity("CMXenoRunner", map.GridCoords);
            var cocoon = SEntMan.SpawnEntity(null, map.GridCoords);
            SEntMan.AddComponent<HiveKingCocoonComponent>(cocoon);
            var hives = SEntMan.System<SharedXenoHiveSystem>();
            hives.SetHive(cocoon, hive);
            hives.SetHive(candidate, otherHive);
            Server.PlayerMan.SetAttachedEntity(ServerSession!, candidate);
            var vote = SEntMan.System<HiveBoonSystem>().EnsureVote(cocoon);

            void Vote() => SEntMan.EventBus.RaiseLocalEvent(candidate,
                new HiveKingVoteDialogEvent(SEntMan.GetNetEntity(cocoon), SEntMan.GetNetEntity(candidate)));

            Vote();
            Assert.That(vote.Comp.Votes, Is.Empty, "Other hives cannot vote for this King.");
            hives.SetHive(candidate, hive);
#pragma warning disable RA0002 // Model a caste that can vote but cannot become King.
            SEntMan.AddComponent(candidate, new ExcludedFromKingVoteComponent { CanVote = true, CanBeKing = false });
#pragma warning restore RA0002
            Vote();
            Assert.That(vote.Comp.Votes, Is.Empty, "Excluded castes cannot become King.");
            SEntMan.RemoveComponent<ExcludedFromKingVoteComponent>(candidate);

            Vote();
            Assert.That(vote.Comp.Votes.GetValueOrDefault(ServerSession!.UserId), Is.EqualTo(1),
                "A fresh player must be eligible without five hours of xeno playtime.");
        });
    }

    [Test]
    public async Task NewPlayerCanBecomeCorruptedQueenButCannotReplaceALivingQueen()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var hive = SEntMan.SpawnEntity("CMUCorruptedHive", map.GridCoords);
            var drone = SEntMan.SpawnEntity("CMXenoDrone", map.GridCoords);
            var hives = SEntMan.System<SharedXenoHiveSystem>();
            hives.SetHive(drone, hive);
            Server.PlayerMan.SetAttachedEntity(ServerSession!, drone);
            var evolution = SEntMan.GetComponent<XenoEvolutionComponent>(drone);
            var system = SEntMan.System<XenoEvolutionSystem>();
            var canEvolve = typeof(XenoEvolutionSystem).GetMethod("CanEvolvePopup", BindingFlags.Instance | BindingFlags.NonPublic)!;
            bool CanBecomeQueen() => (bool) canEvolve.Invoke(system,
                [new Entity<XenoEvolutionComponent>(drone, evolution), new EntProtoId("CMXenoQueen"), false])!;

            Assert.That(CanBecomeQueen(), Is.True,
                "A fresh player must be eligible without thirty hours of xeno playtime.");
            var queen = SEntMan.SpawnEntity("CMXenoQueen", map.GridCoords);
            hives.SetHive(queen, hive);
            Assert.That(CanBecomeQueen(), Is.False, "A living Queen still prevents another Queen evolving.");
        });
    }

    private static void AssertNoPlaytimeGate(string role, IEnumerable<JobRequirement>? requirements)
    {
        Assert.That(requirements?.Where(requirement => requirement is RoleTimeRequirement or
            DepartmentTimeRequirement or OverallPlaytimeRequirement or TotalJobsTimeRequirement or
            TotalDepartmentsTimeRequirement).Select(requirement => requirement.GetType().Name),
            Is.Null.Or.Empty, $"{role} still requires playtime to join.");
    }
}
