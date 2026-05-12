using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared.DoAfter;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Components;
using Content.Shared.Popups;
using Robust.Shared.Network;

namespace Content.Shared._AU14.Abominations;

public sealed class AbominationAssimilateSystem : EntitySystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<AbominationAssimilateComponent, AbominationAssimilateActionEvent>(OnAssimilateAction);
        SubscribeLocalEvent<AbominationAssimilateComponent, AbominationAssimilateDoAfterEvent>(OnAssimilateDoAfter);
    }

    private void OnAssimilateAction(Entity<AbominationAssimilateComponent> mimic, ref AbominationAssimilateActionEvent args)
    {
        if (args.Handled)
            return;

        if (!CanAssimilate(mimic.Owner, args.Target, out var reason))
        {
            _popup.PopupClient(reason, mimic, mimic);
            return;
        }

        args.Handled = true;

        var ev = new AbominationAssimilateDoAfterEvent();
        var doAfter = new DoAfterArgs(EntityManager, mimic.Owner, mimic.Comp.DoAfter, ev, mimic.Owner, target: args.Target)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
            RequireCanInteract = true,
        };

        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnAssimilateDoAfter(Entity<AbominationAssimilateComponent> mimic, ref AbominationAssimilateDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        if (args.Args.Target is not { } target)
            return;

        if (!CanAssimilate(mimic.Owner, target, out var reason))
        {
            _popup.PopupClient(reason, mimic, mimic);
            return;
        }

        args.Handled = true;

        if (_net.IsClient)
            return;

        var profile = BuildProfile(target);

        var assimilated = EnsureComp<AbominationAssimilatedComponent>(target);
        assimilated.AssimilatedBy = mimic.Owner;
        assimilated.Profile = profile;
        Dirty(target, assimilated);

        if (TryComp<AbominationMimicComponent>(mimic.Owner, out var mimicComp))
        {
            mimicComp.AssimilatedPool.Add(profile);
            Dirty(mimic.Owner, mimicComp);
        }

        _popup.PopupEntity(Loc.GetString("abomination-assimilate-complete", ("target", Name(target))), target, mimic);
    }

    private bool CanAssimilate(EntityUid mimic, EntityUid target, out string reason)
    {
        reason = string.Empty;

        if (mimic == target)
        {
            reason = Loc.GetString("abomination-assimilate-self");
            return false;
        }

        if (!HasComp<HumanoidAppearanceComponent>(target))
        {
            reason = Loc.GetString("abomination-assimilate-not-humanoid");
            return false;
        }

        if (!_mobState.IsIncapacitated(target))
        {
            reason = Loc.GetString("abomination-assimilate-not-incapacitated");
            return false;
        }

        if (HasComp<AbominationAssimilatedComponent>(target))
        {
            reason = Loc.GetString("abomination-assimilate-already");
            return false;
        }

        if (HasComp<AbominationComponent>(target))
        {
            reason = Loc.GetString("abomination-assimilate-not-humanoid");
            return false;
        }

        return true;
    }

    private AbominationAssimilationProfile BuildProfile(EntityUid target)
    {
        var profile = new AbominationAssimilationProfile
        {
            Name = Name(target),
            SourceEntity = GetNetEntity(target),
        };

        if (TryComp<NpcFactionMemberComponent>(target, out var npcFaction))
        {
            foreach (var faction in npcFaction.Factions)
                profile.Factions.Add(faction);
        }

        if (TryComp<UserIFFComponent>(target, out var iff))
        {
            foreach (var faction in iff.Factions)
                profile.IffFactions.Add(faction);
        }

        if (TryComp<HumanoidAppearanceComponent>(target, out var humanoid))
            profile.Appearance = SnapshotAppearance(humanoid);

        return profile;
    }

    private static AbominationAppearanceSnapshot SnapshotAppearance(HumanoidAppearanceComponent humanoid)
    {
        return new AbominationAppearanceSnapshot
        {
            Species = humanoid.Species,
            SkinColor = humanoid.SkinColor,
            EyeColor = humanoid.EyeColor,
            Sex = humanoid.Sex,
            Gender = humanoid.Gender,
            Age = humanoid.Age,
            MarkingSet = new MarkingSet(humanoid.MarkingSet),
            CustomBaseLayers = new Dictionary<Content.Shared.Humanoid.HumanoidVisualLayers, CustomBaseLayerInfo>(humanoid.CustomBaseLayers),
        };
    }
}
