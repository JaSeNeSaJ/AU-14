using System.Linq;
using Content.Shared._AU14.Abominations.Abilities;
using Content.Shared.Popups;

namespace Content.Server._AU14.Abominations;

public sealed class AbominationConstructionSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<AbominationConstructionComponent, AbominationConstructionChooseActionEvent>(OnChooseAction);
        SubscribeLocalEvent<AbominationConstructionComponent, AbominationConstructionChooseMessage>(OnChooseMessage);
        SubscribeLocalEvent<AbominationConstructionComponent, AbominationConstructionSecreteActionEvent>(OnSecreteAction);
    }

    private void OnChooseAction(Entity<AbominationConstructionComponent> ent, ref AbominationConstructionChooseActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        _ui.TryOpenUi(ent.Owner, AbominationConstructionUiKey.Key, args.Performer);
        PushBuiState(ent);
    }

    private void OnChooseMessage(Entity<AbominationConstructionComponent> ent, ref AbominationConstructionChooseMessage args)
    {
        if (!ent.Comp.CanBuild.Contains(args.Structure))
            return;

        ent.Comp.BuildChoice = args.Structure;
        Dirty(ent);
        PushBuiState(ent);
    }

    private void OnSecreteAction(Entity<AbominationConstructionComponent> ent, ref AbominationConstructionSecreteActionEvent args)
    {
        if (args.Handled)
            return;

        if (ent.Comp.BuildChoice is not { } choice)
        {
            _popup.PopupClient(Loc.GetString("abomination-secrete-no-choice"), ent, ent);
            return;
        }

        args.Handled = true;

        var target = _transform.ToMapCoordinates(args.Target);
        Spawn(choice, target);
    }

    private void PushBuiState(Entity<AbominationConstructionComponent> ent)
    {
        var options = ent.Comp.CanBuild.Select(id => id.Id).ToList();
        _ui.SetUiState(ent.Owner, AbominationConstructionUiKey.Key,
            new AbominationConstructionBuiState(options, ent.Comp.BuildChoice?.Id));
    }
}
