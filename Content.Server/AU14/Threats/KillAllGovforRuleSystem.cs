using System.Linq;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Shared.AU14;
using Content.Shared.Cuffs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs;
using Content.Shared.NPC.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.Cuffs.Components;

namespace Content.Server.AU14.Threats;

public sealed class KillAllGovforRuleSystem : GameRuleSystem<KillAllGovforRuleComponent>
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly Round.AuRoundSystem _auRoundSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnMobStateChanged(MobStateChangedEvent ev)
    {
        // Only run this logic when the KillAllGovfor rule is active
        if (!_gameTicker.IsGameRuleActive<KillAllGovforRuleComponent>())
            return;

        // Only care about dead mobs
        if (ev.NewMobState != MobState.Dead)
            return;

        CheckVictoryCondition();
    }

    /// <summary>
    /// Called by KillAllRulesHandcuffSystem when a Govfor entity is handcuffed.
    /// </summary>
    public void OnHandcuffEvent(EntityUid uid)
    {

        CheckVictoryCondition();
    }

    private void CheckVictoryCondition()
    {
        // Get the active rule entity and its component to read Percent
        var queryRule = EntityQueryEnumerator<KillAllGovforRuleComponent, GameRuleComponent>();
        if (!queryRule.MoveNext(out var ruleEnt, out var ruleComp, out var gameRuleComp) || !GameTicker.IsGameRuleActive(ruleEnt, gameRuleComp))
            return;

        var requiredPercent = Math.Clamp(ruleComp.Percent, 1, 100);
        var countArrests = ruleComp.Arrest;

        // Count total and dead/arrested Govfor mobs
        var total = 0;
        var eliminated = 0;

        var query = _entityManager.EntityQueryEnumerator<MobStateComponent, NpcFactionMemberComponent>();
        while (query.MoveNext(out var uid, out var mobState, out var faction))
        {
            if (faction.Factions.Any(f => f.ToString().ToLowerInvariant() == "govfor"))
            {
                total++;

                // Count as eliminated if dead
                if (mobState.CurrentState == MobState.Dead)
                {
                    eliminated++;
                }
                // Or if arrested flag is set and they're cuffed
                else if (countArrests && TryComp<CuffableComponent>(uid, out var cuffable) && cuffable.CuffedHandCount > 0)
                {
                    eliminated++;
                }
            }
        }

        if (total == 0)
            return; // nothing to count

        var percentEliminated = (int) ((double)eliminated / total * 100.0);

        if (percentEliminated >= requiredPercent)
        {
            if (_gameTicker.RunLevel != GameRunLevel.InRound)
                return;

            var customMessage = ruleComp.WinMessage;
            if (!string.IsNullOrEmpty(customMessage))
            {
                _gameTicker.EndRound(customMessage);
            }
            else
            {
                var winMessage = _auRoundSystem._selectedthreat?.WinMessage;
                if (!string.IsNullOrEmpty(winMessage))
                {
                    _gameTicker.EndRound(winMessage);
                }
                else
                {
                    _gameTicker.EndRound("Threat victory: Required percentage of Govfor eliminated.");
                }
            }
        }
    }
}
