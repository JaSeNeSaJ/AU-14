using Content.Shared._CMU14.Medical.Human.Components;
using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared.FixedPoint;
using Robust.Shared.IoC;
using Robust.Shared.Timing;

namespace Content.Shared._CMU14.Medical.Human.Systems;

public sealed partial class HumanTreatedWoundHealingSystem : EntitySystem
{
    [Dependency] private SharedHumanMedicalSystem _medical = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<HumanMedicalComponent, ActiveTreatedWoundHealingComponent>();
        while (query.MoveNext(out var uid, out var medical, out var active))
        {
            if (!HumanMedicalWorkerTiming.TryGetElapsed(
                    now,
                    ref active.LastUpdate,
                    ref active.NextUpdate,
                    out var elapsed))
            {
                continue;
            }

            var tick = CalculateTreatedWoundHealingTick(medical);
            if (!tick.HasActiveHealing)
            {
                _medical.RefreshActiveMarkers(uid, medical);
                continue;
            }

            var result = HumanMedicalLedger.AdvanceTreatedWoundHealing(medical, elapsed);
            if (!result.Applied)
            {
                _medical.RefreshActiveMarkers(uid, medical);
                continue;
            }

            _medical.NotifyLedgerChanged((uid, medical), result);

            var ev = new HumanTreatedWoundHealingTickEvent(
                uid,
                result,
                tick.ActiveInjuries,
                result.BruteHealed,
                result.BurnHealed,
                elapsed.Float());
            RaiseLocalEvent(uid, ref ev);
        }
    }

    public static HumanTreatedWoundHealingTick CalculateTreatedWoundHealingTick(HumanMedicalComponent medical)
    {
        var activeInjuries = 0;
        var bruteRecoveryRates = new FixedPoint2[HumanMedicalComponent.RegionSlotCount];
        var burnRecoveryRates = new FixedPoint2[HumanMedicalComponent.RegionSlotCount];

        foreach (var injury in medical.Injuries)
        {
            if (!HumanMedicalLedger.CanTreatedInjuryRecover(injury))
                continue;

            var regionIndex = (int) injury.Region;
            if (regionIndex <= 0 ||
                regionIndex >= medical.Regions.Length ||
                regionIndex >= HumanMedicalComponent.RegionSlotCount ||
                medical.Regions[regionIndex].Region != injury.Region)
            {
                continue;
            }

            activeInjuries++;
            if (injury.Kind == InjuryKind.Burn)
                burnRecoveryRates[regionIndex] = FixedPoint2.Max(burnRecoveryRates[regionIndex], injury.RecoveryRate);
            else
                bruteRecoveryRates[regionIndex] = FixedPoint2.Max(bruteRecoveryRates[regionIndex], injury.RecoveryRate);
        }

        var bruteRecoveryRate = FixedPoint2.Zero;
        var burnRecoveryRate = FixedPoint2.Zero;
        for (var i = 1; i < HumanMedicalComponent.RegionSlotCount; i++)
        {
            bruteRecoveryRate += bruteRecoveryRates[i];
            burnRecoveryRate += burnRecoveryRates[i];
        }

        return new HumanTreatedWoundHealingTick(activeInjuries, bruteRecoveryRate, burnRecoveryRate);
    }
}

public readonly record struct HumanTreatedWoundHealingTick(
    int ActiveInjuries,
    FixedPoint2 BruteRecoveryRate,
    FixedPoint2 BurnRecoveryRate)
{
    public bool HasActiveHealing => ActiveInjuries > 0 &&
        (BruteRecoveryRate > FixedPoint2.Zero || BurnRecoveryRate > FixedPoint2.Zero);
}

[ByRefEvent]
public readonly record struct HumanTreatedWoundHealingTickEvent(
    EntityUid Body,
    MedicalTransactionResult Result,
    int ActiveInjuriesBeforeTick,
    FixedPoint2 BruteHealed,
    FixedPoint2 BurnHealed,
    float FrameTime);
