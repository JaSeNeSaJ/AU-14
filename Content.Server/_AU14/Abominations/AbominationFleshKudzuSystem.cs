using Content.Server.Chat.Systems;
using Content.Shared._AU14.Abominations;
using Content.Shared.Damage;
using Content.Shared.Interaction.Events;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._AU14.Abominations;

/// <summary>
/// Periodic heal-tick for abominations standing on a flesh kudzu tile, plus
/// occasional sob/cry/scream emotes. Damage tick for non-abominations is
/// handled by upstream DamageContacts on the kudzu prototype. Abomination
/// melee attacks on tendons are rejected here so the threat can't trash its
/// own coverage.
/// </summary>
public sealed class AbominationFleshKudzuSystem : EntitySystem
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<AbominationComponent, AttackAttemptEvent>(OnAbominationAttackAttempt);
    }

    /// <summary>
    /// Block abominations from melee-attacking flesh kudzu — they kept
    /// destroying their own coverage in playtest.
    /// </summary>
    private void OnAbominationAttackAttempt(Entity<AbominationComponent> ent, ref AttackAttemptEvent args)
    {
        if (args.Target is { } target && HasComp<AbominationFleshKudzuComponent>(target))
            args.Cancel();
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<AbominationFleshKudzuComponent, PhysicsComponent>();
        while (query.MoveNext(out var uid, out var kudzu, out var physics))
        {
            if (kudzu.NextHealAt <= now)
            {
                kudzu.NextHealAt = now + kudzu.HealInterval;
                HealContacts((uid, kudzu, physics));
            }

            if (kudzu.NextEmoteAt <= now)
            {
                kudzu.NextEmoteAt = now + TimeSpan.FromSeconds(_random.NextDouble(
                    kudzu.EmoteIntervalMin.TotalSeconds,
                    kudzu.EmoteIntervalMax.TotalSeconds));

                if (kudzu.Emotes.Count > 0)
                {
                    // forceEmote + ignoreActionBlocker so the kudzu (which has no
                    // Speech/Vocal) can still emit the chat + sound; without
                    // these the emote silently fails AllowedToUseEmote.
                    // TODO: filter so abominations don't see/hear it themselves.
                    _chat.TryEmoteWithChat(uid, _random.Pick(kudzu.Emotes), ignoreActionBlocker: true, forceEmote: true);
                }
            }
        }
    }

    private void HealContacts(Entity<AbominationFleshKudzuComponent, PhysicsComponent> ent)
    {
        foreach (var contact in _physics.GetContactingEntities(ent.Owner, ent.Comp2))
        {
            if (!HasComp<AbominationComponent>(contact))
                continue;

            _damageable.TryChangeDamage(contact, ent.Comp1.Heal, true);
        }
    }
}
