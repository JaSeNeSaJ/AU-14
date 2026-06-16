using Content.Shared._CMU14.Chemistry.Reagent;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using System;
using System.Collections.Generic;
using System.Text;

namespace Content.Shared._AU14.Chemistry.Reagents;

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
