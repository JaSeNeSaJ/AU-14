using System.Linq;
using Content.Server.Polymorph.Components;
using Content.Server.Polymorph.Systems;
using Content.Shared._AU14.Abominations;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Polymorph;
using Content.Shared.Popups;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._AU14.Abominations;

/// <summary>
/// Drives the mimic's transform action and disguise lifetime.
///
/// While disguised the mimic is polymorphed into a real MobHuman so it
/// shares all of a human's capabilities (sprite, hands, slots, talk).
/// We then patch the new human entity's appearance, name, factions, IFF and
/// skills from the snapshot stored on the chosen profile.
///
/// Reverts:
///  - 360s timer (configurable on AbominationMimicComponent.TransformDuration).
///  - PolymorphPrototype.RevertOnCrit / RevertOnDeath handle crit/death paths.
///  - Pressing the transform action while already disguised triggers an
///    explicit revert.
/// </summary>
public sealed class AbominationMimicSystem : EntitySystem
{
    public static readonly ProtoId<PolymorphPrototype> DisguisePolymorph = "AbominationMimicDisguise";

    [Dependency] private readonly NpcFactionSystem _faction = default!;
    [Dependency] private readonly GunIFFSystem _gunIff = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly SkillsSystem _skills = default!;
    [Dependency] private readonly SharedHumanoidAppearanceSystem _humanoid = default!;
    [Dependency] private readonly PolymorphSystem _polymorph = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<AbominationMimicComponent, AbominationMimicTransformActionEvent>(OnTransformAction);
        SubscribeLocalEvent<AbominationMimicComponent, AbominationMimicSelectFormMessage>(OnSelectForm);
    }

    private void OnTransformAction(Entity<AbominationMimicComponent> mimic, ref AbominationMimicTransformActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        // While disguised, the action acts as a quick revert.
        if (HasComp<AbominationMimicTransformedComponent>(mimic) &&
            HasComp<PolymorphedEntityComponent>(mimic))
        {
            _polymorph.Revert((mimic.Owner, null));
            return;
        }

        if (mimic.Comp.AssimilatedPool.Count == 0)
        {
            _popup.PopupClient(Loc.GetString("abomination-mimic-transform-no-profiles"), mimic, mimic);
            return;
        }

        _ui.TryOpenUi(mimic.Owner, AbominationMimicUiKey.Key, args.Performer);
        PushBuiState(mimic);
    }

    private void OnSelectForm(Entity<AbominationMimicComponent> mimic, ref AbominationMimicSelectFormMessage args)
    {
        if (args.Index < 0 || args.Index >= mimic.Comp.AssimilatedPool.Count)
            return;

        var profile = mimic.Comp.AssimilatedPool[args.Index];
        _ui.CloseUi(mimic.Owner, AbominationMimicUiKey.Key, args.Actor);
        StartDisguise(mimic, profile, mimic.Comp.TransformDuration);
    }

    private void PushBuiState(Entity<AbominationMimicComponent> mimic)
    {
        var names = mimic.Comp.AssimilatedPool.Select(p => p.Name).ToList();
        _ui.SetUiState(mimic.Owner, AbominationMimicUiKey.Key, new AbominationMimicBuiState(names, null));
    }

    /// <summary>
    /// Polymorph the mimic into a real MobHuman, then patch the disguise on top.
    /// The new human entity inherits the mimic's pool so it can transform again.
    /// </summary>
    public EntityUid? StartDisguise(Entity<AbominationMimicComponent> mimic, AbominationAssimilationProfile profile, TimeSpan duration)
    {
        var disguised = _polymorph.PolymorphEntity(mimic.Owner, DisguisePolymorph);
        if (disguised is not { } disguisedUid)
            return null;

        // Carry the mimic's pool + parameters forward so the disguised human
        // can still open the picker and pick a different form.
        var carried = EnsureComp<AbominationMimicComponent>(disguisedUid);
        carried.AssimilatedPool = new List<AbominationAssimilationProfile>(mimic.Comp.AssimilatedPool);
        carried.TransformDuration = mimic.Comp.TransformDuration;
        Dirty(disguisedUid, carried);

        // Mark the disguise so the update loop / explicit revert can find it.
        var tracker = EnsureComp<AbominationMimicTransformedComponent>(disguisedUid);
        tracker.Profile = profile;
        tracker.ExpiresAt = _timing.CurTime + duration;
        Dirty(disguisedUid, tracker);

        ApplyProfile(disguisedUid, profile);
        return disguisedUid;
    }

    private void ApplyProfile(EntityUid disguised, AbominationAssimilationProfile profile)
    {
        if (!string.IsNullOrWhiteSpace(profile.Name))
            _metaData.SetEntityName(disguised, profile.Name);

        ApplyFactions(disguised, profile.Factions);
        ApplyIffFactions(disguised, profile.IffFactions);
        CopySkillsFromSource(disguised, profile);
        ApplyAppearance(disguised, profile.Appearance);
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<AbominationMimicTransformedComponent, PolymorphedEntityComponent>();
        while (query.MoveNext(out var uid, out var disguised, out _))
        {
            if (disguised.ExpiresAt > now)
                continue;

            _polymorph.Revert((uid, null));
        }
    }

    private void ApplyFactions(EntityUid disguised, IEnumerable<string> factions)
    {
        if (!TryComp<NpcFactionMemberComponent>(disguised, out var npc))
            return;

        foreach (var existing in npc.Factions.ToArray())
            _faction.RemoveFaction((disguised, npc), existing.Id, dirty: false);

        foreach (var faction in factions)
            _faction.AddFaction((disguised, npc), faction);
    }

    private void ApplyIffFactions(EntityUid disguised, IEnumerable<string> iffFactions)
    {
        _gunIff.ClearUserFactions(disguised);
        foreach (var faction in iffFactions)
            _gunIff.AddUserFaction(disguised, faction);
    }

    private void CopySkillsFromSource(EntityUid disguised, AbominationAssimilationProfile profile)
    {
        if (profile.SourceEntity is not { } netSource ||
            !TryGetEntity(netSource, out var source) ||
            !TryComp<SkillsComponent>(source.Value, out var sourceSkills))
        {
            return;
        }

        _skills.SetSkills(disguised, new Dictionary<EntProtoId<SkillDefinitionComponent>, int>(sourceSkills.Skills));
    }

    private void ApplyAppearance(EntityUid disguised, AbominationAppearanceSnapshot? snapshot)
    {
        if (snapshot is null || string.IsNullOrEmpty(snapshot.Species.Id))
            return;

        if (!TryComp<HumanoidAppearanceComponent>(disguised, out var humanoid))
            return;

        _humanoid.SetSpecies(disguised, snapshot.Species, sync: false, humanoid);
        _humanoid.SetSex(disguised, snapshot.Sex, sync: false, humanoid);
        humanoid.SkinColor = snapshot.SkinColor;
        humanoid.EyeColor = snapshot.EyeColor;
        humanoid.Age = snapshot.Age;
        humanoid.Gender = snapshot.Gender;
        humanoid.MarkingSet = new MarkingSet(snapshot.MarkingSet);
        humanoid.CustomBaseLayers = new Dictionary<HumanoidVisualLayers, CustomBaseLayerInfo>(snapshot.CustomBaseLayers);
        Dirty(disguised, humanoid);
    }
}
