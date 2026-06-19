using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Map;

namespace Content.Shared._RMC14.Xenonids.Despoiler;

[RegisterComponent]
public sealed partial class XenoDespoilerEmbracingComponent : Component
{
    [DataField]
    public EntityCoordinates Landing;

    [DataField]
    public Vector2 Direction;

    [DataField]
    public Vector2 Origin;

    [DataField]
    public float MaxDistance;

    [DataField]
    public EntityUid? Action;
}
