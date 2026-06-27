using Content.Shared._CMU14.Chemistry.Reagent;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using System;
using System.Collections.Generic;
using System.Text;

namespace Content.Shared._AU14.Chemistry.Reagents;

//WARNING: EVENTSLOP AHEAD

[Serializable, NetSerializable]
public sealed class GenerateReagentEvent(GeneratedReagentData reagent) : EntityEventArgs
{
    public GeneratedReagentData Reagent = reagent;
}
[Serializable, NetSerializable]
public sealed class GeneratedReagentDataEvent(Dictionary<string, GeneratedReagentData> Reagents) : EntityEventArgs
{
    public Dictionary<string, GeneratedReagentData> Reagents = Reagents;
}
[Serializable, NetSerializable]
public sealed class RequestReagentGenerationEvent(GeneratedReagentData reagent) : CancellableEntityEventArgs
{
    public GeneratedReagentData Reagent = reagent;
}
[Serializable, NetSerializable]
public sealed class UpdateResearchConsoleEvent(List<GeneratedReagentData> reagents, TimeSpan nextUpdate) : EntityEventArgs
{
    public List<GeneratedReagentData> Reagents = reagents;
    public TimeSpan NextUpdate = nextUpdate;
}
[Serializable, NetSerializable]
public sealed class UpdateDataTerminalClearanceEvent(int clearance, int credits)
{
    public int Clearance = clearance;
    public int Credits = credits;
}

[Serializable, NetSerializable]
public sealed class TerminalPickReagentEvent(string reagent) : EntityEventArgs
{
    public string Reagent = reagent;
}
[Serializable, NetSerializable]
public sealed class XRFScannedReagentEvent(string reagent) : EntityEventArgs
{
    public string Reagent = reagent;
}

[Serializable, NetSerializable]
public sealed partial class XRFDoAfterEvent() : SimpleDoAfterEvent
{
}
