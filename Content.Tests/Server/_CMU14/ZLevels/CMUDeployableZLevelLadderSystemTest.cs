using System;
using System.IO;
using NUnit.Framework;

namespace Content.Tests.Server._CMU14.ZLevels;

[TestFixture]
public sealed class CMUDeployableZLevelLadderSystemTest
{
    [Test]
    public void DeployableLadderSubscribesToInWorldActivation()
    {
        var text = ReadDeployableLadderSystem();

        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain("SubscribeLocalEvent<CMUDeployableZLevelLadderComponent, ActivateInWorldEvent>(OnActivateInWorld)"));
            Assert.That(text, Does.Contain("private void OnActivateInWorld(Entity<CMUDeployableZLevelLadderComponent> ent, ref ActivateInWorldEvent args)"));
        });
    }

    private static string ReadDeployableLadderSystem()
    {
        var root = FindRepoRoot();
        var path = Path.Combine(
            root,
            "Content.Server",
            "_CMU14",
            "ZLevels",
            "Core",
            "CMUDeployableZLevelLadderSystem.cs");

        Assert.That(File.Exists(path), Is.True);
        return File.ReadAllText(path);
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "SpaceStation14.slnx")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find repository root containing SpaceStation14.slnx.");
    }
}
