using Content.Server.Chat.Systems;
using Content.Server.Medical;
using Content.Server.Polymorph.Systems;
using Content.Shared._AU14.Abominations;
using Content.Shared._RMC14.Synth;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Coordinates;
using Content.Shared.Damage;
using Content.Shared.Drunk;
using Content.Shared.Humanoid;
using Content.Shared.Jittering;
using Content.Shared.Mobs;
using Content.Shared.Polymorph;
using Content.Shared.StatusEffect;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._AU14.Abominations;

/// <summary>
/// Abomination melee hits roll AbominationComponent.InfectionChance against
/// each humanoid hit. Once infected the victim ramps from light coughs and
/// drunkenness up to constant seizures and vomiting over CrescendoAfter
/// minutes. Any infected death polymorphs the body into a mimic and seeds
/// flesh kudzu at the corpse.
/// </summary>
public sealed class AbominationInfectionSystem : EntitySystem
{
    public static readonly EntProtoId FleshKudzuSource = "AU14AbominationFleshKudzuSource";
    public static readonly ProtoId<PolymorphPrototype> TurnIntoMimic = "AbominationAssimilationToMimic";
    public static readonly ProtoId<EmotePrototype> CoughEmote = "Cough";
    public static readonly ProtoId<EmotePrototype> ScreamEmote = "Scream";

    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedDrunkSystem _drunk = default!;
    [Dependency] private readonly SharedJitteringSystem _jitter = default!;
    [Dependency] private readonly PolymorphSystem _polymorph = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly VomitSystem _vomit = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<AbominationComponent, MeleeHitEvent>(OnAbominationMeleeHit);
        SubscribeLocalEvent<AbominationInfectionComponent, MobStateChangedEvent>(OnInfectedMobStateChanged);
    }

    private void OnAbominationMeleeHit(Entity<AbominationComponent> abomination, ref MeleeHitEvent args)
    {
        if (!args.IsHit || args.HitEntities.Count == 0)
            return;

        foreach (var hit in args.HitEntities)
        {
            if (!HasComp<HumanoidAppearanceComponent>(hit))
                continue;
            if (HasComp<AbominationComponent>(hit) || HasComp<AbominationInfectionComponent>(hit))
                continue;
            if (HasComp<SynthComponent>(hit))
                continue;
            if (!_random.Prob(abomination.Comp.InfectionChance))
                continue;

            ApplyInfection(hit);
        }
    }

    private void ApplyInfection(EntityUid target)
    {
        var now = _timing.CurTime;
        var infection = EnsureComp<AbominationInfectionComponent>(target);
        infection.InfectedAt = now;
        infection.NextTickAt = now + infection.TickInterval;
        infection.NextCoughAt = now + infection.CoughIntervalEarly;
        infection.NextJitterAt = now + infection.JitterIntervalEarly;
        if (infection.TickDamage.DamageDict.Count == 0)
        {
            infection.TickDamage = new DamageSpecifier();
            infection.TickDamage.DamageDict.Add("Toxin", 2);
        }
        Dirty(target, infection);
    }

    /// <summary>
    /// Once the victim has shown any symptoms, dying turns them into a mimic
    /// regardless of cause — the threat reclaims the body. Flesh kudzu is
    /// seeded at the corpse coords before polymorph swaps the entity.
    /// </summary>
    private void OnInfectedMobStateChanged(Entity<AbominationInfectionComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        // Capture coords before the polymorph deletes the body.
        var coords = _transform.GetMapCoordinates(ent.Owner);
        if (coords.MapId != default)
            Spawn(FleshKudzuSource, coords);

        if (!ent.Comp.HasShownSymptoms)
            return;

        _polymorph.PolymorphEntity(ent.Owner, TurnIntoMimic);
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<AbominationInfectionComponent>();
        while (query.MoveNext(out var uid, out var infection))
        {
            var severity = GetSeverity(infection, now);
            if (severity > 0 && !infection.HasShownSymptoms)
            {
                infection.HasShownSymptoms = true;
                Dirty(uid, infection);
            }

            if (severity >= 1f && !infection.HasCrescendoed)
            {
                infection.HasCrescendoed = true;
                _chat.TryEmoteWithChat(uid, ScreamEmote);
                Dirty(uid, infection);
            }

            // Severity-scaled toxin tick + drunk.
            if (now >= infection.NextTickAt)
            {
                infection.NextTickAt = now + infection.TickInterval;
                var scaled = infection.TickDamage * (0.4f + 1.6f * severity);
                _damageable.TryChangeDamage(uid, scaled, true);
                _statusEffects.TryAddStatusEffect<DrunkComponent>(uid, SharedDrunkSystem.DrunkKey, infection.DrunkPerTick, true);
            }

            // Coughing — interval shrinks as severity rises.
            if (now >= infection.NextCoughAt)
            {
                var coughInterval = Lerp(infection.CoughIntervalEarly, infection.CoughIntervalLate, severity);
                infection.NextCoughAt = now + coughInterval;
                _chat.TryEmoteWithChat(uid, CoughEmote);
            }

            // Jitter — interval shrinks aggressively as severity rises so it
            // becomes near-constant near crescendo.
            if (now >= infection.NextJitterAt)
            {
                var jitterInterval = Lerp(infection.JitterIntervalEarly, infection.JitterIntervalLate, severity);
                infection.NextJitterAt = now + jitterInterval;
                var burst = TimeSpan.FromSeconds(2 + 4 * severity);
                _jitter.DoJitter(uid, burst, refresh: true, amplitude: 6 + 16 * severity, frequency: 8 + 8 * severity);
            }

            // Vomiting only kicks in past the threshold and accelerates with severity.
            if (severity >= infection.VomitSeverityThreshold && now >= infection.NextVomitAt)
            {
                infection.NextVomitAt = now + infection.VomitInterval;
                _vomit.Vomit(uid);
            }
        }
    }

    private float GetSeverity(AbominationInfectionComponent infection, TimeSpan now)
    {
        var elapsed = (now - infection.InfectedAt).TotalSeconds;
        var total = Math.Max(1.0, infection.CrescendoAfter.TotalSeconds);
        return (float) Math.Clamp(elapsed / total, 0.0, 1.0);
    }

    private static TimeSpan Lerp(TimeSpan a, TimeSpan b, float t)
    {
        return TimeSpan.FromSeconds(a.TotalSeconds + (b.TotalSeconds - a.TotalSeconds) * t);
    }
}
