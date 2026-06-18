using Content.Shared._CMU14.Medical.Human.Components;
using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.Network;

namespace Content.Shared._CMU14.Medical.Human.Systems;

public sealed partial class HumanMedicalDamageableBridgeSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private INetManager _net = default!;

    private static readonly string[] LedgerTraumaTypes =
    [
        "Blunt",
        "Slash",
        "Piercing",
        "Heat",
        "Shock",
        "Cold",
        "Caustic",
    ];

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HumanMedicalStartupEvent>(OnMedicalStartup);
        SubscribeLocalEvent<HumanMedicalLedgerChangedEvent>(OnLedgerChanged);
    }

    private void OnMedicalStartup(ref HumanMedicalStartupEvent args)
    {
        if (_net.IsClient)
            return;

        ProjectLedgerTrauma((args.Body, args.Medical));
    }

    private void OnLedgerChanged(ref HumanMedicalLedgerChangedEvent args)
    {
        if (_net.IsClient ||
            !TryComp<HumanMedicalComponent>(args.Body, out var medical))
        {
            return;
        }

        ProjectLedgerTrauma((args.Body, medical));
    }

    public void ProjectLedgerTrauma(Entity<HumanMedicalComponent> ent)
    {
        if (!TryComp<DamageableComponent>(ent.Owner, out var damageable))
            return;

        var projected = BuildProjectedDamage(ent.Comp, damageable.Damage);
        if (damageable.Damage.Equals(projected))
            return;

        _damageable.SetDamage(ent.Owner, damageable, projected);
    }

    public static DamageSpecifier BuildProjectedDamage(
        HumanMedicalComponent medical,
        DamageSpecifier existing)
    {
        var projected = new DamageSpecifier(existing);
        foreach (var type in LedgerTraumaTypes)
        {
            if (projected.DamageDict.ContainsKey(type))
                projected.DamageDict[type] = FixedPoint2.Zero;
        }

        var brute = FixedPoint2.Zero;
        var burn = FixedPoint2.Zero;
        foreach (var region in medical.Regions)
        {
            if (region.Region == BodyRegion.None)
                continue;

            brute += region.BruteDamage;
            burn += region.BurnDamage;
        }

        projected.DamageDict["Blunt"] = brute;
        projected.DamageDict["Heat"] = burn;
        return projected;
    }

    public static bool HasLedgerOwnedTrauma(DamageSpecifier damage)
    {
        foreach (var type in LedgerTraumaTypes)
        {
            if (damage.DamageDict.ContainsKey(type))
                return true;
        }

        return false;
    }
}
