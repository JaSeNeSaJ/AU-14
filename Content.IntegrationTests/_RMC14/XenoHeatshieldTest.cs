using System.Numerics;
using Content.Shared._RMC14.Xenonids.Heatshield;
using Content.Shared.Actions.Components;
using Content.Shared.Atmos.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests._RMC14;

[TestFixture]
public sealed class XenoHeatshieldTest
{
    [Test]
    public async Task BurningVomitBileIgnitesThreeTilesInFront()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid xeno = default;
        EntityUid targetNorth = default;
        EntityUid targetCenter = default;
        EntityUid targetSouth = default;
        EntityUid behind = default;
        Entity<ActionComponent> action = default;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            xeno = entMan.SpawnEntity("CMXenoDefenderHeatshield", map.GridCoords);
            targetNorth = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 1)));
            targetCenter = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            targetSouth = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, -1)));
            behind = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(-1, 0)));
            action = SpawnAction(entMan);

            var flammable = entMan.GetComponent<FlammableComponent>(xeno);
            flammable.OnFire = true;
            entMan.Dirty(xeno, flammable);
        });

        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;

            try
            {
                RaiseVomitBile(entMan, xeno, map.GridCoords.Offset(new Vector2(1, 0)), action);

                Assert.Multiple(() =>
                {
                    Assert.That(IsOnFire(entMan, targetNorth), Is.True);
                    Assert.That(IsOnFire(entMan, targetCenter), Is.True);
                    Assert.That(IsOnFire(entMan, targetSouth), Is.True);
                    Assert.That(IsOnFire(entMan, behind), Is.False);
                });
            }
            finally
            {
                entMan.DeleteEntity(xeno);
                entMan.DeleteEntity(targetNorth);
                entMan.DeleteEntity(targetCenter);
                entMan.DeleteEntity(targetSouth);
                entMan.DeleteEntity(behind);
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

    private static void RaiseVomitBile(
        IEntityManager entMan,
        EntityUid xeno,
        EntityCoordinates target,
        Entity<ActionComponent> action)
    {
        var ev = new XenoVomitBileActionEvent
        {
            Performer = xeno,
            Action = action,
            Target = target,
        };

        entMan.EventBus.RaiseLocalEvent(xeno, ev);
    }

    private static bool IsOnFire(IEntityManager entMan, EntityUid entity)
    {
        return entMan.GetComponent<FlammableComponent>(entity).OnFire;
    }
}
