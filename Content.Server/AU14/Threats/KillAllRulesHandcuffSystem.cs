using System.Linq;
using Content.Server.GameTicking;
using Content.Shared.AU14;
using Content.Shared.Cuffs;
using Content.Shared.Cuffs.Components;
using Content.Shared.NPC.Components;

namespace Content.Server.AU14.Threats;

/// <summary>
/// Shared system for handling handcuff events for both KillAllClf and KillAllGovfor rules.
/// Prevents duplicate subscription errors.
/// </summary>
public sealed class KillAllRulesHandcuffSystem : EntitySystem
{
    [Dependency] private readonly GameTicker _gameTicker = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CuffableComponent, TargetHandcuffedEvent>(OnTargetHandcuffed);
    }

    private void OnTargetHandcuffed(EntityUid uid, CuffableComponent component, ref TargetHandcuffedEvent args)
    {
        // Check if this entity has a faction
        if (!TryComp<NpcFactionMemberComponent>(uid, out var faction))
            return;

        var factionName = "";
        if (faction.Factions.Any(f => f.ToString().ToLowerInvariant() == "clf"))
            factionName = "clf";
        else if (faction.Factions.Any(f => f.ToString().ToLowerInvariant() == "govfor"))
            factionName = "govfor";
        else
            return;

        // Dispatch to the appropriate rule system
        if (factionName == "clf" && _gameTicker.IsGameRuleActive<KillAllClfRuleComponent>())
        {
            var sys = EntityManager.System<KillAllClfRuleSystem>();
            sys.OnHandcuffEvent(uid);
        }
        else if (factionName == "govfor" && _gameTicker.IsGameRuleActive<KillAllGovforRuleComponent>())
        {
            var sys = EntityManager.System<KillAllGovforRuleSystem>();
            sys.OnHandcuffEvent(uid);
        }
    }
}

