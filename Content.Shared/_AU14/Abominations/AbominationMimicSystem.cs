using System.Linq;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Mobs;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Popups;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Player;

namespace Content.Shared._AU14.Abominations;

/// <summary>
/// Drives the mimic's transform action and disguise lifetime.
///
/// Wired up:
///  - Action -> opens the form-picker BUI (handled by AbominationMimicBuiSystem on the server).
///  - Update loop expires the disguise after AbominationMimicComponent.TransformDuration.
///  - MobStateChangedEvent reverts the disguise on crit/dead.
///  - ApplyDisguise swaps name, NpcFactionMember factions, UserIFF factions, copies
///    SkillsComponent and HumanoidAppearance fields (species/sex/age/skin/eyes/markings/etc.)
///    from the snapshot taken at assimilation time.
///  - RestoreCombatForm reverses every change.
/// </summary>
public sealed class AbominationMimicSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly NpcFactionSystem _faction = default!;
    [Dependency] private readonly GunIFFSystem _gunIff = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly SkillsSystem _skills = default!;
    [Dependency] private readonly SharedHumanoidAppearanceSystem _humanoid = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<AbominationMimicComponent, AbominationMimicTransformActionEvent>(OnTransformAction);
        SubscribeLocalEvent<AbominationMimicComponent, AbominationMimicSelectFormMessage>(OnSelectForm);
        SubscribeLocalEvent<AbominationMimicTransformedComponent, MobStateChangedEvent>(OnDisguisedMobStateChanged);
    }

    private void OnTransformAction(Entity<AbominationMimicComponent> mimic, ref AbominationMimicTransformActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        // While disguised, the action acts as a quick revert.
        if (HasComp<AbominationMimicTransformedComponent>(mimic))
        {
            RemoveDisguise(mimic.Owner);
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
        StartDisguise(mimic.Owner, profile, mimic.Comp.TransformDuration);
        _ui.CloseUi(mimic.Owner, AbominationMimicUiKey.Key, args.Actor);
    }

    private void PushBuiState(Entity<AbominationMimicComponent> mimic)
    {
        var names = mimic.Comp.AssimilatedPool.Select(p => p.Name).ToList();
        int? active = null;
        if (TryComp<AbominationMimicTransformedComponent>(mimic, out var disguised))
        {
            for (var i = 0; i < mimic.Comp.AssimilatedPool.Count; i++)
            {
                if (ReferenceEquals(mimic.Comp.AssimilatedPool[i], disguised.Profile))
                {
                    active = i;
                    break;
                }
            }
        }

        _ui.SetUiState(mimic.Owner, AbominationMimicUiKey.Key, new AbominationMimicBuiState(names, active));
    }

    private void OnDisguisedMobStateChanged(Entity<AbominationMimicTransformedComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState is MobState.Critical or MobState.Dead)
            RemoveDisguise(ent.Owner);
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<AbominationMimicTransformedComponent>();
        while (query.MoveNext(out var uid, out var disguised))
        {
            if (disguised.ExpiresAt > now)
                continue;

            RemoveDisguise(uid);
        }
    }

    /// <summary>
    /// Begin a disguise. Snapshots the combat form, then writes the profile onto the mimic.
    /// If the mimic is already disguised this is a no-op (server should reject first).
    /// </summary>
    public void StartDisguise(EntityUid mimic, AbominationAssimilationProfile profile, TimeSpan duration)
    {
        if (HasComp<AbominationMimicTransformedComponent>(mimic))
            return;

        var disguise = EnsureComp<AbominationMimicTransformedComponent>(mimic);
        disguise.Profile = profile;
        disguise.ExpiresAt = _timing.CurTime + duration;
        disguise.OriginalName = Name(mimic);
        disguise.OriginalFactions = TryComp<NpcFactionMemberComponent>(mimic, out var npc)
            ? new HashSet<string>(npc.Factions.Select(f => f.Id))
            : new HashSet<string>();
        disguise.OriginalIffFactions = TryComp<UserIFFComponent>(mimic, out var iff)
            ? new HashSet<string>(iff.Factions.Select(f => f.Id))
            : new HashSet<string>();
        disguise.AddedSkillsOnApply = !HasComp<SkillsComponent>(mimic);
        disguise.AddedHumanoidOnApply = !HasComp<HumanoidAppearanceComponent>(mimic);
        Dirty(mimic, disguise);

        ApplyDisguise(mimic, profile);
    }

    private void ApplyDisguise(EntityUid mimic, AbominationAssimilationProfile profile)
    {
        if (!string.IsNullOrWhiteSpace(profile.Name))
            _metaData.SetEntityName(mimic, profile.Name);

        SwapFactions(mimic, profile.Factions);
        SwapIffFactions(mimic, profile.IffFactions);
        CopySkillsFromSource(mimic, profile);
        ApplyAppearance(mimic, profile.Appearance);
    }

    public void RemoveDisguise(EntityUid mimic)
    {
        if (!TryComp<AbominationMimicTransformedComponent>(mimic, out var disguise))
            return;

        RestoreCombatForm(mimic, disguise);
        RemCompDeferred<AbominationMimicTransformedComponent>(mimic);

        _popup.PopupClient(Loc.GetString("abomination-mimic-transform-revert"), mimic, mimic);
    }

    private void RestoreCombatForm(EntityUid mimic, AbominationMimicTransformedComponent disguise)
    {
        if (!string.IsNullOrEmpty(disguise.OriginalName))
            _metaData.SetEntityName(mimic, disguise.OriginalName);

        SwapFactions(mimic, disguise.OriginalFactions);
        SwapIffFactions(mimic, disguise.OriginalIffFactions);

        if (disguise.AddedSkillsOnApply)
            RemComp<SkillsComponent>(mimic);

        if (disguise.AddedHumanoidOnApply)
            RemComp<HumanoidAppearanceComponent>(mimic);
    }

    private void SwapFactions(EntityUid mimic, IEnumerable<string> factions)
    {
        if (!TryComp<NpcFactionMemberComponent>(mimic, out var npc))
            return;

        foreach (var existing in npc.Factions.ToArray())
            _faction.RemoveFaction((mimic, npc), existing.Id, dirty: false);

        foreach (var faction in factions)
            _faction.AddFaction((mimic, npc), faction);
    }

    private void SwapIffFactions(EntityUid mimic, IEnumerable<string> iffFactions)
    {
        if (!HasComp<UserIFFComponent>(mimic))
            return;

        _gunIff.ClearUserFactions(mimic);
        foreach (var faction in iffFactions)
            _gunIff.AddUserFaction(mimic, faction);
    }

    private void CopySkillsFromSource(EntityUid mimic, AbominationAssimilationProfile profile)
    {
        if (profile.SourceEntity is not { } source ||
            !Exists(source) ||
            !TryComp<SkillsComponent>(source, out var sourceSkills))
        {
            return;
        }

        _skills.SetSkills(mimic, new Dictionary<EntProtoId<SkillDefinitionComponent>, int>(sourceSkills.Skills));
    }

    private void ApplyAppearance(EntityUid mimic, AbominationAppearanceSnapshot? snapshot)
    {
        if (snapshot is null || string.IsNullOrEmpty(snapshot.Species.Id))
            return;

        var humanoid = EnsureComp<HumanoidAppearanceComponent>(mimic);

        // Species must be set before sex so the species' default sprite layers exist.
        _humanoid.SetSpecies(mimic, snapshot.Species, sync: false, humanoid);
        _humanoid.SetSex(mimic, snapshot.Sex, sync: false, humanoid);
        humanoid.SkinColor = snapshot.SkinColor;
        humanoid.EyeColor = snapshot.EyeColor;
        humanoid.Age = snapshot.Age;
        humanoid.Gender = snapshot.Gender;
        humanoid.MarkingSet = new MarkingSet(snapshot.MarkingSet);
        humanoid.CustomBaseLayers = new Dictionary<HumanoidVisualLayers, CustomBaseLayerInfo>(snapshot.CustomBaseLayers);

        Dirty(mimic, humanoid);
    }
}
