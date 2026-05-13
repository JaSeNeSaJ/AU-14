using Content.Server.Polymorph.Systems;
using Content.Shared._AU14.Abominations;
using Content.Shared._RMC14.Synth;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared.DoAfter;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Components;
using Content.Shared.Polymorph;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;

namespace Content.Server._AU14.Abominations;

public sealed class AbominationAssimilateSystem : EntitySystem
{
    /// <summary>
    /// Polymorph prototype used to turn the assimilated humanoid into an
    /// AU14AbominationMimic. One-way — does not revert on crit/death.
    /// </summary>
    public static readonly ProtoId<PolymorphPrototype> AssimilationPolymorph = "AbominationAssimilationToMimic";

    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly PolymorphSystem _polymorph = default!;
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

        var profile = BuildProfile(target);

        // Share the new profile with every existing mimic so the pool is
        // global to the threat ("any mimic can transform into any assimilated").
        AddProfileToAllMimics(profile);

        _popup.PopupEntity(Loc.GetString("abomination-assimilate-complete", ("target", Name(target))), target, mimic);

        // The victim becomes a mimic themselves. Polymorph swaps the entity
        // for an AU14AbominationMimic and transfers the player's mind.
        var newMimic = _polymorph.PolymorphEntity(target, AssimilationPolymorph);
        if (newMimic is { } newMimicUid)
        {
            // The freshly-spawned mimic starts with the full current pool so
            // it can immediately impersonate any prior victim, including itself.
            var newMimicComp = EnsureComp<AbominationMimicComponent>(newMimicUid);
            newMimicComp.AssimilatedPool = new List<AbominationAssimilationProfile>(GatherCurrentPool());
            Dirty(newMimicUid, newMimicComp);
        }
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

        // Synths have no flesh to absorb. Same flavour as xenos refusing to nest them.
        if (HasComp<SynthComponent>(target))
        {
            reason = Loc.GetString("abomination-assimilate-synth");
            return false;
        }

        if (!_mobState.IsIncapacitated(target))
        {
            reason = Loc.GetString("abomination-assimilate-not-incapacitated");
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
            CustomBaseLayers = new Dictionary<HumanoidVisualLayers, CustomBaseLayerInfo>(humanoid.CustomBaseLayers),
        };
    }

    private void AddProfileToAllMimics(AbominationAssimilationProfile profile)
    {
        var query = EntityQueryEnumerator<AbominationMimicComponent>();
        while (query.MoveNext(out var uid, out var mimic))
        {
            mimic.AssimilatedPool.Add(profile);
            Dirty(uid, mimic);
        }
    }

    private List<AbominationAssimilationProfile> GatherCurrentPool()
    {
        // First mimic with a non-empty pool acts as the source of truth.
        // AddProfileToAllMimics keeps them in lockstep, so any one will do.
        var query = EntityQueryEnumerator<AbominationMimicComponent>();
        while (query.MoveNext(out _, out var mimic))
        {
            if (mimic.AssimilatedPool.Count > 0)
                return new List<AbominationAssimilationProfile>(mimic.AssimilatedPool);
        }

        return new List<AbominationAssimilationProfile>();
    }
}
