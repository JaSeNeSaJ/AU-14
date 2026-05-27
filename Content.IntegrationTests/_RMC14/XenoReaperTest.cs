using System.Numerics;
using System.Collections.Generic;
using Content.Shared._RMC14.Actions;
using Content.Server.Body.Systems;
using Content.Shared._RMC14.Medical.Unrevivable;
using Content.Shared._RMC14.Shields;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Construction;
using Content.Shared._RMC14.Xenonids.Egg;
using Content.Shared._RMC14.Xenonids.Evolution;
using Content.Shared._RMC14.Xenonids.Reaper;
using Content.Shared.Actions.Components;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Damage;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Traits.Assorted;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests._RMC14;

[TestFixture]
public sealed class XenoReaperTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  parent: CMXenoReaper
  id: RMCTestXenoReaperStocked
  components:
  - type: XenoReaper
    fleshResin: 1000
";

    [Test]
    public async Task ReaperIsCarrierStrainWithDroneConstruction()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var reaper = entMan.SpawnEntity("CMXenoReaper", map.GridCoords);

            try
            {
                var xeno = entMan.GetComponent<XenoComponent>(reaper);
                var devolve = entMan.GetComponent<XenoDevolveComponent>(reaper);
                Assert.That(entMan.TryGetComponent<XenoConstructionComponent>(reaper, out var construction), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(xeno.Role.Id, Is.EqualTo("CMXenoCarrier"));
                    Assert.That(devolve.DevolvesTo, Is.EqualTo(new[] { "CMXenoCarrier" }));
                    Assert.That(construction!.BuildDelay, Is.EqualTo(TimeSpan.FromSeconds(2)));
                    Assert.That(construction.CanBuild, Does.Contain("WallXenoResin"));
                    Assert.That(construction.CanBuild, Does.Contain("WallXenoMembrane"));
                    Assert.That(construction.CanBuild, Does.Contain("DoorXenoResin"));
                    Assert.That(construction.CanBuild, Does.Contain("XenoStickyResin"));
                    Assert.That(construction.CanBuild, Does.Contain("XenoFastResin"));
                    Assert.That(construction.CanBuild, Does.Not.Contain("WallXenoResinThick"));
                    Assert.That(construction.CanBuild, Does.Not.Contain("HiveAcidPillarXeno"));
                    Assert.That(construction.CanOrderConstruction, Does.Contain("HiveCoreXenoConstructionNode"));
                    Assert.That(construction.CanUpgrade, Is.False);
                    Assert.That(entMan.HasComponent<XenoEggRetrieverComponent>(reaper), Is.False);
                });
            }
            finally
            {
                entMan.DeleteEntity(reaper);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ReaperEvolvesFromCarrierNotHivelord()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var carrier = entMan.SpawnEntity("CMXenoCarrier", map.GridCoords);
            var hivelord = entMan.SpawnEntity("CMXenoHivelord", map.GridCoords.Offset(new Vector2(1, 0)));

            try
            {
                var carrierEvolution = entMan.GetComponent<XenoEvolutionComponent>(carrier);
                var hivelordEvolution = entMan.GetComponent<XenoEvolutionComponent>(hivelord);

                Assert.Multiple(() =>
                {
                    Assert.That(carrierEvolution.EvolvesTo, Does.Contain("CMXenoReaper"));
                    Assert.That(hivelordEvolution.EvolvesTo, Does.Not.Contain("CMXenoReaper"));
                });
            }
            finally
            {
                entMan.DeleteEntity(carrier);
                entMan.DeleteEntity(hivelord);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FleshHarvestRejectsDeadMarineThatIsNotPermaDead()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var mob = entMan.System<MobStateSystem>();
            var reaper = entMan.SpawnEntity("CMXenoReaper", map.GridCoords);
            var marine = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            var action = SpawnAction(entMan);

            try
            {
                mob.ChangeMobState(marine, MobState.Dead);
                var ev = RaiseFleshHarvest(entMan, reaper, marine, action);

                Assert.That(ev.Handled, Is.False);
            }
            finally
            {
                entMan.DeleteEntity(reaper);
                entMan.DeleteEntity(marine);
                entMan.DeleteEntity(action.Owner);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FleshHarvestRemovesPermaDeadMarineLimbs()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid reaper = default;
        EntityUid marine = default;
        Entity<ActionComponent> action = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var mob = entMan.System<MobStateSystem>();
            reaper = entMan.SpawnEntity("CMXenoReaper", map.GridCoords);
            marine = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            action = SpawnAction(entMan);

            mob.ChangeMobState(marine, MobState.Dead);
            entMan.EnsureComponent<UnrevivableComponent>(marine);

            var before = CountLimbs(entMan, marine);
            Assert.That(before, Is.GreaterThan(0));

            var ev = RaiseFleshHarvest(entMan, reaper, marine, action);
            Assert.That(ev.Handled, Is.True);
        });

        await server.WaitRunTicks(300);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            Assert.That(CountLimbs(entMan, marine), Is.Zero);

            entMan.DeleteEntity(reaper);
            entMan.DeleteEntity(marine);
            entMan.DeleteEntity(action.Owner);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FleshBloomActionHasSevenTileRange()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var action = entMan.SpawnEntity("ActionXenoFleshBloom", MapCoordinates.Nullspace);

            try
            {
                var target = entMan.GetComponent<TargetActionComponent>(action);
                var range = entMan.GetComponent<ActionInRangeUnobstructedComponent>(action);

                Assert.Multiple(() =>
                {
                    Assert.That(target.Range, Is.EqualTo(7));
                    Assert.That(range.Range, Is.EqualTo(7));
                });
            }
            finally
            {
                entMan.DeleteEntity(action);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FleshBloomUsesDelayedThreeByThreeToxinBloom()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid reaper = default;
        var targets = new List<EntityUid>();
        Entity<ActionComponent> action = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var center = map.GridCoords.Offset(new Vector2(2, 0));
            reaper = entMan.SpawnEntity("RMCTestXenoReaperStocked", map.GridCoords);
            for (var x = -1; x <= 1; x++)
            {
                for (var y = -1; y <= 1; y++)
                {
                    targets.Add(entMan.SpawnEntity("CMMobHuman", center.Offset(new Vector2(x, y))));
                }
            }

            action = SpawnAction(entMan);

            RaiseFleshBloom(entMan, reaper, center, action);

            Assert.That(CountBloomEntities(entMan), Is.Zero);
        });

        await server.WaitRunTicks(90);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var blooms = GetBlooms(entMan);

            Assert.That(blooms, Has.Count.EqualTo(9));
            Assert.Multiple(() =>
            {
                Assert.That(blooms, Has.All.Matches<Entity<XenoFleshBloomComponent>>(bloom => bloom.Comp.Range <= 0.5f));
                Assert.That(CountTelegraphs(entMan), Is.EqualTo(9));
            });

            foreach (var target in targets)
            {
                var damage = entMan.GetComponent<DamageableComponent>(target).Damage.DamageDict;
                Assert.Multiple(() =>
                {
                    Assert.That(damage.TryGetValue("Poison", out var poison) && poison > 0, Is.True);
                    Assert.That(!damage.TryGetValue("Cellular", out var cellular) || cellular <= 0, Is.True);
                });
            }

            entMan.DeleteEntity(reaper);
            foreach (var target in targets)
            {
                entMan.DeleteEntity(target);
            }

            entMan.DeleteEntity(action.Owner);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RaptureSpawnsHitEffect()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var reaper = entMan.SpawnEntity("CMXenoReaper", map.GridCoords);
            var target = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            var action = SpawnAction(entMan);

            try
            {
                var ev = RaiseRapture(entMan, reaper, target, action);

                Assert.Multiple(() =>
                {
                    Assert.That(ev.Handled, Is.True);
                    Assert.That(CountPrototype(entMan, "RMCEffectExtraSlash"), Is.EqualTo(1));
                });
            }
            finally
            {
                entMan.DeleteEntity(reaper);
                entMan.DeleteEntity(target);
                entMan.DeleteEntity(action.Owner);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CarrionMantleAppliesKingShield()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var reaper = entMan.SpawnEntity("RMCTestXenoReaperStocked", map.GridCoords);
            var action = SpawnAction(entMan);

            try
            {
                RaiseCarrionMantle(entMan, reaper, reaper, action);

                Assert.That(entMan.TryGetComponent<XenoShieldComponent>(reaper, out var shield), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(shield!.Active, Is.True);
                    Assert.That(shield.Shield, Is.EqualTo(XenoShieldSystem.ShieldType.King));
                    Assert.That(entMan.HasComponent<KingShieldComponent>(reaper), Is.True);
                });
            }
            finally
            {
                entMan.DeleteEntity(reaper);
                entMan.DeleteEntity(action.Owner);
            }
        });

        await pair.CleanReturnAsync();
    }

    private static Entity<ActionComponent> SpawnAction(IEntityManager entMan)
    {
        var action = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
        return (action, entMan.EnsureComponent<ActionComponent>(action));
    }

    private static XenoFleshHarvestActionEvent RaiseFleshHarvest(
        IEntityManager entMan,
        EntityUid reaper,
        EntityUid target,
        Entity<ActionComponent> action)
    {
        var ev = new XenoFleshHarvestActionEvent
        {
            Performer = reaper,
            Action = action,
            Target = target,
        };

        entMan.EventBus.RaiseLocalEvent(reaper, ev);
        return ev;
    }

    private static void RaiseFleshBloom(
        IEntityManager entMan,
        EntityUid reaper,
        EntityCoordinates target,
        Entity<ActionComponent> action)
    {
        var ev = new XenoFleshBloomActionEvent
        {
            Performer = reaper,
            Action = action,
            Target = target,
        };

        entMan.EventBus.RaiseLocalEvent(reaper, ev);
    }

    private static XenoRaptureActionEvent RaiseRapture(
        IEntityManager entMan,
        EntityUid reaper,
        EntityUid target,
        Entity<ActionComponent> action)
    {
        var ev = new XenoRaptureActionEvent
        {
            Performer = reaper,
            Action = action,
            Target = target,
        };

        entMan.EventBus.RaiseLocalEvent(reaper, ev);
        return ev;
    }

    private static void RaiseCarrionMantle(
        IEntityManager entMan,
        EntityUid reaper,
        EntityUid target,
        Entity<ActionComponent> action)
    {
        var ev = new XenoCarrionMantleActionEvent
        {
            Performer = reaper,
            Action = action,
            Target = target,
        };

        entMan.EventBus.RaiseLocalEvent(reaper, ev);
    }

    private static int CountLimbs(IEntityManager entMan, EntityUid body)
    {
        var bodySystem = entMan.System<BodySystem>();
        var bodyComp = entMan.GetComponent<BodyComponent>(body);
        var count = 0;

        foreach (var type in new[] { BodyPartType.Arm, BodyPartType.Hand, BodyPartType.Leg, BodyPartType.Foot })
        {
            foreach (var _ in bodySystem.GetBodyChildrenOfType(body, type, bodyComp))
            {
                count++;
            }
        }

        return count;
    }

    private static int CountBloomEntities(IEntityManager entMan)
    {
        return GetBlooms(entMan).Count;
    }

    private static List<Entity<XenoFleshBloomComponent>> GetBlooms(IEntityManager entMan)
    {
        var result = new List<Entity<XenoFleshBloomComponent>>();
        var query = entMan.EntityQueryEnumerator<XenoFleshBloomComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            result.Add((uid, comp));
        }

        return result;
    }

    private static int CountTelegraphs(IEntityManager entMan)
    {
        return CountPrototype(entMan, "RMCEffectReaperFleshBloomTelegraph");
    }

    private static int CountPrototype(IEntityManager entMan, string prototypeId)
    {
        var count = 0;
        var query = entMan.EntityQueryEnumerator<MetaDataComponent>();
        while (query.MoveNext(out _, out var metadata))
        {
            if (metadata.EntityPrototype?.ID == prototypeId)
                count++;
        }

        return count;
    }
}
