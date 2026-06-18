using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Xenonids.ZJump;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(CMUXenoZJumpSystem))]
public sealed partial class CMUXenoZJumpingComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid OriginMap;

    [DataField, AutoNetworkedField]
    public bool LeftOriginMap;
}
