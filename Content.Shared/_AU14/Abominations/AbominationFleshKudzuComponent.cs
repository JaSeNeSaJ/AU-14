using Content.Shared.Chat.Prototypes;
using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._AU14.Abominations;

/// <summary>
/// Behaviour for the flesh kudzu tile. Heals abominations standing on it
/// and periodically vents emotes (crying / gasping) for ambience.
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

    /// <summary>Minimum delay between vocal emotes on this tile.</summary>
    [DataField, AutoNetworkedField]
    public TimeSpan EmoteIntervalMin = TimeSpan.FromSeconds(20);

    [DataField, AutoNetworkedField]
    public TimeSpan EmoteIntervalMax = TimeSpan.FromSeconds(60);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan NextEmoteAt;

    /// <summary>
    /// Emotes the kudzu can vent — picked from at random. Defaults are the
    /// existing speech emotes so the kudzu sobs / gasps audibly.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<ProtoId<EmotePrototype>> Emotes = new()
    {
        "Crying",
        "Gasp",
        "Scream",
    };

    /// <summary>
    /// Sound collections played alongside the emote (the emote system itself
    /// doesn't play sound for non-humanoid emitters). Pulled at random.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<SoundSpecifier> EmoteSounds = new()
    {
        new SoundCollectionSpecifier("MaleScreams"),
        new SoundCollectionSpecifier("FemaleScreams"),
    };
}
