using Content.Server.Chat.Systems;
using Content.Server.Medical;
using Content.Shared._AU14.Abominations;
using Content.Shared._RMC14.Synth;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Coordinates;
using Content.Shared.Damage;
using Content.Shared.Drunk;
using Content.Shared.Humanoid;
using Content.Shared.Jittering;
using Content.Shared.Mobs;
using Content.Shared.StatusEffect;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._AU14.Abominations;

/// <summary>
/// Mimic melee hits have a chance to infect humanoid targets with
/// AbominationInfectionComponent. Infected humanoids cough, drunken, and take
/// toxin damage; after 8 minutes they shake and vomit. Dying while infected
/// seeds flesh kudzu at the corpse.
/// </summary>
public sealed class AbominationInfectionSystem : EntitySystem
{
    public static readonly EntProtoId FleshKudzuSource = "AU14AbominationFleshKudzuSource";
    public static readonly ProtoId<EmotePrototype> CoughEmote = "Cough";

    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedDrunkSystem _drunk = default!;
    [Dependency] private readonly SharedJitteringSystem _jitter = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
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
        infection.NextCoughAt = now + infection.CoughInterval;
        // Default tick damage: light toxin (data hook overrides via prototype).
        if (infection.TickDamage.DamageDict.Count == 0)
        {
            infection.TickDamage = new DamageSpecifier();
            infection.TickDamage.DamageDict.Add("Toxin", 2);
        }
        Dirty(target, infection);
    }

    private void OnInfectedMobStateChanged(Entity<AbominationInfectionComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        // Burst out into kudzu where they fell.
        Spawn(FleshKudzuSource, ent.Owner.ToCoordinates());
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<AbominationInfectionComponent>();
        while (query.MoveNext(out var uid, out var infection))
        {
            if (!infection.HasCrescendoed && now >= infection.InfectedAt + infection.CrescendoAfter)
            {
                infection.HasCrescendoed = true;
                infection.NextVomitAt = now;
                Dirty(uid, infection);
            }

            if (now >= infection.NextTickAt)
            {
                infection.NextTickAt = now + infection.TickInterval;
                Tick((uid, infection));
            }

            if (now >= infection.NextCoughAt)
            {
                infection.NextCoughAt = now + infection.CoughInterval;
                _chat.TryEmoteWithChat(uid, CoughEmote);
            }

            if (infection.HasCrescendoed && now >= infection.NextVomitAt)
            {
                infection.NextVomitAt = now + infection.VomitInterval;
                _vomit.Vomit(uid);
                _jitter.DoJitter(uid, TimeSpan.FromSeconds(6), refresh: true, amplitude: 12, frequency: 14);
            }
        }
    }

    private void Tick(Entity<AbominationInfectionComponent> ent)
    {
        _damageable.TryChangeDamage(ent.Owner, ent.Comp.TickDamage, true);
        _statusEffects.TryAddStatusEffect<DrunkComponent>(ent.Owner, SharedDrunkSystem.DrunkKey, ent.Comp.DrunkPerTick, true);
    }
}
