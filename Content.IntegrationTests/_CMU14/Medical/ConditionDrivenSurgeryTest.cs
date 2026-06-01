using System.Collections.Generic;
using System.Reflection;
using Content.Server._CMU14.Medical.Surgery;
using Content.Shared._CMU14.Medical.Bones;
using Content.Shared._CMU14.Medical.Organs;
using Content.Shared._CMU14.Medical.Organs.Liver;
using Content.Shared._CMU14.Medical.Surgery;
using Content.Shared._CMU14.Medical.Surgery.Traits;
using Content.Shared._CMU14.Medical.Shrapnel;
using Content.Shared._CMU14.Medical.Wounds;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Medical.Surgery;
using Content.Shared._RMC14.Medical.Surgery.Steps.Parts;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests._CMU14.Medical;

[TestFixture]
public sealed class ConditionDrivenSurgeryTest
{
    [Test]
    public async Task OpenFractureWithoutTraitsResolvesNormalRepairStep()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var flow = entMan.System<SharedCMUSurgeryFlowSystem>();
            var fracture = entMan.System<SharedFractureSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                var arm = GetBodyPart(entMan, human, BodyPartType.Arm, BodyPartSymmetry.Right);
                OpenSoftTissue(entMan, arm);

                var frac = entMan.EnsureComponent<FractureComponent>(arm);
                fracture.SetSeverity((arm, frac), FractureSeverity.Simple);

                Assert.That(flow.TryResolveNextStep(human, arm, "CMUSurgerySetSimpleFracture", out var resolved), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(resolved.ResolvedSurgeryId, Is.EqualTo("CMUSurgerySetSimpleFracture"));
                    Assert.That(resolved.ToolCategory, Is.EqualTo("bone_setter"));
                });
            }
            finally
            {
                entMan.DeleteEntity(human);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SurgicalTraitsResolveInDeterministicOrder()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var flow = entMan.System<SharedCMUSurgeryFlowSystem>();
            var traits = entMan.System<SharedCMUSurgicalTraitSystem>();
            var fracture = entMan.System<SharedFractureSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                var arm = GetBodyPart(entMan, human, BodyPartType.Arm, BodyPartSymmetry.Right);
                OpenSoftTissue(entMan, arm);

                var frac = entMan.EnsureComponent<FractureComponent>(arm);
                fracture.SetSeverity((arm, frac), FractureSeverity.Comminuted);

                traits.EnsureTrait(arm, CMUSurgicalTrait.VascularTear);
                traits.EnsureTrait(arm, CMUSurgicalTrait.EmbeddedForeignBody);
                traits.EnsureTrait(arm, CMUSurgicalTrait.CompartmentPressure);
                traits.EnsureTrait(arm, CMUSurgicalTrait.ContaminatedWound);
                traits.EnsureTrait(arm, CMUSurgicalTrait.BoneSplintered);

                AssertNext(flow, human, arm, "CMUSurgeryTieVascularTear");
                traits.RemoveTrait(arm, CMUSurgicalTrait.VascularTear);

                AssertNext(flow, human, arm, "CMUSurgeryExtractForeignBody");
                traits.RemoveTrait(arm, CMUSurgicalTrait.EmbeddedForeignBody);

                AssertNext(flow, human, arm, "CMUSurgeryRelieveCompartmentPressure");
                traits.RemoveTrait(arm, CMUSurgicalTrait.CompartmentPressure);

                AssertNext(flow, human, arm, "CMUSurgeryDebrideContaminatedWound");
                traits.RemoveTrait(arm, CMUSurgicalTrait.ContaminatedWound);

                AssertNext(flow, human, arm, "CMUSurgeryRemoveBoneFragments");
                traits.RemoveTrait(arm, CMUSurgicalTrait.BoneSplintered);

                AssertNext(flow, human, arm, "CMUSurgerySetComminutedFracture");
            }
            finally
            {
                entMan.DeleteEntity(human);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ArmedSurgeryReResolvesInjectedCleanupBeforeRunningStaleStep()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var flow = entMan.System<CMUSurgeryFlowSystem>();
            var traits = entMan.System<SharedCMUSurgicalTraitSystem>();
            var fracture = entMan.System<SharedFractureSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var surgeon = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<BypassSkillChecksComponent>(surgeon);
                entMan.EnsureComponent<CMUAutodocContainedPatientComponent>(human);

                var arm = GetBodyPart(entMan, human, BodyPartType.Arm, BodyPartSymmetry.Right);
                OpenSoftTissue(entMan, arm);

                var frac = entMan.EnsureComponent<FractureComponent>(arm);
                fracture.SetSeverity((arm, frac), FractureSeverity.Simple);

                var armed = flow.TryArmStep(
                    surgeon,
                    human,
                    arm,
                    "CMUSurgerySetSimpleFracture",
                    0,
                    BodyPartType.Arm,
                    BodyPartSymmetry.Right);

                Assert.That(armed, Is.Not.Null);
                Assert.That(armed!.SurgeryId, Is.EqualTo("CMUSurgerySetSimpleFracture"));

                traits.EnsureTrait(arm, CMUSurgicalTrait.VascularTear);

                Assert.That(flow.TryCompleteAutomatedStep(human, armed, surgeon), Is.True);

                var rearmed = entMan.GetComponent<CMUSurgeryArmedStepComponent>(human);
                Assert.Multiple(() =>
                {
                    Assert.That(rearmed.SurgeryId, Is.EqualTo("CMUSurgeryTieVascularTear"));
                    Assert.That(rearmed.RequiredToolCategory, Is.EqualTo("hemostat"));
                    Assert.That(traits.HasTrait(arm, CMUSurgicalTrait.VascularTear), Is.True);
                    Assert.That(entMan.GetComponent<FractureComponent>(arm).Severity, Is.EqualTo(FractureSeverity.Simple));
                });
            }
            finally
            {
                entMan.DeleteEntity(human);
                entMan.DeleteEntity(surgeon);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ResolveTraitStepRemovesTraitAndSuppressesVascularBleeding()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var traits = entMan.System<SharedCMUSurgicalTraitSystem>();
            var wounds = entMan.System<SharedCMUWoundsSystem>();
            var rmcSurgery = entMan.System<Content.Shared._RMC14.Medical.Surgery.SharedCMSurgerySystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                var arm = GetBodyPart(entMan, human, BodyPartType.Arm, BodyPartSymmetry.Right);
                traits.EnsureTrait(arm, CMUSurgicalTrait.VascularTear);
                wounds.SeedInternalBleed(arm, "fracture:Comminuted", 0.5f);

                var step = rmcSurgery.GetSingleton("CMUSurgeryStepTieVascularTear");
                Assert.That(step, Is.Not.Null);

                var ev = new CMSurgeryStepEvent(human, human, arm, new List<EntityUid>());
                entMan.EventBus.RaiseLocalEvent(step.Value, ref ev);

                Assert.Multiple(() =>
                {
                    Assert.That(traits.HasTrait(arm, CMUSurgicalTrait.VascularTear), Is.False);
                    Assert.That(entMan.HasComponent<InternalBleedingComponent>(arm), Is.False);
                });
            }
            finally
            {
                entMan.DeleteEntity(human);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ResolveForeignBodyStepClearsShrapnelCondition()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var traits = entMan.System<SharedCMUSurgicalTraitSystem>();
            var shrapnel = entMan.System<SharedCMUShrapnelSystem>();
            var rmcSurgery = entMan.System<Content.Shared._RMC14.Medical.Surgery.SharedCMSurgerySystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                var arm = GetBodyPart(entMan, human, BodyPartType.Arm, BodyPartSymmetry.Right);
                Assert.That(shrapnel.AddShrapnel(arm, 1, 10f), Is.True);
                Assert.That(traits.HasTrait(arm, CMUSurgicalTrait.EmbeddedForeignBody), Is.True);

                var step = rmcSurgery.GetSingleton("CMUSurgeryStepExtractForeignBody");
                Assert.That(step, Is.Not.Null);

                var ev = new CMSurgeryStepEvent(human, human, arm, new List<EntityUid>());
                entMan.EventBus.RaiseLocalEvent(step.Value, ref ev);

                Assert.Multiple(() =>
                {
                    Assert.That(traits.HasTrait(arm, CMUSurgicalTrait.EmbeddedForeignBody), Is.False);
                    Assert.That(entMan.HasComponent<CMUShrapnelComponent>(arm), Is.False);
                });
            }
            finally
            {
                entMan.DeleteEntity(human);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task OrganRepairSurgeryInjectsOrganTraitsInOrder()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var flow = entMan.System<SharedCMUSurgeryFlowSystem>();
            var traits = entMan.System<SharedCMUSurgicalTraitSystem>();
            var organHealth = entMan.System<SharedOrganHealthSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                var torso = GetBodyPart(entMan, human, BodyPartType.Torso, BodyPartSymmetry.None);
                OpenBoneCavity(entMan, torso);

                var liver = GetPartOrgan<LiverComponent>(entMan, torso);
                var health = entMan.GetComponent<OrganHealthComponent>(liver);
                SetPublicField(health, nameof(OrganHealthComponent.Current), (FixedPoint2)20);
                organHealth.RecomputeStage((liver, health), human);

                traits.EnsureTrait(torso, CMUSurgicalTrait.EmbeddedForeignBody);
                traits.EnsureTrait(torso, CMUSurgicalTrait.OrganAdhesion);
                traits.EnsureTrait(torso, CMUSurgicalTrait.OrganHemorrhage);

                AssertNextOrgan(flow, human, torso, "CMUSurgeryExtractForeignBody");
                traits.RemoveTrait(torso, CMUSurgicalTrait.EmbeddedForeignBody);

                AssertNextOrgan(flow, human, torso, "CMUSurgeryFreeOrganAdhesions");
                traits.RemoveTrait(torso, CMUSurgicalTrait.OrganAdhesion);

                AssertNextOrgan(flow, human, torso, "CMUSurgeryPackOrganBleed");
                traits.RemoveTrait(torso, CMUSurgicalTrait.OrganHemorrhage);

                AssertNextOrgan(flow, human, torso, "CMUSurgeryRepairLiver");
            }
            finally
            {
                entMan.DeleteEntity(human);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FractureSeveritySeedsBoundedTraits()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var traits = entMan.System<SharedCMUSurgicalTraitSystem>();
            var fracture = entMan.System<SharedFractureSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                var arm = GetBodyPart(entMan, human, BodyPartType.Arm, BodyPartSymmetry.Right);
                var torso = GetBodyPart(entMan, human, BodyPartType.Torso, BodyPartSymmetry.None);

                var armFrac = entMan.EnsureComponent<FractureComponent>(arm);
                fracture.SetSeverity((arm, armFrac), FractureSeverity.Comminuted);

                var torsoFrac = entMan.EnsureComponent<FractureComponent>(torso);
                fracture.SetSeverity((torso, torsoFrac), FractureSeverity.Comminuted);

                Assert.Multiple(() =>
                {
                    Assert.That(traits.HasTrait(arm, CMUSurgicalTrait.BoneSplintered), Is.True);
                    Assert.That(traits.CountTraits(arm), Is.LessThanOrEqualTo(2));
                    Assert.That(traits.HasTrait(torso, CMUSurgicalTrait.BoneSplintered), Is.True);
                    Assert.That(traits.CountTraits(torso), Is.LessThanOrEqualTo(2));
                });
            }
            finally
            {
                entMan.DeleteEntity(human);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public void SurgicalTraitGenerationUsesApprovedBalanceRates()
    {
        Assert.Multiple(() =>
        {
            Assert.That(CMUSurgicalTraitGenerationSystem.CompoundContaminationChance, Is.EqualTo(0.65f));
            Assert.That(CMUSurgicalTraitGenerationSystem.ComminutedSecondTraitChance, Is.EqualTo(0.5f));
            Assert.That(CMUSurgicalTraitGenerationSystem.DamagedOrganComplicationChance, Is.EqualTo(0.25f));
            Assert.That(CMUSurgicalTraitGenerationSystem.FailingOrganComplicationChance, Is.EqualTo(0.6f));
            Assert.That(CMUSurgicalTraitGenerationSystem.ShouldSeedCompoundContamination(0.64f), Is.True);
            Assert.That(CMUSurgicalTraitGenerationSystem.ShouldSeedCompoundContamination(0.65f), Is.False);
            Assert.That(CMUSurgicalTraitGenerationSystem.ShouldSeedComminutedSecondTrait(0.49f), Is.True);
            Assert.That(CMUSurgicalTraitGenerationSystem.ShouldSeedComminutedSecondTrait(0.5f), Is.False);
            Assert.That(CMUSurgicalTraitGenerationSystem.ShouldSeedDamagedOrganComplication(0.24f), Is.True);
            Assert.That(CMUSurgicalTraitGenerationSystem.ShouldSeedDamagedOrganComplication(0.25f), Is.False);
            Assert.That(CMUSurgicalTraitGenerationSystem.ShouldSeedFailingOrganComplication(0.59f), Is.True);
            Assert.That(CMUSurgicalTraitGenerationSystem.ShouldSeedFailingOrganComplication(0.6f), Is.False);
        });
    }

    private static void AssertNext(SharedCMUSurgeryFlowSystem flow, EntityUid human, EntityUid part, string surgeryId)
    {
        Assert.That(flow.TryResolveNextStep(human, part, "CMUSurgerySetComminutedFracture", out var resolved), Is.True);
        Assert.That(resolved.ResolvedSurgeryId, Is.EqualTo(surgeryId));
    }

    private static void AssertNextOrgan(SharedCMUSurgeryFlowSystem flow, EntityUid human, EntityUid part, string surgeryId)
    {
        Assert.That(flow.TryResolveNextStep(human, part, "CMUSurgeryRepairLiver", out var resolved), Is.True);
        Assert.That(resolved.ResolvedSurgeryId, Is.EqualTo(surgeryId));
    }

    private static void OpenSoftTissue(IEntityManager entMan, EntityUid part)
    {
        entMan.EnsureComponent<CMIncisionOpenComponent>(part);
        entMan.EnsureComponent<CMBleedersClampedComponent>(part);
        entMan.EnsureComponent<CMSkinRetractedComponent>(part);
    }

    private static void OpenBoneCavity(IEntityManager entMan, EntityUid part)
    {
        OpenSoftTissue(entMan, part);
        entMan.EnsureComponent<CMRibcageSawedComponent>(part);
        entMan.EnsureComponent<CMRibcageOpenComponent>(part);
    }

    private static EntityUid GetPartOrgan<TOrgan>(IEntityManager entMan, EntityUid part) where TOrgan : IComponent
    {
        var body = entMan.System<SharedBodySystem>();
        foreach (var (organUid, _) in body.GetPartOrgans(part))
        {
            if (entMan.HasComponent<TOrgan>(organUid))
                return organUid;
        }

        Assert.Fail($"Expected part to contain organ {typeof(TOrgan).Name}.");
        return EntityUid.Invalid;
    }

    private static void SetPublicField<TComponent>(TComponent comp, string name, object value)
        where TComponent : IComponent
    {
        typeof(TComponent).GetField(name, BindingFlags.Instance | BindingFlags.Public)!.SetValue(comp, value);
    }

    private static EntityUid GetBodyPart(
        IEntityManager entMan,
        EntityUid bodyUid,
        BodyPartType type,
        BodyPartSymmetry symmetry)
    {
        var body = entMan.System<SharedBodySystem>();
        foreach (var (partUid, part) in body.GetBodyChildren(bodyUid))
        {
            if (part.PartType == type && part.Symmetry == symmetry)
                return partUid;
        }

        Assert.Fail($"Expected CMU human to have {symmetry} {type}.");
        return EntityUid.Invalid;
    }
}
