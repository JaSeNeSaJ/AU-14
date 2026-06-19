using Content.Shared._CMU14.Medical.Human.Components;
using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared._RMC14.Medical.Defibrillator;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Medical;
using Robust.Shared.Network;

namespace Content.Shared._CMU14.Medical.Human.Systems;

public sealed partial class HumanMedicalDefibrillatorHealingSystem : EntitySystem
{
    [Dependency] private SharedHumanMedicalSystem _humanMedical = default!;
    [Dependency] private INetManager _net = default!;

    private static readonly string[] BruteTypes =
    {
        "Blunt",
        "Slash",
        "Piercing",
    };

    private static readonly string[] BurnTypes =
    {
        "Heat",
        "Shock",
        "Cold",
        "Caustic",
    };

    private static readonly BodyRegion[] RegionRecoveryPriority =
    {
        BodyRegion.Head,
        BodyRegion.Chest,
        BodyRegion.Groin,
        BodyRegion.LeftArm,
        BodyRegion.RightArm,
        BodyRegion.LeftHand,
        BodyRegion.RightHand,
        BodyRegion.LeftLeg,
        BodyRegion.RightLeg,
        BodyRegion.LeftFoot,
        BodyRegion.RightFoot,
    };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DefibrillatorComponent, ComponentStartup>(OnDefibrillatorStartup);
        SubscribeLocalEvent<HumanMedicalDefibrillatorHealingComponent, RMCDefibrillatorDamageModifyEvent>(
            OnDefibrillatorDamageModify,
            after: [typeof(RMCDefibrillatorSystem)]);
    }

    private void OnDefibrillatorStartup(Entity<DefibrillatorComponent> ent, ref ComponentStartup args)
    {
        if (_net.IsClient)
            return;

        EnsureComp<HumanMedicalDefibrillatorHealingComponent>(ent);
    }

    private void OnDefibrillatorDamageModify(
        Entity<HumanMedicalDefibrillatorHealingComponent> ent,
        ref RMCDefibrillatorDamageModifyEvent args)
    {
        if (_net.IsClient ||
            !TryComp<HumanMedicalComponent>(args.Target, out var medical))
        {
            return;
        }

        var transaction = CreateHealingTransaction(medical, args.Heal, out var remainingHeal);
        if (transaction == null)
        {
            args.Heal = remainingHeal;
            return;
        }

        var result = _humanMedical.ApplyTransaction((args.Target, medical), transaction);
        if (result.Applied)
            args.Heal = remainingHeal;
    }

    public static MedicalTransaction? CreateHealingTransaction(
        HumanMedicalComponent medical,
        DamageSpecifier heal,
        out DamageSpecifier remainingHeal)
    {
        HumanMedicalLedger.EnsureInitialized(medical);

        remainingHeal = StripLedgerHealing(heal);
        var bruteHeal = GetHealingAmount(heal, BruteTypes);
        var burnHeal = GetHealingAmount(heal, BurnTypes);
        if (bruteHeal <= FixedPoint2.Zero &&
            burnHeal <= FixedPoint2.Zero)
        {
            return null;
        }

        var transaction = new MedicalTransaction(BodyRegion.Chest);
        AddRegionRecoveryEffects(medical, transaction, InjuryKind.Bruise, bruteHeal);
        AddRegionRecoveryEffects(medical, transaction, InjuryKind.Burn, burnHeal);

        return transaction.Count > 0 ? transaction : null;
    }

    private static DamageSpecifier StripLedgerHealing(DamageSpecifier heal)
    {
        var remaining = new DamageSpecifier(heal);
        RemoveHealingTypes(remaining, BruteTypes);
        RemoveHealingTypes(remaining, BurnTypes);
        return remaining;
    }

    private static void RemoveHealingTypes(
        DamageSpecifier heal,
        IReadOnlyList<string> types)
    {
        for (var i = 0; i < types.Count; i++)
        {
            var type = types[i];
            if (heal.DamageDict.TryGetValue(type, out var value) &&
                value < FixedPoint2.Zero)
            {
                heal.DamageDict.Remove(type);
            }
        }
    }

    private static FixedPoint2 GetHealingAmount(
        DamageSpecifier heal,
        IReadOnlyList<string> types)
    {
        var amount = FixedPoint2.Zero;
        for (var i = 0; i < types.Count; i++)
        {
            if (heal.DamageDict.TryGetValue(types[i], out var value) &&
                value < FixedPoint2.Zero)
            {
                amount -= value;
            }
        }

        return amount;
    }

    private static void AddRegionRecoveryEffects(
        HumanMedicalComponent medical,
        MedicalTransaction transaction,
        InjuryKind repairKind,
        FixedPoint2 amount)
    {
        if (amount <= FixedPoint2.Zero)
            return;

        var remaining = amount;
        for (var i = 0; i < RegionRecoveryPriority.Length; i++)
        {
            var region = RegionRecoveryPriority[i];
            var floor = GetHealingFloor(medical, region, repairKind);
            var healable = FixedPoint2.Max(
                FixedPoint2.Zero,
                GetRegionDamage(medical, region, repairKind) - floor);

            if (healable <= FixedPoint2.Zero)
                continue;

            var repair = FixedPoint2.Min(remaining, healable);
            transaction.Add(MedicalEffect.RepairRegionDamage(region, repairKind, repair, floor));
            remaining -= repair;

            if (remaining <= FixedPoint2.Zero)
                return;
        }
    }

    private static FixedPoint2 GetRegionDamage(
        HumanMedicalComponent medical,
        BodyRegion region,
        InjuryKind repairKind)
    {
        var state = HumanMedicalLedger.GetRegion(medical, region);
        return repairKind == InjuryKind.Burn
            ? state.BurnDamage
            : state.BruteDamage;
    }

    private static FixedPoint2 GetHealingFloor(
        HumanMedicalComponent medical,
        BodyRegion region,
        InjuryKind repairKind)
    {
        if (repairKind != InjuryKind.Burn)
            return FixedPoint2.Zero;

        for (var i = 0; i < medical.Injuries.Count; i++)
        {
            var injury = medical.Injuries[i];
            if (injury.Region == region &&
                injury.Kind == InjuryKind.Burn &&
                injury.Flags.HasFlag(InjuryFlags.Necrotic))
            {
                return FixedPoint2.New(5);
            }
        }

        return FixedPoint2.Zero;
    }
}
