using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._AU14.Abominations;

/// <summary>
/// Behaviour for the flesh kudzu tile. Heals abominations standing on it,
/// and periodically plays a sob / gasp / breathing sound.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class AbominationFleshKudzuComponent : Component
{
    /// <summary>How often the heal tick runs.</summary>
    [DataField, AutoNetworkedField]
    public TimeSpan HealInterval = TimeSpan.FromSeconds(2);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan NextHealAt;

    /// <summary>Damage applied (typically negative) to abominations in contact each tick.</summary>
    [DataField, AutoNetworkedField]
    public DamageSpecifier Heal = new();

    /// <summary>Minimum delay between sob/gasp sound plays on this tile.</summary>
    [DataField, AutoNetworkedField]
    public TimeSpan SobIntervalMin = TimeSpan.FromSeconds(20);

    [DataField, AutoNetworkedField]
    public TimeSpan SobIntervalMax = TimeSpan.FromSeconds(60);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan NextSobAt;

    [DataField, AutoNetworkedField]
    public SoundSpecifier? SobSound = new SoundCollectionSpecifier("MaleScreams");
}
