using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared.FixedPoint;

namespace Content.Shared._CMU14.Medical.Human.Rules;

public readonly record struct MedicalDamageContext(
    InjuryKind PrimaryInjuryKind,
    bool BoneContact = false,
    bool OrganContact = false,
    bool VascularContact = false,
    OrganSlot OrganSlot = OrganSlot.None,
    float OrganDamageScale = 0f,
    FixedPoint2 InternalBleedRate = default);

public readonly record struct MedicalDamagePolicy(
    bool MedicalEnabled = true,
    bool BoneEnabled = true,
    bool OrganEnabled = true,
    bool WoundsEnabled = true,
    FixedPoint2 EscharBurnThreshold = default)
{
    public static MedicalDamagePolicy Default => new(
        MedicalEnabled: true,
        BoneEnabled: true,
        OrganEnabled: true,
        WoundsEnabled: true);
}

public static class MedicalDamageRules
{
    private static readonly FixedPoint2 MinimumTrackedInjuryDamage = FixedPoint2.New(5);
    private static readonly FixedPoint2 MinimumExternalBleedDamage = FixedPoint2.New(5);
    private static readonly FixedPoint2 DefaultEscharBurnThreshold = FixedPoint2.New(30);
    private const float ExternalBleedDamageMultiplier = 0.0375f;

    public static MedicalTransaction CreateDamageTransaction(
        BodyRegion region,
        FixedPoint2 brute,
        FixedPoint2 burn,
        MedicalDamageContext context,
        MedicalRngContext rng,
        FixedPoint2 escharBurnThreshold = default,
        OrganState[]? organs = null,
        MedicalDamagePolicy? policy = null)
    {
        var effectivePolicy = policy ?? MedicalDamagePolicy.Default;
        if (escharBurnThreshold > FixedPoint2.Zero)
            effectivePolicy = effectivePolicy with { EscharBurnThreshold = escharBurnThreshold };

        var transaction = new MedicalTransaction(region);
        if (!effectivePolicy.MedicalEnabled || region == BodyRegion.None)
            return transaction;

        if (brute > FixedPoint2.Zero || burn > FixedPoint2.Zero)
            transaction.Add(MedicalEffect.AddRegionDamage(region, brute, burn));

        AddPrimaryInjury(transaction, region, brute, burn, context, effectivePolicy);
        AddExternalBleeding(transaction, region, brute, context, effectivePolicy);
        AddFracture(transaction, region, brute, context, effectivePolicy);
        AddOrganDamage(transaction, region, brute, burn, context, rng, organs, effectivePolicy);
        AddInternalBleeding(transaction, region, context, effectivePolicy);

        return transaction;
    }

    private static void AddPrimaryInjury(
        MedicalTransaction transaction,
        BodyRegion region,
        FixedPoint2 brute,
        FixedPoint2 burn,
        MedicalDamageContext context,
        MedicalDamagePolicy policy)
    {
        if (!policy.WoundsEnabled)
            return;

        var injuryDamage = context.PrimaryInjuryKind == InjuryKind.Burn ? burn : brute;
        if (injuryDamage < MinimumTrackedInjuryDamage)
            return;

        var burnThreshold = GetBurnFlagThreshold(context.PrimaryInjuryKind, policy.EscharBurnThreshold);
        var injuryFlags = burnThreshold > FixedPoint2.Zero && injuryDamage >= burnThreshold
            ? InjuryFlags.Necrotic
            : InjuryFlags.None;

        transaction.Add(MedicalEffect.AddInjury(
            region,
            context.PrimaryInjuryKind,
            InjuryRules.GetStage(context.PrimaryInjuryKind, injuryDamage),
            injuryDamage,
            injuryFlags,
            burnThreshold));
    }

    private static FixedPoint2 GetBurnFlagThreshold(
        InjuryKind kind,
        FixedPoint2 escharBurnThreshold)
    {
        if (kind != InjuryKind.Burn)
            return FixedPoint2.Zero;

        return escharBurnThreshold > FixedPoint2.Zero
            ? escharBurnThreshold
            : DefaultEscharBurnThreshold;
    }

    private static void AddExternalBleeding(
        MedicalTransaction transaction,
        BodyRegion region,
        FixedPoint2 brute,
        MedicalDamageContext context,
        MedicalDamagePolicy policy)
    {
        if (!policy.WoundsEnabled ||
            context.PrimaryInjuryKind is not (InjuryKind.Cut or InjuryKind.Puncture) ||
            brute < MinimumExternalBleedDamage)
        {
            return;
        }

        var rate = FixedPoint2.New(brute.Float() * ExternalBleedDamageMultiplier);
        if (rate <= FixedPoint2.Zero)
            return;

        transaction.Add(MedicalEffect.AddBleedSource(
            region,
            BleedKind.External,
            rate));
    }

    private static void AddFracture(
        MedicalTransaction transaction,
        BodyRegion region,
        FixedPoint2 brute,
        MedicalDamageContext context,
        MedicalDamagePolicy policy)
    {
        if (!policy.BoneEnabled || !context.BoneContact)
            return;

        var fracture = SkeletalRules.EvaluateContactFracture(
            new SkeletalRuleInput(
                region,
                brute,
                BoneContact: true,
                AlreadyBroken: false,
                Splinted: false));

        if (fracture.ShouldBreak)
        {
            transaction.Add(MedicalEffect.SetSkeletalState(
                region,
                broken: true,
                splinted: false,
                fracture.Severity));
        }
    }

    private static void AddOrganDamage(
        MedicalTransaction transaction,
        BodyRegion region,
        FixedPoint2 brute,
        FixedPoint2 burn,
        MedicalDamageContext context,
        MedicalRngContext rng,
        OrganState[]? organs,
        MedicalDamagePolicy policy)
    {
        if (!policy.OrganEnabled ||
            !context.OrganContact ||
            context.OrganDamageScale <= 0f)
        {
            return;
        }

        var total = brute + burn;
        if (total <= FixedPoint2.Zero)
            return;

        var organ = context.OrganSlot;
        if (organ == OrganSlot.None &&
            !OrganRules.TryPickDamageTarget(region, organs, rng.OrganRoll, out organ))
        {
            return;
        }

        transaction.Add(MedicalEffect.AddOrganDamage(
            organ,
            FixedPoint2.New(total.Float() * context.OrganDamageScale)));
    }

    private static void AddInternalBleeding(
        MedicalTransaction transaction,
        BodyRegion region,
        MedicalDamageContext context,
        MedicalDamagePolicy policy)
    {
        if (!policy.WoundsEnabled ||
            !context.VascularContact ||
            context.InternalBleedRate <= FixedPoint2.Zero)
        {
            return;
        }

        transaction.Add(MedicalEffect.AddBleedSource(
            region,
            BleedKind.Internal,
            context.InternalBleedRate));
    }
}
