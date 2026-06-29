using System.Text.RegularExpressions;
using Content.Server.Speech.Components;

namespace Content.Server.Speech.EntitySystems;

public sealed partial class MothAccentSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MothAccentComponent, AccentGetEvent>(OnAccent);
    }

    private void OnAccent(EntityUid uid, MothAccentComponent component, AccentGetEvent args)
    {
        var message = args.Message;

        // buzzz
        message = LowerBuzzRegex().Replace(message, "zzz");
        // buZZZ
        message = UpperBuzzRegex().Replace(message, "ZZZ");

        args.Message = message;
    }

    [GeneratedRegex("z{1,3}")]
    private static partial Regex LowerBuzzRegex();

    [GeneratedRegex("Z{1,3}")]
    private static partial Regex UpperBuzzRegex();
}
