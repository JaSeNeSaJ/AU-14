using System.Numerics;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Xenonids.Charge.CursorCharge;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class XenoChargerComponent : Component
{

    // --- Charge tuning ---
    [DataField] public int MaxStage = 8;
    [DataField] public float DistancePerStage = 1f; //placeholder for ease of test
    [DataField] public float BaseSpeed = 4f;
    [DataField] public float SpeedPerStage = 1f;
    [DataField] public float BaseTurnRate = 3f;
    [DataField] public float MinTurnRate = 1f;
    [DataField] public float SoundEveryDistance = 10f;

    // --- Charge collision tuning ---
    [DataField] public float HumanDamageMultiplier = 5f;
    [DataField] public float HumanDamageMultiplierMax = 10f;
    [DataField] public float HumanKnockdownDuration = 1f;
    [DataField] public float BarricadeCollisionDamage = 20f;
    [DataField] public float StructureDamageMultiplier = 15f;

    // --- Lunge tuning ---
    [DataField] public float LungeDistance = 3f;
    [DataField] public float LungeSpeed = 12f;
    [DataField] public float LungeSpeedPerStage = 2f;
    [DataField] public float LungeDistancePerStage = 1.75f;

    // --- Lunge standalone cc ---
    [DataField] public float StandaloneDamage = 30f;
    [DataField] public float StandaloneKnockback = 1f;
    [DataField] public float StandaloneKnockdownDuration = 0.5f;

    // --- Lunge charged cc ---
    [DataField] public float ChargedDamageBase = 30f;
    [DataField] public float ChargedDamagePerStage = 15f;
    [DataField] public float ChargedKnockback = 2f;
    [DataField] public float ChargedKnockdownDuration = 1.5f;

    // --- SFX
    [DataField, AutoNetworkedField] public SoundSpecifier CadeHitSound = new SoundPathSpecifier("/Audio/_RMC14/Effects/metal_crash.ogg");
    [DataField, AutoNetworkedField] public SoundSpecifier? ChargeSound = new SoundPathSpecifier("/Audio/_RMC14/Xeno/alien_footstep_charge1.ogg", AudioParams.Default.WithVolume(-4));

    // --- Cooldowns ---
    [DataField] public TimeSpan ChargeCooldown = TimeSpan.FromSeconds(3);
    [DataField] public TimeSpan LungeCooldown = TimeSpan.FromSeconds(8);
    [DataField] public TimeSpan LungeChargedCooldown = TimeSpan.FromSeconds(4);
}
