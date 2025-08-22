namespace Content.Server.AU14.Round;


[RegisterComponent]
public sealed partial class WhitelistedShuttleComponent: Component
{

    [DataField("faction", required: true)]
    public string? Faction  { get; set; } = default;

}
