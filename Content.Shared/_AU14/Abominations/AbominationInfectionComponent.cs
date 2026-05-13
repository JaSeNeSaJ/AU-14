using Content.Shared.Damage;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._AU14.Abominations;

/// <summary>
/// Applied to a humanoid hit by a mimic. Causes periodic toxin/cough/drunkenness
/// ticks; after CrescendoAfter elapsed the victim crescendos into rapid jitter
/// and frequent vomiting. Dying while infected seeds flesh kudzu at the corpse.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class AbominationInfectionComponent : Component
{
    /// <summary>When the infection was applied (server time).</summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan InfectedAt;

    /// <summary>How long until the symptoms escalate to the seizure/vomiting phase.</summary>
    [DataField, AutoNetworkedField]
    public TimeSpan CrescendoAfter = TimeSpan.FromMinutes(8);

    /// <summary>Has the crescendo (jitter + vomit) phase started yet?</summary>
    [DataField, AutoNetworkedField]
    public bool HasCrescendoed;

    /// <summary>How often the main symptom tick runs.</summary>
    [DataField, AutoNetworkedField]
    public TimeSpan TickInterval = TimeSpan.FromSeconds(6);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan NextTickAt;

    /// <summary>Damage applied each symptom tick.</summary>
    [DataField, AutoNetworkedField]
    public DamageSpecifier TickDamage = new();

    /// <summary>How long Drunk status is applied each symptom tick (early phase).</summary>
    [DataField, AutoNetworkedField]
    public TimeSpan DrunkPerTick = TimeSpan.FromSeconds(10);

    /// <summary>How often coughs fire during the early phase.</summary>
    [DataField, AutoNetworkedField]
    public TimeSpan CoughInterval = TimeSpan.FromSeconds(12);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan NextCoughAt;

    /// <summary>How often the late-phase vomiting fires.</summary>
    [DataField, AutoNetworkedField]
    public TimeSpan VomitInterval = TimeSpan.FromSeconds(15);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan NextVomitAt;
}
