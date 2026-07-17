using Content.Shared._AU14.ZLevelBuilding;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests._AU14.ZLevelBuilding;

[TestFixture]
public sealed class TileApplierSystemTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: AU14TestAnchoredTileDependent
  components:
  - type: Transform
    anchored: true
";

    [Test]
    public async Task DeletingFloorSupportDefersTileRemovalPastTermination()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid support = default;
        EntityUid dependent = default;
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var mapSystem = entities.System<SharedMapSystem>();
            mapSystem.SetTile(map.Grid.Owner, map.Grid.Comp, new Vector2i(1, 0), map.Tile.Tile);
            support = entities.SpawnEntity("AU14TileFloorSupport", map.GridCoords);
            dependent = entities.SpawnEntity("AU14TestAnchoredTileDependent", map.GridCoords);

            Assert.That(entities.HasComponent<TileFloorSupportComponent>(support), Is.True);
            Assert.DoesNotThrow(() => entities.DeleteEntity(support));
        });

        await pair.RunTicksSync(2);

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var mapSystem = entities.System<SharedMapSystem>();
            var tile = mapSystem.GetTileRef(map.Grid.Owner, map.Grid.Comp, map.GridCoords);

            Assert.Multiple(() =>
            {
                Assert.That(tile.Tile.IsEmpty, Is.True);
                Assert.That(entities.Deleted(support), Is.True);
                Assert.That(entities.Deleted(dependent), Is.False);
                Assert.That(entities.GetComponent<TransformComponent>(dependent).Anchored, Is.False);
            });
        });

        await pair.CleanReturnAsync();
    }
}
