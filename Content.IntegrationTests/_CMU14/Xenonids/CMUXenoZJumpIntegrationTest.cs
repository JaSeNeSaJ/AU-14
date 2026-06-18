using System.Numerics;
using Content.IntegrationTests.Pair;
using Content.Shared._CMU14.Xenonids.ZJump;
using Content.Shared._CMU14.ZLevels.Core.Components;
using Content.Shared._RMC14.Xenonids.Plasma;
using Content.Shared.Actions.Components;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.StatusEffect;
using Robust.Shared.Map;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests._CMU14.Xenonids;

[TestFixture]
public sealed class CMUXenoZJumpIntegrationTest
{
    [Test]
    public async Task XenoReceivesZJumpActionOnMapInit()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var xeno = entMan.SpawnEntity("CMXenoRunner", map.GridCoords);

            try
            {
                Assert.That(HasAction(entMan, xeno, "CMUActionXenoZJump"), Is.True);
            }
            finally
            {
                entMan.DeleteEntity(xeno);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ZJumpFallingBackToLaunchZLevelStunsAndDamagesXeno()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var (lower, upper) = await CreateLinkedMaps(pair);

        EntityUid xeno = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var map = server.System<SharedMapSystem>();
            var plasma = entMan.System<XenoPlasmaSystem>();

            map.SetTile(upper.Grid.Owner, upper.Grid.Comp, upper.GridCoords, Tile.Empty);

            xeno = entMan.SpawnEntity("CMXenoRunner", lower.GridCoords);
            var plasmaComp = entMan.GetComponent<XenoPlasmaComponent>(xeno);
            plasma.SetPlasma((xeno, plasmaComp), plasmaComp.MaxPlasma);

            var target = lower.GridCoords.Offset(new Vector2(0.5f, 0));
            var ev = new CMUXenoZJumpDoAfterEvent(entMan.GetNetCoordinates(target));
            entMan.EventBus.RaiseLocalEvent(xeno, ev);
        });

        await server.WaitRunTicks(120);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var status = entMan.System<StatusEffectQuerySystem>();
            var xform = entMan.GetComponent<TransformComponent>(xeno);
            var damage = entMan.GetComponent<DamageableComponent>(xeno);

            Assert.That(xform.MapUid, Is.EqualTo(lower.MapUid));
            Assert.That(status.HasStatusEffect(xeno, "Stun"), Is.True);
            Assert.That(damage.TotalDamage, Is.GreaterThan(FixedPoint2.Zero));
        });

        await pair.CleanReturnAsync();
    }

    private static async Task<(TestMapData Lower, TestMapData Upper)> CreateLinkedMaps(TestPair pair)
    {
        var lower = await pair.CreateTestMap(tile: "Plating");
        var upper = await pair.CreateTestMap(tile: "Plating");

        await pair.Server.WaitAssertion(() =>
        {
            var entMan = pair.Server.EntMan;
            var lowerZ = entMan.EnsureComponent<CMUZLevelMapComponent>(lower.MapUid);
            lowerZ.MapAbove = upper.MapUid;
            lowerZ.MapBelow = null;
            lowerZ.Depth = 0;
            lowerZ.NetworkUid = EntityUid.Invalid;
            entMan.Dirty(lower.MapUid, lowerZ);

            var upperZ = entMan.EnsureComponent<CMUZLevelMapComponent>(upper.MapUid);
            upperZ.MapAbove = null;
            upperZ.MapBelow = lower.MapUid;
            upperZ.Depth = 1;
            upperZ.NetworkUid = EntityUid.Invalid;
            entMan.Dirty(upper.MapUid, upperZ);
        });

        return (lower, upper);
    }

    private static bool HasAction(IEntityManager entMan, EntityUid user, string prototype)
    {
        if (!entMan.TryGetComponent<ActionsComponent>(user, out var actions))
            return false;

        foreach (var action in actions.Actions)
        {
            if (entMan.GetComponent<MetaDataComponent>(action).EntityPrototype?.ID == prototype)
                return true;
        }

        return false;
    }
}
