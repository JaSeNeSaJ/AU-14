using Content.Shared._RMC14.Buckle;
using Content.Shared.Buckle.Components;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Buckle;

public sealed partial class BuckleTest
{
    private const string RMCBuckleDrawDepthDummyId = "RMCBuckleDrawDepthDummy";
    private const string RMCNoDrawDepthStrapDummyId = "RMCNoDrawDepthStrapDummy";
    private static readonly EntProtoId RMCChairId = "CMChair";

    [TestPrototypes]
    private const string RMCDrawDepthPrototypes = $@"
- type: entity
  name: {RMCBuckleDrawDepthDummyId}
  id: {RMCBuckleDrawDepthDummyId}
  components:
  - type: Buckle
  - type: Sprite
    drawdepth: Mobs

- type: entity
  name: {RMCNoDrawDepthStrapDummyId}
  id: {RMCNoDrawDepthStrapDummyId}
  components:
  - type: Strap
  - type: Sprite
    drawdepth: Objects
  - type: RMCStrapNoDrawDepthChange
";

    [Test]
    public async Task RMCNoDrawDepthStrapDoesNotLowerBuckledSpriteOnBuckledEvent()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;
        var entMan = client.ResolveDependency<IEntityManager>();

        await client.WaitAssertion(() =>
        {
            var buckle = entMan.SpawnEntity(RMCBuckleDrawDepthDummyId, MapCoordinates.Nullspace);
            var strap = entMan.SpawnEntity(RMCNoDrawDepthStrapDummyId, MapCoordinates.Nullspace);

            var transform = entMan.System<SharedTransformSystem>();
            transform.SetWorldRotationNoLerp(strap, Direction.North.ToAngle());

            var buckleComp = entMan.GetComponent<BuckleComponent>(buckle);
            var strapComp = entMan.GetComponent<StrapComponent>(strap);
            var buckleSprite = entMan.GetComponent<SpriteComponent>(buckle);
            var strapSprite = entMan.GetComponent<SpriteComponent>(strap);
            var originalDrawDepth = buckleSprite.DrawDepth;

            Assert.That(originalDrawDepth, Is.GreaterThan(strapSprite.DrawDepth));

            var ev = new BuckledEvent((strap, strapComp), (buckle, buckleComp));
            entMan.EventBus.RaiseLocalEvent(buckle, ref ev, true);

            Assert.Multiple(() =>
            {
                Assert.That(buckleSprite.DrawDepth, Is.EqualTo(originalDrawDepth));
                Assert.That(buckleComp.OriginalDrawDepth, Is.Null);
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RMCChairBaseOptsOutOfBuckleDrawDepthChanges()
    {
        await using var pair = await PoolManager.GetServerClient();
        var protoMan = pair.Server.ResolveDependency<IPrototypeManager>();
        var factory = pair.Server.ResolveDependency<IComponentFactory>();

        await pair.Server.WaitAssertion(() =>
        {
            var chair = protoMan.Index<EntityPrototype>(RMCChairId);
            Assert.That(
                chair.TryGetComponent<RMCStrapNoDrawDepthChangeComponent>(out _, factory),
                Is.True);
        });

        await pair.CleanReturnAsync();
    }
}
