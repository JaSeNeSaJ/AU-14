using Content.Shared.Body.Part;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;

namespace Content.Shared._CMU14.Medical.Human.Damage;

public enum CMUTraumaMechanism : byte
{
    Generic,
    Ballistic,
    Slash,
    Pierce,
    Blunt,
    Explosive,
}

public enum CMUTraumaDepth : byte
{
    Graze,
    SoftTissue,
    Bone,
    Deep,
    Severe,
}

public readonly record struct CMUTraumaContactResult(
    CMUTraumaMechanism Mechanism,
    CMUTraumaDepth Depth,
    bool BoneContact,
    bool OrganContact,
    bool VascularContact,
    float OrganPassThrough,
    float InternalBleedRate,
    bool HighEnergy)
{
    public static CMUTraumaContactResult SoftTissue(CMUTraumaMechanism mechanism)
        => new(mechanism, CMUTraumaDepth.SoftTissue, false, false, false, 0f, 0f, false);
}

public readonly record struct CMUTraumaContactSettings
{
    public FixedPoint2 BallisticHighDamageThreshold { get; init; }
    public FixedPoint2 MeleeHighDamageThreshold { get; init; }

    public float BallisticHeadBoneChance { get; init; }
    public float BallisticTorsoBoneChance { get; init; }
    public float BallisticArmBoneChance { get; init; }
    public float BallisticLegBoneChance { get; init; }
    public float BallisticOtherBoneChance { get; init; }
    public float BallisticHeadOrganChance { get; init; }
    public float BallisticTorsoOrganChance { get; init; }
    public float BallisticVascularChance { get; init; }

    public float PierceBoneChance { get; init; }
    public float PierceOrganChance { get; init; }
    public float PierceVascularChance { get; init; }

    public float SlashBoneChance { get; init; }
    public float SlashOrganChance { get; init; }
    public float SlashVascularChance { get; init; }

    public float BluntBoneChance { get; init; }
    public float BluntOrganChance { get; init; }
    public float BluntVascularChance { get; init; }

    public float BallisticOrganPassThrough { get; init; }
    public float PierceOrganPassThrough { get; init; }
    public float SlashOrganPassThrough { get; init; }
    public float BluntOrganPassThrough { get; init; }
    public float HighEnergyOrganPassThrough { get; init; }
    public float ExplosiveOrganPassThrough { get; init; }

    public float BallisticInternalBleedRate { get; init; }
    public float PierceInternalBleedRate { get; init; }
    public float SlashInternalBleedRate { get; init; }
    public float BluntInternalBleedRate { get; init; }

    public static CMUTraumaContactSettings Default => new()
    {
        BallisticHighDamageThreshold = FixedPoint2.New(45),
        MeleeHighDamageThreshold = FixedPoint2.New(45),

        BallisticHeadBoneChance = 0.13f,
        BallisticTorsoBoneChance = 0.06f,
        BallisticArmBoneChance = 0.12f,
        BallisticLegBoneChance = 0.12f,
        BallisticOtherBoneChance = 0.07f,
        BallisticHeadOrganChance = 0.016f,
        BallisticTorsoOrganChance = 0.05f,
        BallisticVascularChance = 0.03f,

        PierceBoneChance = 0.04f,
        PierceOrganChance = 0.035f,
        PierceVascularChance = 0.04f,

        SlashBoneChance = 0.02f,
        SlashOrganChance = 0.02f,
        SlashVascularChance = 0.05f,

        BluntBoneChance = 0.10f,
        BluntOrganChance = 0.01f,
        BluntVascularChance = 0.02f,

        BallisticOrganPassThrough = 0.35f,
        PierceOrganPassThrough = 0.30f,
        SlashOrganPassThrough = 0.20f,
        BluntOrganPassThrough = 0.15f,
        HighEnergyOrganPassThrough = 0.50f,
        ExplosiveOrganPassThrough = 1.0f,

        BallisticInternalBleedRate = 0.25f,
        PierceInternalBleedRate = 0.30f,
        SlashInternalBleedRate = 0.25f,
        BluntInternalBleedRate = 0.20f,
    };
}

public static class CMUTraumaContactModel
{
    private const float BoneDepthScore = 0.30f;
    private const float DeepDepthScore = 0.70f;

    public static CMUTraumaContactResult Create(
        CMUTraumaMechanism mechanism,
        BodyPartType partType,
        FixedPoint2 bruteDamage,
        bool hasOrgans,
        float roll,
        CMUTraumaContactSettings settings)
    {
        return Create(mechanism, default, partType, bruteDamage, hasOrgans, settings);
    }

    public static CMUTraumaContactResult Create(
        CMUTraumaMechanism mechanism,
        BodyPartType partType,
        FixedPoint2 bruteDamage,
        bool hasOrgans,
        CMUTraumaContactSettings settings)
    {
        return Create(mechanism, default, partType, bruteDamage, hasOrgans, settings);
    }

    public static CMUTraumaContactResult Create(
        CMUTraumaMechanism mechanism,
        DamageImpact impact,
        BodyPartType partType,
        FixedPoint2 bruteDamage,
        bool hasOrgans,
        float roll,
        CMUTraumaContactSettings settings)
    {
        return Create(mechanism, impact, partType, bruteDamage, hasOrgans, settings);
    }

    public static CMUTraumaContactResult Create(
        CMUTraumaMechanism mechanism,
        DamageImpact impact,
        BodyPartType partType,
        FixedPoint2 bruteDamage,
        bool hasOrgans,
        CMUTraumaContactSettings settings)
    {
        impact = NormalizeImpact(mechanism, impact);
        if (mechanism == CMUTraumaMechanism.Explosive)
        {
            return new CMUTraumaContactResult(
                mechanism,
                CMUTraumaDepth.Severe,
                true,
                hasOrgans,
                true,
                hasOrgans ? settings.ExplosiveOrganPassThrough : 0f,
                GetInternalBleedRate(mechanism, impact, settings),
                true);
        }

        if (bruteDamage <= FixedPoint2.Zero || mechanism == CMUTraumaMechanism.Generic)
            return CMUTraumaContactResult.SoftTissue(mechanism);

        if (impact.IsSpecified && impact.Contact is DamageImpactContact.Burn or DamageImpactContact.Snag)
            return CMUTraumaContactResult.SoftTissue(mechanism);

        if (IsHighEnergy(mechanism, impact, bruteDamage, settings))
        {
            return new CMUTraumaContactResult(
                mechanism,
                CMUTraumaDepth.Severe,
                true,
                hasOrgans,
                true,
                hasOrgans ? settings.HighEnergyOrganPassThrough : 0f,
                GetInternalBleedRate(mechanism, impact, settings),
                true);
        }

        var depth = ResolveDepth(mechanism, impact, partType, bruteDamage, settings);
        var bone = depth >= CMUTraumaDepth.Bone;
        var organ = hasOrgans && depth >= CMUTraumaDepth.Deep;
        var vascular = ShouldCauseVascularContact(depth, mechanism, impact);

        return new CMUTraumaContactResult(
            mechanism,
            depth,
            bone,
            organ,
            vascular,
            organ ? GetOrganPassThrough(mechanism, impact, settings) : 0f,
            vascular ? GetInternalBleedRate(mechanism, impact, settings) : 0f,
            false);
    }

    private static CMUTraumaDepth ResolveDepth(
        CMUTraumaMechanism mechanism,
        DamageImpact impact,
        BodyPartType partType,
        FixedPoint2 bruteDamage,
        CMUTraumaContactSettings settings)
    {
        var threshold = GetHighDamageThreshold(mechanism, settings);
        if (threshold <= FixedPoint2.Zero)
            return CMUTraumaDepth.SoftTissue;

        var score = bruteDamage.Float() / threshold.Float();
        score *= GetMechanismDepthMultiplier(mechanism);
        score *= GetContactDepthMultiplier(impact);
        score *= GetPenetrationDepthMultiplier(impact);
        score *= GetEnergyDepthMultiplier(impact);
        score *= GetPartDepthMultiplier(partType);

        if (score >= DeepDepthScore)
            return CMUTraumaDepth.Deep;
        if (score >= BoneDepthScore)
            return CMUTraumaDepth.Bone;

        return CMUTraumaDepth.SoftTissue;
    }

    private static FixedPoint2 GetHighDamageThreshold(
        CMUTraumaMechanism mechanism,
        CMUTraumaContactSettings settings)
    {
        return mechanism == CMUTraumaMechanism.Ballistic
            ? settings.BallisticHighDamageThreshold
            : settings.MeleeHighDamageThreshold;
    }

    private static DamageImpact NormalizeImpact(CMUTraumaMechanism mechanism, DamageImpact impact)
    {
        if (impact.IsSpecified)
            return impact;

        return mechanism switch
        {
            CMUTraumaMechanism.Ballistic => DamageImpact.Projectile,
            CMUTraumaMechanism.Pierce => new DamageImpact(
                DamageImpactDelivery.Melee,
                DamageImpactContact.Stab,
                DamageImpactPenetration.Medium,
                DamageImpactEnergy.Medium),
            CMUTraumaMechanism.Slash => DamageImpact.MeleeSlash,
            CMUTraumaMechanism.Blunt => new DamageImpact(
                DamageImpactDelivery.Melee,
                DamageImpactContact.Crush,
                DamageImpactPenetration.None,
                DamageImpactEnergy.Medium),
            CMUTraumaMechanism.Explosive => DamageImpact.Explosion,
            _ => impact,
        };
    }

    private static float GetMechanismDepthMultiplier(CMUTraumaMechanism mechanism)
    {
        return mechanism switch
        {
            CMUTraumaMechanism.Ballistic => 1.15f,
            CMUTraumaMechanism.Pierce => 1.05f,
            CMUTraumaMechanism.Slash => 0.75f,
            CMUTraumaMechanism.Blunt => 1f,
            CMUTraumaMechanism.Explosive => 2f,
            _ => 0f,
        };
    }

    private static float GetContactDepthMultiplier(DamageImpact impact)
    {
        return impact.Contact switch
        {
            DamageImpactContact.Blast => 2f,
            DamageImpactContact.Crush => 1.25f,
            DamageImpactContact.Stab => 1.1f,
            DamageImpactContact.Fragment => 0.85f,
            DamageImpactContact.Burn or DamageImpactContact.Snag => 0f,
            _ => 1f,
        };
    }

    private static float GetPenetrationDepthMultiplier(DamageImpact impact)
    {
        return impact.Penetration switch
        {
            DamageImpactPenetration.None => impact.Contact == DamageImpactContact.Crush ? 1f : 0.25f,
            DamageImpactPenetration.Low => 0.45f,
            DamageImpactPenetration.Medium => 1.15f,
            DamageImpactPenetration.High => 1.65f,
            DamageImpactPenetration.Forced => 2.25f,
            _ => 1f,
        };
    }

    private static float GetEnergyDepthMultiplier(DamageImpact impact)
    {
        return impact.Energy switch
        {
            DamageImpactEnergy.Low => 0.75f,
            DamageImpactEnergy.High => 1.15f,
            DamageImpactEnergy.Severe => 1.5f,
            _ => 1f,
        };
    }

    private static float GetPartDepthMultiplier(BodyPartType partType)
    {
        return partType switch
        {
            BodyPartType.Head => 1.1f,
            BodyPartType.Arm or BodyPartType.Hand => 1.05f,
            BodyPartType.Leg or BodyPartType.Foot => 1.05f,
            _ => 1f,
        };
    }

    private static bool ShouldCauseVascularContact(
        CMUTraumaDepth depth,
        CMUTraumaMechanism mechanism,
        DamageImpact impact)
    {
        if (depth < CMUTraumaDepth.Deep)
            return false;
        if (depth >= CMUTraumaDepth.Severe)
            return true;

        return mechanism switch
        {
            CMUTraumaMechanism.Ballistic or CMUTraumaMechanism.Pierce => true,
            CMUTraumaMechanism.Slash => impact.Penetration >= DamageImpactPenetration.Medium,
            _ => false,
        };
    }

    private static bool IsHighEnergy(
        CMUTraumaMechanism mechanism,
        DamageImpact impact,
        FixedPoint2 bruteDamage,
        CMUTraumaContactSettings settings)
    {
        if (impact.IsSpecified && impact.Contact is DamageImpactContact.Burn or DamageImpactContact.Snag)
            return false;

        if (impact is { Delivery: DamageImpactDelivery.Explosion } ||
            impact is { Energy: DamageImpactEnergy.Severe })
        {
            return true;
        }

        var threshold = GetHighDamageThreshold(mechanism, settings);

        return threshold > FixedPoint2.Zero && bruteDamage >= threshold;
    }

    private static float GetOrganPassThrough(CMUTraumaMechanism mechanism, DamageImpact impact, CMUTraumaContactSettings settings)
        => Chance((mechanism switch
        {
            CMUTraumaMechanism.Ballistic => settings.BallisticOrganPassThrough,
            CMUTraumaMechanism.Pierce => settings.PierceOrganPassThrough,
            CMUTraumaMechanism.Slash => settings.SlashOrganPassThrough,
            CMUTraumaMechanism.Blunt => settings.BluntOrganPassThrough,
            _ => 0f,
        }) * GetOrganPassThroughMultiplier(mechanism, impact));

    private static float GetInternalBleedRate(CMUTraumaMechanism mechanism, DamageImpact impact, CMUTraumaContactSettings settings)
        => Chance((mechanism switch
        {
            CMUTraumaMechanism.Ballistic => settings.BallisticInternalBleedRate,
            CMUTraumaMechanism.Pierce => settings.PierceInternalBleedRate,
            CMUTraumaMechanism.Slash => settings.SlashInternalBleedRate,
            CMUTraumaMechanism.Blunt => settings.BluntInternalBleedRate,
            CMUTraumaMechanism.Explosive => MathF.Max(settings.BallisticInternalBleedRate, settings.BluntInternalBleedRate),
            _ => 0f,
        }) * GetVascularDepthMultiplier(mechanism, impact));

    private static float GetBoneDepthMultiplier(CMUTraumaMechanism mechanism, DamageImpact impact)
    {
        if (!impact.IsSpecified || mechanism == CMUTraumaMechanism.Ballistic)
            return 1f;

        if (impact.Contact is DamageImpactContact.Burn or DamageImpactContact.Snag)
            return 0f;

        return impact.Penetration switch
        {
            DamageImpactPenetration.None => impact.Contact == DamageImpactContact.Crush ? 1f : 0f,
            DamageImpactPenetration.Medium => 1.25f,
            DamageImpactPenetration.High => 1.5f,
            DamageImpactPenetration.Forced => 2f,
            _ => 1f,
        };
    }

    private static float GetOrganDepthMultiplier(CMUTraumaMechanism mechanism, DamageImpact impact)
    {
        if (!impact.IsSpecified || mechanism == CMUTraumaMechanism.Ballistic)
            return 1f;

        if (impact.Contact is DamageImpactContact.Burn or DamageImpactContact.Snag)
            return 0f;

        return impact.Penetration switch
        {
            DamageImpactPenetration.None => 0f,
            DamageImpactPenetration.Low => impact.Contact switch
            {
                DamageImpactContact.Slash => 0.25f,
                DamageImpactContact.Fragment => 0.35f,
                _ => 0.5f,
            },
            DamageImpactPenetration.Medium => 1.25f,
            DamageImpactPenetration.High => 1.75f,
            DamageImpactPenetration.Forced => 2f,
            _ => 1f,
        };
    }

    private static float GetVascularDepthMultiplier(CMUTraumaMechanism mechanism, DamageImpact impact)
    {
        if (!impact.IsSpecified || mechanism == CMUTraumaMechanism.Ballistic)
            return 1f;

        if (impact.Contact is DamageImpactContact.Burn or DamageImpactContact.Snag)
            return 0f;

        return impact.Penetration switch
        {
            DamageImpactPenetration.None => 0f,
            DamageImpactPenetration.Low => 0.3f,
            DamageImpactPenetration.Medium => 1.25f,
            DamageImpactPenetration.High => 1.75f,
            DamageImpactPenetration.Forced => 2f,
            _ => 1f,
        };
    }

    private static float GetOrganPassThroughMultiplier(CMUTraumaMechanism mechanism, DamageImpact impact)
    {
        if (!impact.IsSpecified)
            return 1f;

        if (mechanism == CMUTraumaMechanism.Ballistic)
        {
            return impact.Penetration switch
            {
                DamageImpactPenetration.Medium => 1.15f,
                DamageImpactPenetration.High => 1.25f,
                DamageImpactPenetration.Forced => 1.35f,
                _ => 1f,
            };
        }

        return impact.Penetration switch
        {
            DamageImpactPenetration.Low => 0.5f,
            DamageImpactPenetration.Medium => 1.25f,
            DamageImpactPenetration.High => 1.5f,
            DamageImpactPenetration.Forced => 2f,
            _ => 1f,
        };
    }

    private static float Chance(float chance)
        => Math.Clamp(chance, 0f, 1f);
}
