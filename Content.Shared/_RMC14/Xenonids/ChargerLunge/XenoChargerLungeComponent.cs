// Content.Shared/_RMC14/Xenonids/Charge/CursorCharge/XenoChargerLungeComponent.cs

using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Xenonids.ChargerLunge;

/// <summary>
///     Grants the xeno a lunge ability. Standalone: short dash with moderate damage and knockback.
///     When used during an active cursor charge, consumes all momentum and scales damage/speed/knockback
///     by the charge stage at the moment of activation.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class XenoChargerLungeComponent : Component
{
    [DataField] public SoundSpecifier CadeHitSound = new SoundPathSpecifier("/Audio/_RMC14/Effects/metal_crash.ogg");
    [DataField] public SoundSpecifier LungeSound = new SoundPathSpecifier("/Audio/_RMC14/Effects/meteorimpact.ogg");
    // --- Distance / speed ---

    /// <summary>Tiles traveled during a standalone lunge.</summary>
    [DataField] public float LungeDistance = 5f;

    /// <summary>Base movement speed during the lunge (tiles/s).</summary>
    [DataField] public float LungeSpeed = 14f;

    /// <summary>Additional speed added per charge stage when lunging out of a charge.</summary>
    [DataField] public float LungeSpeedPerStage = 3f;

    /// <summary>Additional tiles of lunge distance added per charge stage.</summary>
    [DataField] public float LungeDistancePerStage = 1.5f;

    // --- Standalone damage / cc ---

    /// <summary>Blunt damage dealt to each target during a standalone lunge.</summary>
    [DataField] public float StandaloneDamage = 30f;

    /// <summary>Knockback power applied to targets during a standalone lunge.</summary>
    [DataField] public float StandaloneKnockback = 3f;

    /// <summary>Knockdown duration (seconds) applied to targets during a standalone lunge.</summary>
    [DataField] public float StandaloneKnockdownDuration = 0.5f;

    // --- Charged damage / cc ---

    /// <summary>Base blunt damage per target when lunging out of a charge (before stage scaling).</summary>
    [DataField] public float ChargedDamageBase = 30f;

    /// <summary>Additional damage added per charge stage.</summary>
    [DataField] public float ChargedDamagePerStage = 15f;

    /// <summary>Base knockback power when lunging out of a charge.</summary>
    [DataField] public float ChargedKnockback = 4f;

    /// <summary>Knockdown duration (seconds) applied to targets during a charged lunge.</summary>
    [DataField] public float ChargedKnockdownDuration = 1.5f;

    // --- Cooldowns ---

    /// <summary>Cooldown when used standalone.</summary>
    [DataField] public TimeSpan Cooldown = TimeSpan.FromSeconds(8);

    /// <summary>Reduced cooldown rewarded for using the lunge during an active charge.</summary>
    [DataField] public TimeSpan ChargedCooldown = TimeSpan.FromSeconds(4);
}
