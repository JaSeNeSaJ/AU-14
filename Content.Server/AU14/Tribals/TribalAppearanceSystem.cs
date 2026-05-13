using Content.Shared.AU14;
using Content.Shared.Humanoid;

namespace Content.Server.AU14.Tribals;

/// <summary>
/// Forces every tribal humanoid to a grey / dark-cyan skin tone (Na'vi-ish)
/// after the random profile roll has run. Subscribes "after" the random
/// humanoid system so it overwrites the species default skin.
/// </summary>
public sealed class TribalAppearanceSystem : EntitySystem
{
    public static readonly Color TribalSkin = Color.FromHex("#4F7A82");

    [Dependency] private readonly SharedHumanoidAppearanceSystem _humanoid = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<TribalComponent, MapInitEvent>(OnMapInit, after: new[] { typeof(Content.Server.Humanoid.Systems.RandomHumanoidAppearanceSystem) });
    }

    private void OnMapInit(Entity<TribalComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp<HumanoidAppearanceComponent>(ent, out var humanoid))
            return;

        _humanoid.SetSkinColor(ent.Owner, TribalSkin, sync: false, verify: false, humanoid);
        Dirty(ent.Owner, humanoid);
    }
}
