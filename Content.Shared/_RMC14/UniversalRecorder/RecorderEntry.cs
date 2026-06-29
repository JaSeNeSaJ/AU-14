using Content.Shared._RMC14.Language.Prototypes;
using Robust.Shared.Prototypes;

public readonly record struct RecorderEntry(
    TimeSpan Timestamp,
    string SpeakerName,
    string SpeechVerb,
    ProtoId<LanguagePrototype> Language,
    string Text,
    string FontId,
    int FontSize,
    bool Bold,
    string TranscriptLine
);