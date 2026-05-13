using System.Linq;
using Content.Server.Chat.Systems;
using Content.Server.Polymorph.Components;
using Content.Server.Polymorph.Systems;
using Content.Shared._AU14.Abominations;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared.Actions;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Jittering;
using Content.Shared.Mobs;
using Content.Shared.Stunnable;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Polymorph;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._AU14.Abominations;

/// <summary>
/// Drives the mimic's transform action and disguise lifetime.
///
/// Flow:
///  1. Transform action -> picker BUI -> StartDisguise polymorphs into a real
///     MobHuman, patches name/factions/IFF/skills/appearance, and grants the
///     disguised entity an explicit Revert button.
///  2. Disguise lasts <see cref="AbominationMimicComponent.TransformDuration"/>
///     (default 4.5min). Expiry, crit/death, and pressing the Revert button
///     all funnel through <see cref="BeginRevert"/>.
///  3. BeginRevert adds <see cref="AbominationMimicRevertingComponent"/> which
///     spends a couple of seconds jittering + screaming, then polymorph-reverts
///     the entity back into its combat form. The cooldown
///     (<see cref="AbominationMimicComponent.TransformCooldown"/>, default 5min)
///     is stamped onto the original mimic at this point.
/// </summary>
public sealed class AbominationMimicSystem : EntitySystem
{
    public static readonly ProtoId<PolymorphPrototype> DisguisePolymorph = "AbominationMimicDisguise";
    public static readonly EntProtoId RevertAction = "ActionAbominationMimicRevert";
    public static readonly ProtoId<EmotePrototype> ScreamEmote = "Scream";

    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly NpcFactionSystem _faction = default!;
    [Dependency] private readonly GunIFFSystem _gunIff = default!;
    [Dependency] private readonly SharedJitteringSystem _jitter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly SkillsSystem _skills = default!;
    [Dependency] private readonly SharedHumanoidAppearanceSystem _humanoid = default!;
    [Dependency] private readonly PolymorphSystem _polymorph = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<AbominationMimicComponent, AbominationMimicTransformActionEvent>(OnTransformAction);
        SubscribeLocalEvent<AbominationMimicComponent, AbominationMimicSelectFormMessage>(OnSelectForm);
        SubscribeLocalEvent<AbominationMimicTransformedComponent, AbominationMimicRevertActionEvent>(OnRevertAction);
        SubscribeLocalEvent<AbominationMimicTransformedComponent, MobStateChangedEvent>(OnDisguisedMobStateChanged);
    }

    private void OnTransformAction(Entity<AbominationMimicComponent> mimic, ref AbominationMimicTransformActionEvent args)
    {
        if (args.Handled)
            return;

        // Disguised mimics already have the Revert button; the Transform action
        // on the disguised form is a no-op so they don't double-pick.
        if (HasComp<AbominationMimicTransformedComponent>(mimic))
            return;

        if (mimic.Comp.NextTransformAt is { } cd && _timing.CurTime < cd)
        {
            _popup.PopupClient(Loc.GetString("abomination-mimic-on-cooldown"), mimic, mimic);
            return;
        }

        if (mimic.Comp.AssimilatedPool.Count == 0)
        {
            _popup.PopupClient(Loc.GetString("abomination-mimic-transform-no-profiles"), mimic, mimic);
            return;
        }

        args.Handled = true;
        _ui.TryOpenUi(mimic.Owner, AbominationMimicUiKey.Key, args.Performer);
        PushBuiState(mimic);
    }

    private void OnSelectForm(Entity<AbominationMimicComponent> mimic, ref AbominationMimicSelectFormMessage args)
    {
        if (args.Index < 0 || args.Index >= mimic.Comp.AssimilatedPool.Count)
            return;

        // Cooldown re-check in case they sat on the picker.
        if (mimic.Comp.NextTransformAt is { } cd && _timing.CurTime < cd)
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
    /// Polymorph the mimic into a real MobHuman, patch the disguise on top,
    /// and grant the disguised entity the Revert action.
    /// </summary>
    public EntityUid? StartDisguise(Entity<AbominationMimicComponent> mimic, AbominationAssimilationProfile profile, TimeSpan duration)
    {
        var disguised = _polymorph.PolymorphEntity(mimic.Owner, DisguisePolymorph);
        if (disguised is not { } disguisedUid)
            return null;

        var carried = EnsureComp<AbominationMimicComponent>(disguisedUid);
        carried.AssimilatedPool = new List<AbominationAssimilationProfile>(mimic.Comp.AssimilatedPool);
        carried.TransformDuration = mimic.Comp.TransformDuration;
        carried.TransformCooldown = mimic.Comp.TransformCooldown;
        Dirty(disguisedUid, carried);

        var tracker = EnsureComp<AbominationMimicTransformedComponent>(disguisedUid);
        tracker.Profile = profile;
        tracker.ExpiresAt = _timing.CurTime + duration;
        Dirty(disguisedUid, tracker);

        ApplyProfile(disguisedUid, profile);

        _actions.AddAction(disguisedUid, RevertAction);
        return disguisedUid;
    }

    private void OnRevertAction(Entity<AbominationMimicTransformedComponent> ent, ref AbominationMimicRevertActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        BeginRevert(ent.Owner);
    }

    private void OnDisguisedMobStateChanged(Entity<AbominationMimicTransformedComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState is MobState.Critical or MobState.Dead)
            BeginRevert(ent.Owner);
    }

    /// <summary>
    /// Start the shake+scream revert sequence. Idempotent: if a revert is
    /// already pending we leave the existing timer alone.
    /// </summary>
    private void BeginRevert(EntityUid mimic)
    {
        if (!HasComp<AbominationMimicTransformedComponent>(mimic))
            return;

        if (HasComp<AbominationMimicRevertingComponent>(mimic))
            return;

        var reverting = EnsureComp<AbominationMimicRevertingComponent>(mimic);
        reverting.RevertAt = _timing.CurTime + reverting.JitterDuration;
        Dirty(mimic, reverting);

        // Fall over, scream and shake for the entire 7s wind-down before the
        // polymorph revert fires. Jitter amplitude is high so the seizure is
        // visible at a glance.
        _jitter.DoJitter(mimic, reverting.JitterDuration, refresh: true, amplitude: 20, frequency: 18);
        _stun.TryParalyze(mimic, reverting.JitterDuration, true);
        _chat.TryEmoteWithChat(mimic, ScreamEmote);
        _popup.PopupClient(Loc.GetString("abomination-mimic-transform-revert"), mimic, mimic);
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;

        // Stage 1: trigger BeginRevert when the disguise's lifetime ends.
        var disguised = EntityQueryEnumerator<AbominationMimicTransformedComponent, PolymorphedEntityComponent>();
        while (disguised.MoveNext(out var uid, out var tracker, out _))
        {
            if (HasComp<AbominationMimicRevertingComponent>(uid))
                continue;
            if (tracker.ExpiresAt > now)
                continue;

            BeginRevert(uid);
        }

        // Stage 2: actually polymorph-revert once the shake-and-scream timer ends.
        var reverting = EntityQueryEnumerator<AbominationMimicRevertingComponent, PolymorphedEntityComponent>();
        while (reverting.MoveNext(out var uid, out var revert, out var polymorphed))
        {
            if (revert.RevertAt > now)
                continue;

            FinishRevert(uid, polymorphed);
        }
    }

    private void FinishRevert(EntityUid disguisedUid, PolymorphedEntityComponent polymorphed)
    {
        // Stamp the cooldown on the ORIGINAL mimic so the next transform is gated.
        if (TryComp<AbominationMimicComponent>(disguisedUid, out var disguisedMimic) &&
            TryComp<AbominationMimicComponent>(polymorphed.Parent, out var originalMimic))
        {
            originalMimic.AssimilatedPool = new List<AbominationAssimilationProfile>(disguisedMimic.AssimilatedPool);
            originalMimic.NextTransformAt = _timing.CurTime + disguisedMimic.TransformCooldown;
            Dirty(polymorphed.Parent, originalMimic);
        }

        _polymorph.Revert((disguisedUid, null));
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
