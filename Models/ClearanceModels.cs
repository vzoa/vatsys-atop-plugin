using System;
using System.Collections.Generic;

namespace AtopPlugin.Models;

/// <summary>
/// Lightweight POCO representations of CPDLCPlugin's uplink message types,
/// used by the ATOP Clearance window without compile-time dependency on CPDLCPlugin.
/// </summary>
/// 
public class AtopUplinkTemplate
{
    public int Id { get; set; }
    public string Template { get; set; } = "";
    public AtopUplinkParameter[] Parameters { get; set; } = Array.Empty<AtopUplinkParameter>();
    public int ResponseType { get; set; } // maps to UplinkResponseType: 0=NoResponse, 1=WilcoUnable, 2=AffirmNeg, 3=Roger
}

public class AtopUplinkParameter
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = ""; // string representation of CPDLCPlugin.Configuration.ParameterType
}

public class AtopMessageGroup
{
    public string Name { get; set; } = "";
    public AtopMessageReference[] Messages { get; set; } = Array.Empty<AtopMessageReference>();
}

public class AtopMessageReference
{
    public int MessageId { get; set; }
    public Dictionary<string, string>? DefaultParameters { get; set; }
    public int? ResponseType { get; set; }
}

public class AtopDownlinkInfo
{
    public int MessageId { get; set; }
    public string Content { get; set; } = "";
    public DateTimeOffset Received { get; set; }
    public int? ResponseType { get; set; }
    public bool IsClosed { get; set; }
    public bool IsAcknowledged { get; set; }
    public int? MessageReference { get; set; }
}

public class AtopUplinkMessagesConfig
{
    public AtopUplinkTemplate[] MasterMessages { get; set; } = Array.Empty<AtopUplinkTemplate>();
    public AtopMessageReference[] PermanentMessages { get; set; } = Array.Empty<AtopMessageReference>();
    public AtopMessageGroup[] Groups { get; set; } = Array.Empty<AtopMessageGroup>();
}
