using Content.Shared._RMC14.Xenonids.Charge.CursorCharge;
using Content.Shared._RMC14.Xenonids.ChargerLunge;
using Content.Shared.Actions;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;

namespace Content.Shared._RMC14.Xenonids.Charge.CursorCharge;

public sealed class XenoChargerActionSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly XenoChargerMovementSystem _movement = default!;

    /*
     * Outstanding issues
     * - Jitter on charge start
     * - Movement inputs not blocked
     * - lunge direction unintuitive.
     *
     * - Charge-Lunge loop works, nice.
     */

    public override void Initialize()
    {
        SubscribeLocalEvent<XenoChargerComponent, XenoToggleChargingActionEvent>(OnToggleCharge);
    }

    private void OnToggleCharge(Entity<XenoChargerComponent> ent, ref XenoToggleChargingActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        switch (ent.Comp.MoveState)
        {
            case XenoChargerMoveState.Idle:
                _movement.StartCharge(ent);
                _actions.SetCooldown(args.Action.Owner, ent.Comp.ChargeCooldown);

                if (_net.IsServer)
                {
                    _popup.PopupEntity(
                        Loc.GetString("rmc-xeno-charge-start", ("xeno", ent.Owner)),
                        ent,
                        PopupType.Small
                    );
                }
                break;

            case XenoChargerMoveState.Charging:
                var isCharged = ent.Comp.Stage > 0;
                var cooldown = isCharged ? ent.Comp.LungeChargedCooldown : ent.Comp.LungeCooldown;
                _movement.StartLunge(ent);
                _actions.SetCooldown(args.Action.Owner, cooldown);

                if (_net.IsServer)
                {
                    var msgKey = isCharged ? "rmc-xeno-lunge-charged-activate" : "rmc-xeno-lunge-activate";
                    _popup.PopupEntity(Loc.GetString(msgKey, ("xeno", ent.Owner)), ent, PopupType.Small);
                    _audio.PlayPvs(new SoundPathSpecifier("/Audio/_RMC14/Xeno/alien_pounce.ogg"), ent);
                }
                break;

            case XenoChargerMoveState.Lunging:
                // Ignore input while lunge is executing.
                break;
        }
    }
}
