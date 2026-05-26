//Pretty please license this under the MIT license :) - MACMAN2003
using System;
using System.Collections.Generic;
using System.Text;
using Content.Shared._CMU14.Chemistry.Effects.Negative;
using Content.Shared._CMU14.Chemistry.Effects.Neutral;
using Content.Shared._CMU14.Chemistry.Effects.Positive;
using Content.Shared._CMU14.Chemistry.Effects.Special;
using Content.Shared._CMU14.Chemistry.Effects.Reaction;
using Robust.Shared.Random;

namespace Content.Shared._AU14.Chemistry.Reagents;

public sealed partial class ReagentGeneratorSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
}
