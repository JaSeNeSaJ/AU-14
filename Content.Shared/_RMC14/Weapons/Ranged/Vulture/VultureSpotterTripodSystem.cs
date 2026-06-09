using Content.Shared.Containers.ItemSlots;
using Content.Shared.Interaction;
using Content.Shared._RMC14.Scoping;
using Robust.Shared.Containers;

namespace Content.Shared._RMC14.Weapons.Ranged.Vulture;

public sealed partial class VultureSpotterTripodSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedScopeSystem _scope = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<VultureSpotterTripodComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<VultureSpotterTripodComponent, EntInsertedIntoContainerMessage>(OnContainerChanged);
        SubscribeLocalEvent<VultureSpotterTripodComponent, EntRemovedFromContainerMessage>(OnContainerChanged);
        SubscribeLocalEvent<VultureSpotterTripodComponent, InteractHandEvent>(OnInteractHand);
    }

    private void OnInit(Entity<VultureSpotterTripodComponent> ent, ref ComponentInit args)
    {
        UpdateVisuals(ent);
    }

    private void OnContainerChanged(Entity<VultureSpotterTripodComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        OnContainerChanged(ent, args.Container.ID);
    }

    private void OnContainerChanged(Entity<VultureSpotterTripodComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        OnContainerChanged(ent, args.Container.ID);
    }

    private void OnContainerChanged(Entity<VultureSpotterTripodComponent> ent, string containerId)
    {
        if (containerId == ent.Comp.ScopeSlot)
            UpdateVisuals(ent);
    }

    private void OnInteractHand(Entity<VultureSpotterTripodComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled ||
            !TryComp(ent, out ItemSlotsComponent? slots) ||
            !_itemSlots.TryGetSlot(ent.Owner, ent.Comp.ScopeSlot, out var slot, slots) ||
            slot.Item is not { } scopeUid ||
            !TryComp(scopeUid, out ScopeComponent? scope))
        {
            return;
        }

        if (scope.User == args.User)
            _scope.Unscope((scopeUid, scope));
        else
            _scope.StartScoping((scopeUid, scope), args.User);

        args.Handled = true;
    }

    private void UpdateVisuals(Entity<VultureSpotterTripodComponent> ent)
    {
        var hasScope =
            TryComp(ent, out ItemSlotsComponent? slots) &&
            _itemSlots.TryGetSlot(ent.Owner, ent.Comp.ScopeSlot, out var slot, slots) &&
            slot.HasItem;

        _appearance.SetData(ent.Owner, VultureSpotterTripodVisuals.HasScope, hasScope);
    }
}
