// Content.Shared/_RMC14/Xenonids/Charge/CursorCharge/ActiveXenoChargerLungeComponent.cs

using System.Numerics;
using Content.Shared._RMC14.Xenonids.Charge.CursorCharge;
using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Xenonids.ChargerLunge;

/// <summary>
///     Added to a xeno while their lunge is actively executing.
///     Removed automatically when the lunge distance is exhausted or a stopping collision occurs.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ActiveXenoChargerLungeComponent : Component
{
    /// <summary>
    ///     World-space direction the lunge is traveling. Locked at activation time from
    ///     <see cref="XenoCursorSteeringComponent.CurrentHeading"/> (charged) or the
    ///     xeno's facing direction (standalone). Normalized.
    /// </summary>
    public Vector2 LungeDirection = Vector2.UnitX;

    /// <summary>
    ///     Remaining distance (tiles) before the lunge ends naturally.
    /// </summary>
    public float DistanceRemaining;

    /// <summary>
    ///     Snapshot of <see cref="XenoCursorSteeringComponent.Stage"/> at activation time.
    ///     0 when used standalone — drives all damage / speed / cc scaling.
    /// </summary>
    public int ChargeStageAtLunge;

    /// <summary>
    ///     Entities already struck during this lunge. Prevents the bowling-ball from
    ///     hitting the same target more than once per activation.
    /// </summary>
    public HashSet<EntityUid> HitEntities = new();
}
