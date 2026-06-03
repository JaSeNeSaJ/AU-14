using System.Reflection;
using Content.Shared._CMU14.ZLevels.Core.EntitySystems;
using NUnit.Framework;

namespace Content.Tests.Shared._CMU14.ZLevels;

[TestFixture]
public sealed class CMUZLevelGroundSnapTest
{
    [Test]
    public void StickyGroundSnapsPastNormalStepHeight()
    {
        Assert.That(ShouldSnapToGround(0.9f, true), Is.True);
    }

    [Test]
    public void NonStickyGroundDoesNotSnapPastNormalStepHeight()
    {
        Assert.That(ShouldSnapToGround(0.9f, false), Is.False);
    }

    [Test]
    public void CloseNonStickyGroundSnaps()
    {
        Assert.That(ShouldSnapToGround(0.04f, false), Is.True);
    }

    private static bool ShouldSnapToGround(float distanceToGround, bool stickyGround)
    {
        var method = typeof(CMUSharedZLevelsSystem).GetMethod(
            "ShouldSnapToGround",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.That(method, Is.Not.Null);
        return (bool) method!.Invoke(null, new object[] { distanceToGround, stickyGround })!;
    }
}
