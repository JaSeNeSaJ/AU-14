using Content.Shared._RMC14.Dropship;
using Robust.Shared.GameStates;

namespace Content.Server.AU14.Round;


[RegisterComponent,NetworkedComponent]
public sealed partial class WhitelistedShuttleComponent: Component
{

    [DataField("faction", required: true)]
    public string? Faction  { get; set; } = default;

   public DropshipDestinationComponent.DestinationType ShuttleType = DropshipDestinationComponent.DestinationType.Dropship;

}
