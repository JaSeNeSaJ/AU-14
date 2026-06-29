using Content.Shared._RMC14.Language.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._CMU14.Language;

[RegisterComponent, Access(typeof(CMUXenoLanguageSystem))]
public sealed partial class CMUXenoEnglishLanguageComponent : Component
{
    public bool HadSpokenEnglish;
    public bool HadUnderstoodEnglish;
    public ProtoId<LanguagePrototype> PreviousLanguage;
}
