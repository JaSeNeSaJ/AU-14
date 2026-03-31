using Content.Server.Administration.Logs;
using Content.Server.Antag;
using Content.Server.GameTicking.Rules;
using Content.Server.Mind;
using Content.Server.Popups;
using Content.Server.Roles;
using Content.Shared._CMU14.CLFSubverter;
using Content.Shared._RMC14.Medical.Defibrillator;
using Content.Shared._RMC14.Synth;
using Content.Shared.Database;
using Content.Shared.Medical;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Client.GameStates;
using Robust.Shared.Network;

namespace Content.Server._CMU14.CLFSubvertedSynth;

//bashed together from the revolutionary stuff

public sealed class CLFSubvertedSynthRuleSystem : GameRuleSystem<CLFSubvertedSynthRuleComponent>
{
    [Dependency] private readonly AntagSelectionSystem _antag = default!;
    [Dependency] private readonly IAdminLogManager _adminLogManager = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly NpcFactionSystem _npcFaction = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly RoleSystem _role = default!;
    [Dependency] private readonly ISharedPlayerManager _player = default!;
    [Dependency] private readonly SharedSynthSystem _synth = default!;




    public readonly ProtoId<NpcFactionPrototype> CLFNPCFaction = "CLF";

    public override void Initialize()
    {
        base.Initialize();
        //TargetBeforeDefibrillatorZapsEvent doesn't work for some godawful reason
        SubscribeLocalEvent<CLFSubverterComponent, RMCDefibrillatorDamageModifyEvent>(OnSynthRevive);
    }

    private void OnSynthRevive(EntityUid uid, CLFSubverterComponent comp, ref RMCDefibrillatorDamageModifyEvent args)
    {
        if (!HasComp<SynthComponent>(args.Target))
        {
            return;
        }

        if (!_mind.TryGetMind(args.Target, out var mindId, out var mind))
            return;

        _npcFaction.AddFaction(args.Target, CLFNPCFaction);
        _synth.SetGunRestriction(args.Target, true);
        _synth.SetMeleeRestiction(args.Target, true);
        var subvertedComp = EnsureComp<CLFSubvertedSynthComponent>(args.Target);
        _adminLogManager.Add(LogType.Mind,
            LogImpact.Medium,
            $"{ToPrettyString(args.Target)}had a CLF synth subverter used on them");

        if (!_role.MindHasRole<CLFSubvertedSynthRoleComponent>(mindId))
        {
            _role.MindAddRole(mindId, "MindRoleCLFSubvertedSynth");
        }

        if (mind is { UserId: not null } && _player.TryGetSessionById(mind.UserId, out var session))
            _antag.SendBriefing(session, Loc.GetString("clf-subverted-synth-briefing"), Color.Red, subvertedComp.CLFSubversionSound);
    }
}
