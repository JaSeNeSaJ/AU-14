using Robust.Shared.ComponentTrees;
using Robust.Shared.GameStates;
namespace Content.Shared.AU14;

/// <summary>
/// Marks an entity as a cultist, allowing them to access the Hivemind channel.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CultistComponent : Component
{
}


