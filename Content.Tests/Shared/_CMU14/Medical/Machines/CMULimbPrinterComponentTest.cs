using Content.Shared._CMU14.Medical.Machines;
using NUnit.Framework;

namespace Content.Tests.Shared._CMU14.Medical.Machines;

[TestFixture]
public sealed class CMULimbPrinterComponentTest
{
    [Test]
    public void RoboticResourceStacksMatchRmcStockResources()
    {
        var component = new CMULimbPrinterComponent();

        Assert.Multiple(() =>
        {
            Assert.That(component.MetalStack.Id, Is.EqualTo("CMSteel"));
            Assert.That(component.CableStack.Id, Is.EqualTo("RMCCable"));
        });
    }
}
