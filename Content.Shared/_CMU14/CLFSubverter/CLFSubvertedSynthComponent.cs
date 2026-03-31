

using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.CLFSubverter;


[RegisterComponent, NetworkedComponent]
public sealed partial class CLFSubvertedSynthComponent : Component
{
    [DataField]
    public SoundSpecifier CLFSubversionSound = new SoundPathSpecifier("/Audio/Ambience/Antag/headrev_start.ogg");

    public override bool SessionSpecific => true;
}
