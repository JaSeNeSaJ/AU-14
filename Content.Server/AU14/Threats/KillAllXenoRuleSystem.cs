
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.RoundEnd;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs;
using Content.Shared.Cuffs.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.AU14;
using Content.Shared._RMC14.Xenonids;

namespace Content.Server.AU14.Threats;

public sealed class KillAllXenoRuleSystem : GameRuleSystem<KillAllXenoRuleComponent>
{
	[Dependency] private readonly IEntityManager _entityManager = default!;
	[Dependency] private readonly GameTicker _gameTicker = default!;
	[Dependency] private readonly Round.AuRoundSystem _auRoundSystem = default!;
	// RoundEndSystem is not needed directly here; we call GameTicker.EndRound which
	// will ensure round end behavior is handled. Keep dependency list minimal.

	public override void Initialize()
	{
		base.Initialize();
		SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
	}

	private void OnMobStateChanged(MobStateChangedEvent ev)
	{
		// Only run this logic when the KillAllXeno rule is active
		if (!_gameTicker.IsGameRuleActive<KillAllXenoRuleComponent>())
			return;

		// Only care about dead mobs
		if (ev.NewMobState != MobState.Dead)
			return;

		// Get the active rule entity and its component to read Percent
		var queryRule = EntityQueryEnumerator<KillAllXenoRuleComponent, GameRuleComponent>();
		if (!queryRule.MoveNext(out var ruleEnt, out var ruleComp, out var gameRuleComp) || !GameTicker.IsGameRuleActive(ruleEnt, gameRuleComp))
			return;

		var requiredPercentXeno = Math.Clamp(ruleComp.PercentXeno, 1, 100);
		var requiredPercentCultist = Math.Clamp(ruleComp.PercentCultist, 1, 100);

		// Count total and dead Xeno and Cultist mobs separately
		var totalXeno = 0;
		var deadXeno = 0;
		var totalCultist = 0;
		var deadCultist = 0;

		var query = _entityManager.EntityQueryEnumerator<MobStateComponent>();
		while (query.MoveNext(out var uid, out var mobState))
		{
			// Detect xenos by presence of XenoComponent, and cultists by CultistComponent.
			var isXeno = _entityManager.HasComponent<XenoComponent>(uid);
			var isCultist = _entityManager.HasComponent<CultistComponent>(uid);

			if (isXeno)
			{
				totalXeno++;
				if (mobState.CurrentState == MobState.Dead)
					deadXeno++;
			}

			if (isCultist)
			{
				totalCultist++;
				// Count cultists as "dead" if they are actually dead OR restrained (cuffed).
				if (mobState.CurrentState == MobState.Dead)
				{
					deadCultist++;
				}
				else if (_entityManager.TryGetComponent(uid, out CuffableComponent? cuff) && cuff.CuffedHandCount > 0)
				{
					// Restrained cultist counts as killed for the purposes of this rule.
					deadCultist++;
				}
			}
		}

		// If nothing to count at all, bail out
		if (totalXeno == 0 && totalCultist == 0)
			return;

		// Calculate percent dead for each category. If a category has zero total we treat it as satisfied.
		var percentDeadXeno = totalXeno == 0 ? 100 : (int)((double)deadXeno / totalXeno * 100.0);
		var percentDeadCultist = totalCultist == 0 ? 100 : (int)((double)deadCultist / totalCultist * 100.0);

		var xenoSatisfied = percentDeadXeno >= requiredPercentXeno;
		var cultistSatisfied = percentDeadCultist >= requiredPercentCultist;

		if (xenoSatisfied && cultistSatisfied)
		{
			if (_gameTicker.RunLevel != GameRunLevel.InRound)
				return;

			// Prefer any configured win message, otherwise use a default.
			var winMessage = _auRoundSystem._selectedthreat?.WinMessage;
			if (!string.IsNullOrEmpty(winMessage))
			{
				_gameTicker.EndRound(winMessage);
			}
			else
			{
				_gameTicker.EndRound("The Threat has been Eliminated");
			}
		}
	}
}









