using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.RoundEnd;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs;
using Content.Shared.NPC.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.AU14;

namespace Content.Server.AU14.Threats;

public sealed class KillAllApeRuleSystem : GameRuleSystem<KillAllApeRuleComponent>
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
		// Only run this logic when the KillAllApe rule is active
		if (!_gameTicker.IsGameRuleActive<KillAllApeRuleComponent>())
			return;

		// Only care about dead mobs
		if (ev.NewMobState != MobState.Dead)
			return;

		// Get the active rule entity and its component to read Percent
		var queryRule = EntityQueryEnumerator<KillAllApeRuleComponent, GameRuleComponent>();
		if (!queryRule.MoveNext(out var ruleEnt, out var ruleComp, out var gameRuleComp) || !GameTicker.IsGameRuleActive(ruleEnt, gameRuleComp))
			return;

		var requiredPercent = Math.Clamp(ruleComp.Percent, 1, 100);

		// Count total and dead Ape mobs
		var total = 0;
		var dead = 0;

		var query = _entityManager.EntityQueryEnumerator<MobStateComponent>();
		while (query.MoveNext(out var uid, out var mobState))
		{
			if (!_entityManager.HasComponent<ApeComponent>(uid))
				continue;

			total++;
			if (mobState.CurrentState == MobState.Dead)
				dead++;
		}

		if (total == 0)
			return; // nothing to count

		var percentDead = (int) ((double)dead / total * 100.0);

		if (percentDead >= requiredPercent)
		{
			if (_gameTicker.RunLevel != GameRunLevel.InRound)
				return;

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




