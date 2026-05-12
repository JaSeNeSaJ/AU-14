using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._AU14.Abominations;

/// <summary>
/// Applied to a mimic while it is currently disguised as one of its assimilated
/// profiles. Holds the data needed to restore the combat form cleanly on revert.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class AbominationMimicTransformedComponent : Component
{
    [DataField, AutoNetworkedField]
    public AbominationAssimilationProfile Profile = new();

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan ExpiresAt;

    [DataField, AutoNetworkedField]
    public string OriginalName = string.Empty;

    [DataField, AutoNetworkedField]
    public HashSet<string> OriginalFactions = new();

    [DataField, AutoNetworkedField]
    public HashSet<string> OriginalIffFactions = new();

    /// <summary>
    /// True if SkillsComponent was synthesised at transform time and should be
    /// stripped on revert.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool AddedSkillsOnApply;

    /// <summary>
    /// True if HumanoidAppearanceComponent was synthesised at transform time and
    /// should be stripped on revert.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool AddedHumanoidOnApply;
}
