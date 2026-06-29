using Content.Shared._RMC14.Attachable.Components;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Attachable;

[DataDefinition]
[Serializable, NetSerializable]
public partial struct AttachableSlot()
{
    [DataField]
    public bool Locked;

    [DataField]
    public EntityWhitelist? Whitelist;

    [DataField]
    public EntProtoId<AttachableComponent>? StartingAttachable;

    [DataField]
    public List<EntProtoId<AttachableComponent>>? Random;

    [DataField]
    public float RandomChance = 1f;
}

[DataRecord, Serializable, NetSerializable]
public partial record struct AttachableModifierConditions(
    bool UnwieldedOnly = false,
    bool WieldedOnly = false,
    bool ActiveOnly = false,
    bool InactiveOnly = false,
    EntityWhitelist? Whitelist = null,
    EntityWhitelist? Blacklist = null
);

[DataRecord, Serializable, NetSerializable]
public partial record struct AttachableWeaponMeleeModifierSet(
    AttachableModifierConditions? Conditions = null,
    DamageSpecifier? BonusDamage = null,
    DamageImpactProfile? Impact = null
);

[DataRecord, Serializable, NetSerializable]
public partial record struct AttachableWeaponRangedModifierSet(
    AttachableModifierConditions? Conditions = null,
    FixedPoint2 AccuracyAddMult = default, // Affects the accuracy of all shots fired by the weapon. Conversion from 13: accuracy_mod or accuracy_unwielded_mod
    FixedPoint2 DamageFalloffAddMult = default, // This affects the damage falloff of all shots fired by the weapon. Conversion to RMC: damage_falloff_mod
    double BurstScatterAddMult = 0, // This affects scatter during burst and full-auto fire. Conversion to RMC: burst_scatter_mod
    int ShotsPerBurstFlat = 0, // Modifies the maximum number of shots in a burst.
    FixedPoint2 DamageAddMult = default, // Additive multiplier to damage.
    float RecoilFlat = 0f, // How much the camera shakes when you shoot.
    double ScatterFlat = 0, // Scatter in degrees. This is how far bullets go from where you aim. Conversion to RMC: CM_SCATTER * 2
    float FireDelayFlat = 0f, // The delay between each shot. Conversion to RMC: CM_FIRE_DELAY / 10
    float ProjectileSpeedFlat = 0f, // How fast the projectiles move. Conversion to RMC: CM_PROJECTILE_SPEED * 10
    float RangeFlat = 0f // The distance in tiles at which the damage of the projectiles starts to drop off. Conversion to RMC: projectile_max_range_mod
);

[DataRecord, Serializable, NetSerializable]
public partial record struct AttachableWeaponFireModesModifierSet
{
    public AttachableWeaponFireModesModifierSet()
    {
    }

    public AttachableModifierConditions? Conditions { get; set; }
    public SelectiveFire ExtraFireModes { get; set; }
    public SelectiveFire SetFireMode { get; set; }
}

// SS13 has move delay instead of speed. Move delay isn't implemented here, and approximating it through maths like fire delay is scuffed because of how the events used to change speed work.
// So instead we take the default speed values and use them to convert it to a multiplier beforehand.
// Converting from move delay to additive multiplier: 1 / (1 / SS14_SPEED + SS13_MOVE_DELAY / 10) / SS14_SPEED - 1
// Speed and move delay are inversely proportional. So 1 divided by speed is move delay and vice versa.
// We then add the ss13 move delay, and divide 1 by the result to convert it back into speed.
// Then we divide it by the original speed and subtract 1 from the result to get the additive multiplier.
[DataRecord, Serializable, NetSerializable]
public partial record struct AttachableSpeedModifierSet(
    AttachableModifierConditions? Conditions = null,
    float Walk = 0f, // Default human walk speed: 2.5f
    float Sprint = 0f // Default human sprint speed: 4.5f
);

[DataRecord, Serializable, NetSerializable]
public partial record struct AttachableSizeModifierSet(
    AttachableModifierConditions? Conditions = null,
    int Size = 0
);

[DataRecord, Serializable, NetSerializable]
public partial record struct AttachableWieldDelayModifierSet(
    AttachableModifierConditions? Conditions = null,
    TimeSpan Delay = default
);
