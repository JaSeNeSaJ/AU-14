using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Xenonids.JoinXeno;

[Serializable, NetSerializable]
public enum JoinXenoUIKey : byte
{
    Key
}

[Serializable, NetSerializable]
public enum LarvaPoolStatus : byte
{
    Ineligible,
    Waiting,
    Eligible,
}

[Serializable, NetSerializable]
public readonly record struct JoinXenoHiveEntry(
    NetEntity Hive,
    string HiveName,
    LarvaPoolStatus Status,
    int Position);

[Serializable, NetSerializable]
public sealed class JoinXenoBuiState(List<JoinXenoHiveEntry> entries) : BoundUserInterfaceState
{
    public readonly List<JoinXenoHiveEntry> Entries = entries;
}
