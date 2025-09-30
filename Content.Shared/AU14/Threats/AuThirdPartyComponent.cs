using Content.Shared.AU14;
using Robust.Shared.Prototypes;
using System.Collections.Generic;
using Content.Shared._RMC14.Requisitions;
using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared.AU14.util;
using Content.Shared.Roles;
using Robust.Shared.GameStates;

namespace Content.Shared.AU14.Threats;
[RegisterComponent]
public sealed partial class AuThirdPartyComponent : Component
{

    [DataField("blacklistedThreats")]
    public List<string> BlacklistedThreats { get; private set; } = new();

    [DataField("whitelistedThreats")]
    public List<string> WhitelistedThreats { get; private set; } = new();

    [DataField("enterbyshuttle")]
    public bool Enterbyshuttle { get; private set; } =  false;

    [DataField("spawnmarker")]
    public string markerID { get; private set; } =  "genericthirdparty";






}

