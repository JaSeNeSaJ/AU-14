using Content.Server.Ghost.Roles.Components;
using Content.Shared.AU14;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Roles;
using Content.Shared.Station;
using Robust.Shared.Prototypes;

namespace Content.Server.AU14.Tribals;

/// <summary>
/// Forces every tribal humanoid to:
///   - the "Tribal" (Na'vi) species name,
///   - a grey / dark-cyan skin tone (overrides the random profile roll),
///   - and re-equips the job starting gear at MapInit because the
///     stock GhostRoleApplySpecial ComponentStartup pass races with
///     humanoid init and the gear sometimes never lands.
/// Subscribes "after" the random humanoid system so it overwrites the
/// random profile's species / skin pick.
/// </summary>
public sealed class TribalAppearanceSystem : EntitySystem
{
    public static readonly Color TribalSkin = Color.FromHex("#4F7A82");
    public static readonly ProtoId<SpeciesPrototype> TribalSpecies = "Tribal";

    [Dependency] private readonly SharedHumanoidAppearanceSystem _humanoid = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedStationSpawningSystem _stationSpawning = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<TribalComponent, MapInitEvent>(OnMapInit, after: new[] { typeof(Content.Server.Humanoid.Systems.RandomHumanoidAppearanceSystem) });
    }

    private void OnMapInit(Entity<TribalComponent> ent, ref MapInitEvent args)
    {
        if (TryComp<HumanoidAppearanceComponent>(ent, out var humanoid))
        {
            _humanoid.SetSpecies(ent.Owner, TribalSpecies, sync: false, humanoid);
            _humanoid.SetSkinColor(ent.Owner, TribalSkin, sync: false, verify: false, humanoid);
            Dirty(ent.Owner, humanoid);
        }

        // Belt-and-braces equip — GhostRoleApplySpecial's ComponentStartup
        // pass races with humanoid init on this codebase and the standard
        // gear pipe never lands for tribal variants. We re-pull the job's
        // startingGear here, post-MapInit, so spawn-with-loadout behaves
        // identically to other third parties.
        if (TryComp<GhostRoleComponent>(ent, out var ghostRole) &&
            ghostRole.JobProto is { } jobProto &&
            _proto.TryIndex(jobProto, out JobPrototype? job) &&
            job.StartingGear is { } gear)
        {
            _stationSpawning.EquipStartingGear(ent.Owner, gear);
        }
    }
}
