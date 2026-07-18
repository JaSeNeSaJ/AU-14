using System.Linq;
using System.Numerics;
using Content.Shared._RMC14.Xenonids.Stomp;
using Content.Shared.Physics;
using Content.Shared.StatusEffect;
using Robust.Shared.GameObjects;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;

namespace Content.IntegrationTests._RMC14;

[TestFixture]
public sealed class XenoStompTest
{
    [Test]
    public async Task BurrowerStompParalyzesMarineBehindBarricade()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid burrower = default;
        EntityUid barricade = default;
        EntityUid marine = default;

        try
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                burrower = entMan.SpawnEntity("CMXenoBurrower", map.GridCoords.Offset(new Vector2(0.5f, 0.5f)));
                barricade = entMan.SpawnEntity("CMBarricadeMetal", map.GridCoords.Offset(new Vector2(0.5f, 1.5f)));
                marine = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(0.5f, 2.5f)));
            });

            await pair.RunTicksSync(1);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var physics = entMan.System<SharedPhysicsSystem>();
                var status = entMan.System<StatusEffectQuerySystem>();
                var transform = entMan.System<SharedTransformSystem>();
                var origin = transform.GetMapCoordinates(burrower);
                var target = transform.GetMapCoordinates(marine);
                var diff = target.Position - origin.Position;
                var ray = new CollisionRay(origin.Position, Vector2.Normalize(diff), (int) CollisionGroup.BarricadeImpassable);
                var hit = physics.IntersectRay(origin.MapId, ray, diff.Length(), burrower, returnOnFirstHit: true).Single();

                Assert.That(hit.HitEntity, Is.EqualTo(barricade));

                var stomp = new XenoStompDoAfterEvent();
                entMan.EventBus.RaiseLocalEvent(burrower, stomp);

                Assert.That(stomp.Handled, Is.True);
                Assert.That(status.TryGetTime(marine, "Stun", out _), Is.True);
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                if (entMan.EntityExists(burrower))
                    entMan.DeleteEntity(burrower);

                if (entMan.EntityExists(barricade))
                    entMan.DeleteEntity(barricade);

                if (entMan.EntityExists(marine))
                    entMan.DeleteEntity(marine);
            });

            await pair.CleanReturnAsync();
        }
    }
}
