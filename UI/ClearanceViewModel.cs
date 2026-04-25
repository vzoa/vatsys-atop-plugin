using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using AtopPlugin.Conflict;
using AtopPlugin.Helpers;
using AtopPlugin.Models;
using vatsys;
using static vatsys.FDP2.FDR.ExtractedRoute;

namespace AtopPlugin.UI;

/// <summary>
/// ViewModel for the ATOP Clearance window.
/// Matches ATOP spec Figures 9-1: inline editable template fields, EOS markers,
/// construction area with INS/DEL, UNBL command, shortcut bar.
/// </summary>
public class ClearanceViewModel : INotifyPropertyChanged
{
    private const string NoProceduralConflictsFoundMessage = "No Procedural Conflicts Found";
    private const string ConflictWithCountsMessageTemplate = "Conflict with {0} number of aircraft and {1} number of airspaces";

    public event PropertyChangedEventHandler? PropertyChanged;

    // -------------------------------------------------------------------------
    // Built-in MOPS message templates (NASP-4508K) — used when CPDLC plugin is
    // not loaded.  ResponseType: 0=NoResponse 1=WilcoUnable 2=AffirmNeg 3=Roger
    // -------------------------------------------------------------------------
    private static AtopUplinkTemplate BT(int id, string tpl, int rt = 0, params string[] ps) =>
        new() { Id = id, Template = tpl, ResponseType = rt,
                Parameters = ps.Select(n => new AtopUplinkParameter { Name = n, Type = "FreeText" }).ToArray() };

    private static readonly Dictionary<int, AtopUplinkTemplate> _builtIn = new()
    {
        [0]   = BT(0,   "UNABLE"),
        [1]   = BT(1,   "STANDBY"),
        [2]   = BT(2,   "REQUEST DEFERRED"),
        [3]   = BT(3,   "ROGER"),
        [4]   = BT(4,   "AFFIRM"),
        [5]   = BT(5,   "NEGATIVE"),
        [6]   = BT(6,   "EXPECT [lev]", 3, "lev"),
        [7]   = BT(7,   "EXPECT CLIMB AT [time]", 3, "time"),
        [8]   = BT(8,   "EXPECT CLIMB AT [pos]", 3, "pos"),
        [9]   = BT(9,   "EXPECT DESCENT AT [time]", 3, "time"),
        [10]  = BT(10,  "EXPECT DESCENT AT [pos]", 3, "pos"),
        [13]  = BT(13,  "AT [time] EXPECT CLIMB TO [lev]", 3, "time", "lev"),
        [14]  = BT(14,  "AT [pos] EXPECT CLIMB TO [lev]", 3, "pos", "lev"),
        [15]  = BT(15,  "AT [time] EXPECT DESCENT TO [lev]", 3, "time", "lev"),
        [16]  = BT(16,  "AT [pos] EXPECT DESCENT TO [lev]", 3, "pos", "lev"),
        [19]  = BT(19,  "MAINTAIN [lev]", 1, "lev"),
        [20]  = BT(20,  "CLIMB TO [lev]", 1, "lev"),
        [21]  = BT(21,  "AT [time] CLIMB TO [lev]", 1, "time", "lev"),
        [22]  = BT(22,  "AT [pos] CLIMB TO [lev]", 1, "pos", "lev"),
        [23]  = BT(23,  "DESCENT TO [lev]", 1, "lev"),
        [24]  = BT(24,  "AT [time] DESCEND TO [lev]", 1, "time", "lev"),
        [25]  = BT(25,  "AT [pos] DESCEND TO [lev]", 1, "pos", "lev"),
        [26]  = BT(26,  "CLIMB TO REACH [lev] BY [time]", 1, "lev", "time"),
        [27]  = BT(27,  "CLIMB TO REACH [lev] BY [pos]", 1, "lev", "pos"),
        [28]  = BT(28,  "DESCEND TO REACH [lev] BY [time]", 1, "lev", "time"),
        [29]  = BT(29,  "DESCEND TO REACH [lev] BY [pos]", 1, "lev", "pos"),
        [30]  = BT(30,  "MAINTAIN BLOCK [lev] TO [lev2]", 1, "lev", "lev2"),
        [31]  = BT(31,  "CLIMB TO AND MAINTAIN BLOCK [lev] TO [lev2]", 1, "lev", "lev2"),
        [32]  = BT(32,  "DESCEND TO AND MAINTAIN BLOCK [lev] TO [lev2]", 1, "lev", "lev2"),
        [33]  = BT(33,  "CRUISE [lev]", 1, "lev"),
        [34]  = BT(34,  "CRUISE CLIMB TO [lev]", 1, "lev"),
        [36]  = BT(36,  "EXPEDITE CLIMB TO [lev]", 1, "lev"),
        [37]  = BT(37,  "EXPEDITE DESCENT TO [lev]", 1, "lev"),
        [38]  = BT(38,  "IMMEDIATELY CLIMB TO [lev]", 1, "lev"),
        [39]  = BT(39,  "IMMEDIATELY DESCEND TO [lev]", 1, "lev"),
        [40]  = BT(40,  "IMMEDIATELY STOP CLIMB AT [lev]", 1, "lev"),
        [41]  = BT(41,  "IMMEDIATELY STOP DESCENT AT [lev]", 1, "lev"),
        [42]  = BT(42,  "EXPECT TO CROSS [pos] AT [lev]", 3, "pos", "lev"),
        [43]  = BT(43,  "EXPECT TO CROSS [pos] AT OR ABOVE [lev]", 3, "pos", "lev"),
        [44]  = BT(44,  "EXPECT TO CROSS [pos] AT OR BELOW [lev]", 3, "pos", "lev"),
        [45]  = BT(45,  "EXPECT TO CROSS [pos] AT AND MAINTAIN [lev]", 3, "pos", "lev"),
        [46]  = BT(46,  "CROSS [pos] AT [lev]", 1, "pos", "lev"),
        [47]  = BT(47,  "CROSS [pos] AT OR ABOVE [lev]", 1, "pos", "lev"),
        [48]  = BT(48,  "CROSS [pos] AT OR BELOW [lev]", 1, "pos", "lev"),
        [49]  = BT(49,  "CROSS [pos] AT AND MAINTAIN [lev]", 1, "pos", "lev"),
        [50]  = BT(50,  "CROSS [pos] BETWEEN [lev] AND [lev2]", 1, "pos", "lev", "lev2"),
        [51]  = BT(51,  "CROSS [pos] AT [time]", 1, "pos", "time"),
        [52]  = BT(52,  "CROSS [pos] AT OR BEFORE [time]", 1, "pos", "time"),
        [53]  = BT(53,  "CROSS [pos] AT OR AFTER [time]", 1, "pos", "time"),
        [54]  = BT(54,  "CROSS [pos] BETWEEN [time] AND [time2]", 1, "pos", "time", "time2"),
        [55]  = BT(55,  "CROSS [pos] AT [spd]", 1, "pos", "spd"),
        [56]  = BT(56,  "CROSS [pos] AT OR LESS THAN [spd]", 1, "pos", "spd"),
        [57]  = BT(57,  "CROSS [pos] AT OR GREATER THAN [spd]", 1, "pos", "spd"),
        [58]  = BT(58,  "CROSS [pos] AT [time] AT [lev]", 1, "pos", "time", "lev"),
        [59]  = BT(59,  "CROSS [pos] AT OR BEFORE [time] AT [lev]", 1, "pos", "time", "lev"),
        [60]  = BT(60,  "CROSS [pos] AT OR AFTER [time] AND [lev]", 1, "pos", "time", "lev"),
        [61]  = BT(61,  "CROSS [pos] AT AND MAINTAIN [lev] AT [spd]", 1, "pos", "lev", "spd"),
        [64]  = BT(64,  "OFFSET [doff] [dir] OF ROUTE", 1, "doff", "dir"),
        [65]  = BT(65,  "AT [pos] OFFSET [doff] [dir] OF ROUTE", 1, "pos", "doff", "dir"),
        [66]  = BT(66,  "AT [time] OFFSET [doff] [dir] OF ROUTE", 1, "time", "doff", "dir"),
        [67]  = BT(67,  "PROCEED BACK ON ROUTE", 1),
        [68]  = BT(68,  "REJOIN ROUTE BY [pos]", 1, "pos"),
        [69]  = BT(69,  "REJOIN ROUTE BY [time]", 1, "time"),
        [70]  = BT(70,  "EXPECT BACK ON ROUTE BY [pos]", 3, "pos"),
        [71]  = BT(71,  "EXPECT BACK ON ROUTE BY [time]", 3, "time"),
        [73]  = BT(73,  "AT [time] CLEARED [rc] ALT [lev] FREQ [freq] SSR [code]", 1, "time", "rc", "lev", "freq", "code"),
        [74]  = BT(74,  "PROCEED DIRECT TO [pos]", 1, "pos"),
        [76]  = BT(76,  "AT [time] PROCEED DIRECT TO [pos]", 1, "time", "pos"),
        [77]  = BT(77,  "AT [pos] PROCEED DIRECT TO [pos2]", 1, "pos", "pos2"),
        [79]  = BT(79,  "CLEARED TO [pos] VIA [rc]", 1, "pos", "rc"),
        [80]  = BT(80,  "CLEARED [rc]", 1, "rc"),
        [82]  = BT(82,  "CLEARED TO DEVIATE UP TO [doff] [dir] OF ROUTE", 1, "doff", "dir"),
        [83]  = BT(83,  "AT [pos] CLEARED [rc]", 1, "pos", "rc"),
        [85]  = BT(85,  "EXPECT [rc]", 3, "rc"),
        [86]  = BT(86,  "AT [pos] EXPECT [rc]", 3, "pos", "rc"),
        [87]  = BT(87,  "EXPECT DIRECT TO [pos]", 3, "pos"),
        [88]  = BT(88,  "AT [pos] EXPECT DIRECT TO [pos2]", 3, "pos", "pos2"),
        [89]  = BT(89,  "AT [time] EXPECT DIRECT TO [pos]", 3, "time", "pos"),
        [90]  = BT(90,  "AT [lev] EXPECT DIRECT TO [pos]", 3, "lev", "pos"),
        [93]  = BT(93,  "EXPECT FURTHER CLEARANCE AT [time]", 3, "time"),
        [98]  = BT(98,  "IMMEDIATELY TURN [dir] HEADING [deg]", 1, "dir", "deg"),
        [99]  = BT(99,  "EXPECT [proc name]", 3, "proc name"),
        [100] = BT(100, "AT [time] EXPECT [spd]", 3, "time", "spd"),
        [101] = BT(101, "AT [pos] EXPECT [spd]", 3, "pos", "spd"),
        [102] = BT(102, "AT [lev] EXPECT [spd]", 3, "lev", "spd"),
        [103] = BT(103, "AT [time] EXPECT [spd] TO [spd2]", 3, "time", "spd", "spd2"),
        [104] = BT(104, "AT [pos] EXPECT [spd] TO [spd2]", 3, "pos", "spd", "spd2"),
        [105] = BT(105, "AT [lev] EXPECT [spd] TO [spd2]", 3, "lev", "spd", "spd2"),
        [106] = BT(106, "MAINTAIN [spd]", 1, "spd"),
        [108] = BT(108, "MAINTAIN [spd] OR GREATER", 1, "spd"),
        [109] = BT(109, "MAINTAIN [spd] OR LESS", 1, "spd"),
        [111] = BT(111, "INCREASE SPEED TO [spd]", 1, "spd"),
        [112] = BT(112, "INCREASE SPEED TO [spd] OR GREATER", 1, "spd"),
        [113] = BT(113, "REDUCE SPEED TO [spd]", 1, "spd"),
        [114] = BT(114, "REDUCE SPEED TO [spd] OR LESS", 1, "spd"),
        [115] = BT(115, "DO NOT EXCEED [spd]", 1, "spd"),
        [117] = BT(117, "CONTACT [unit name] [freq]", 1, "unit name", "freq"),
        [118] = BT(118, "AT [pos] CONTACT [unit name] [freq]", 1, "pos", "unit name", "freq"),
        [119] = BT(119, "AT [time] CONTACT [unit name] [freq]", 1, "time", "unit name", "freq"),
        [120] = BT(120, "MONITOR [unit name] [freq]", 1, "unit name", "freq"),
        [121] = BT(121, "AT [pos] MONITOR [unit name] [freq]", 1, "pos", "unit name", "freq"),
        [122] = BT(122, "AT [time] MONITOR [unit name] [freq]", 1, "time", "unit name", "freq"),
        [123] = BT(123, "SQUAWK [code]", 1, "code"),
        [124] = BT(124, "STOP SQUAWK", 1),
        [125] = BT(125, "SQUAWK ALTITUDE", 1),
        [126] = BT(126, "STOP ALTITUDE SQUAWK", 1),
        [127] = BT(127, "REPORT BACK ON ROUTE", 1),
        [128] = BT(128, "REPORT LEAVING [lev]", 3, "lev"),
        [129] = BT(129, "REPORT LEVEL [lev]", 3, "lev"),
        [130] = BT(130, "REPORT PASSING [pos]", 3, "pos"),
        [131] = BT(131, "REPORT REMAINING FUEL AND SOULS ON BOARD"),
        [132] = BT(132, "CONFIRM POSITION"),
        [133] = BT(133, "CONFIRM ALTITUDE"),
        [134] = BT(134, "CONFIRM SPEED"),
        [135] = BT(135, "CONFIRM ASSIGNED ALTITUDE"),
        [136] = BT(136, "CONFIRM ASSIGNED SPEED"),
        [137] = BT(137, "CONFIRM ASSIGNED ROUTE"),
        [138] = BT(138, "CONFIRM TIME OVER REPORTED WAYPOINT"),
        [139] = BT(139, "CONFIRM REPORTED WAYPOINT"),
        [140] = BT(140, "CONFIRM NEXT WAYPOINT"),
        [141] = BT(141, "CONFIRM NEXT WAYPOINT ETA"),
        [142] = BT(142, "CONFIRM ENSUING WAYPOINT"),
        [143] = BT(143, "CONFIRM REQUEST"),
        [144] = BT(144, "CONFIRM SQUAWK"),
        [145] = BT(145, "CONFIRM HEADING"),
        [147] = BT(147, "REQUEST POSITION REPORT"),
        [148] = BT(148, "WHEN CAN YOU ACCEPT [lev]", 0, "lev"),
        [149] = BT(149, "CAN YOU ACCEPT [lev] AT [pos]", 2, "lev", "pos"),
        [150] = BT(150, "CAN YOU ACCEPT [lev] AT [time]", 2, "lev", "time"),
        [151] = BT(151, "WHEN CAN YOU ACCEPT [spd]", 0, "spd"),
        [152] = BT(152, "WHEN CAN YOU ACCEPT [dir] [doff] OFFSET", 0, "dir", "doff"),
        [153] = BT(153, "ALTIMETER [altim]", 3, "altim"),
        [154] = BT(154, "RADAR SERVICE TERMINATED"),
        [155] = BT(155, "RADAR CONTACT [pos]", 0, "pos"),
        [156] = BT(156, "RADAR CONTACT LOST"),
        [157] = BT(157, "CHECK STUCK MICROPHONE ON [freq]", 0, "freq"),
        [158] = BT(158, "ATIS [atis]", 3, "atis"),
        [160] = BT(160, "NEXT DATA AUTHORITY [icao]", 0, "icao"),
        [164] = BT(164, "WHEN READY"),
        [165] = BT(165, "THEN"),
        [166] = BT(166, "DUE TO TRAFFIC"),
        [167] = BT(167, "DUE TO AIRSPACE RESTRICTION"),
        [168] = BT(168, "DISREGARD"),
        [169] = BT(169, "[freetext]", 3, "freetext"),
        [170] = BT(170, "(EMERGENCY) [freetext]", 0, "freetext"),
        [171] = BT(171, "CLIMB AT [vert] MINIMUM", 1, "vert"),
        [172] = BT(172, "CLIMB AT [vert] MAXIMUM", 1, "vert"),
        [173] = BT(173, "DESCEND AT [vert] MINIMUM", 1, "vert"),
        [174] = BT(174, "DESCEND AT [vert] MAXIMUM", 1, "vert"),
        [175] = BT(175, "REPORT REACHING [lev]", 0, "lev"),
        [176] = BT(176, "MAINTAIN OWN SEPARATION AND VMC", 1),
        [177] = BT(177, "AT PILOTS DISCRETION"),
        [179] = BT(179, "SQUAWK IDENT", 1),
        [180] = BT(180, "REPORT REACHING BLOCK [lev] TO [lev2]", 0, "lev", "lev2"),
        [181] = BT(181, "REPORT DISTANCE [to/from] [pos]", 0, "to/from", "pos"),
        [182] = BT(182, "CONFIRM ATIS CODE"),
        [192] = BT(192, "NO REPORTED IFR TRAFFIC"),
        [197] = BT(197, "REPORT RADIAL AND DISTANCE FROM [pos]", 0, "pos"),
    };

    // -------------------------------------------------------------------------
    // MOPS-driven menu structure per NASP-4508K pages 282-288
    // Key: category, Value: (SubGroup name or null for direct items, message IDs[])[]
    // -------------------------------------------------------------------------
    private static readonly Dictionary<string, (string? SubGroup, int[] MessageIds)[]> MopsMenuStructure = new()
    {
        ["Urgent"] = new[]
        {
            ((string?)null, new[] { 5, 38, 40, 39, 41, 98, 132, 133, 131, 170 })
        },
        ["Rpt"] = new[]
        {
            ("Report",       new[] { 130, 181, 197 }),
            ("Confirm",      new[] { 132, 133, 134, 135, 136, 137, 138, 139, 140, 141, 142, 143, 144, 145, 182 }),
            ((string?)null,  new[] { 128, 129, 175, 180, 147, 131 })
        },
        ["Negot"] = new[]
        {
            ((string?)null, new[] { 148, 151, 152, 149, 150 })
        },
        ["Rspn"] = new[]
        {
            ((string?)null, new[] { 0, 1, 2, 3, 4, 5, 169 })
        },
        ["Misc"] = new[]
        {
            ("Free Text",    new[] { 169, 170 }),
            ("Radar",        new[] { 155, 156, 154 }),
            ((string?)null,  new[] { 164, 165, 166, 167, 168, 176, 177, 192, 153, 157, 158 })
        },
        ["Vert"] = new[]
        {
            ("Climb",        new[] { 20, 31, 21, 22, 26, 27, 36, 38, 40, 171, 172 }),
            ("Descend",      new[] { 23, 32, 24, 25, 28, 29, 37, 39, 41, 173, 174 }),
            ("Expect",       new[] { 6, 7, 8, 9, 10, 13, 14, 15, 16 }),
            ("Cruise",       new[] { 33, 34 }),
            ((string?)null,  new[] { 19, 30 })
        },
        ["Route"] = new[]
        {
            ("ATC Clrc",     new[] { 73, 79, 80, 83, 74, 76, 77 }),
            ("Lateral",      new[] { 64, 65, 66, 67, 68, 69, 70, 71, 82, 127 }),
            ("Expect",       new[] { 85, 86, 87, 88, 89, 90, 93, 99 })
        },
        ["Speed"] = new[]
        {
            ("Maintain",     new[] { 106, 108, 109 }),
            ("Increase",     new[] { 111, 112 }),
            ("Reduce",       new[] { 113, 114 }),
            ("Expect",       new[] { 100, 101, 102, 103, 104, 105 }),
            ((string?)null,  new[] { 115 })
        },
        ["X-ing"] = new[]
        {
            ("Level",        new[] { 42, 43, 44, 45, 46, 47, 48, 49, 50 }),
            ("Time",         new[] { 51, 52, 53, 54, 149, 150, 151, 152 }),
            ("Speed",        new[] { 55, 56, 57 }),
            ("Combined",     new[] { 58, 59, 60, 61 })
        },
        ["Comm"] = new[]
        {
            ((string?)null, new[] { 117, 118, 119, 120, 121, 122, 123, 124, 125, 126, 179, 160 })
        }
    };

    public static readonly string[] CategoryNames =
    {
        "Urgent", "Rpt", "Negot", "Rspn", "Misc",
        "Vert", "Route", "Speed", "X-ing", "Comm", "Pre-Fmt"
    };

    // -------------------------------------------------------------------------
    // State
    // -------------------------------------------------------------------------
    private string _callsign = "";
    private string _route = "";
    private string _selectedCategory = "Vert";
    private string? _selectedSubCategory;
    private readonly Dictionary<int, AtopUplinkTemplate> _masterLookup = new();
    private AtopUplinkMessagesConfig? _config;

    // Construction area: list of message lines the controller is composing
    private readonly List<ConstructionLine> _constructionLines = new();
    private int _selectedConstructionIndex = -1;

    // Template area: which template is selected (arrow indicator)
    private int _selectedTemplateIndex = -1;

    // Downlinks awaiting response
    private readonly List<AtopDownlinkInfo> _openDownlinks = new();
    private int? _replyToDownlinkId;

    // Response / feedback area
    private string _responseText = "";
    private bool _isProbed;
    private bool _conflictDetected;
    private bool _overrideActive;
    private bool _isSent;

    /// <summary>Max construction elements per spec: 5.</summary>
    public const int MaxConstructionElements = 5;

    // -------------------------------------------------------------------------
    // Properties
    // -------------------------------------------------------------------------
    public string Callsign
    {
        get => _callsign;
        set { _callsign = value; OnPropertyChanged(); }
    }

    public string Route
    {
        get => _route;
        set { _route = value; OnPropertyChanged(); }
    }

    public string SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (_selectedCategory == value) return;
            _selectedCategory = value;
            _selectedTemplateIndex = -1;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SubCategories));
            SelectedSubCategory = SubCategories.FirstOrDefault();
        }
    }

    public string? SelectedSubCategory
    {
        get => _selectedSubCategory;
        set
        {
            if (_selectedSubCategory == value) return;
            _selectedSubCategory = value;
            _selectedTemplateIndex = -1;
            OnPropertyChanged();
            OnPropertyChanged(nameof(VisibleTemplates));
        }
    }

    public int SelectedTemplateIndex
    {
        get => _selectedTemplateIndex;
        set
        {
            _selectedTemplateIndex = value;
            OnPropertyChanged();
        }
    }

    public IReadOnlyList<string> SubCategories
    {
        get
        {
            if (_selectedCategory == "Pre-Fmt")
                return new[] { "Permanent" };

            if (MopsMenuStructure.TryGetValue(_selectedCategory, out var groups))
                return groups.Where(g => g.SubGroup != null).Select(g => g.SubGroup!).ToArray();

            return Array.Empty<string>();
        }
    }

    public IReadOnlyList<TemplateDisplayItem> VisibleTemplates
    {
        get
        {
            if (_selectedCategory == "Pre-Fmt")
            {
                if (_config == null) return Array.Empty<TemplateDisplayItem>();
                var permanentItems = new List<TemplateDisplayItem>();
                foreach (var r in _config.PermanentMessages ?? Array.Empty<AtopMessageReference>())
                {
                    var master = ResolveTemplate(r.MessageId);
                    if (master != null)
                        permanentItems.Add(BuildDisplayItem(master, r));
                }
                return permanentItems;
            }

            if (_selectedSubCategory != null && MopsMenuStructure.TryGetValue(_selectedCategory, out var groups))
            {
                var group = groups.FirstOrDefault(g => g.SubGroup == _selectedSubCategory);
                if (group != default)
                {
                    var items = new List<TemplateDisplayItem>();
                    foreach (var id in group.MessageIds)
                    {
                        var master = ResolveTemplate(id);
                        if (master != null)
                            items.Add(BuildDisplayItem(master, new AtopMessageReference { MessageId = id }));
                    }
                    return items;
                }
            }

            return Array.Empty<TemplateDisplayItem>();
        }
    }

    private AtopUplinkTemplate? ResolveTemplate(int id) =>
        _masterLookup.TryGetValue(id, out var t) ? t :
        _builtIn.TryGetValue(id, out var b) ? b : null;

    private TemplateDisplayItem BuildDisplayItem(AtopUplinkTemplate master, AtopMessageReference reference) =>
        new()
        {
            MessageId = master.Id,
            Template = master,
            Reference = reference,
            Segments = ParseTemplateSegments(master, reference)
        };

    public IReadOnlyList<ConstructionLine> ConstructionLines => _constructionLines;

    public int SelectedConstructionIndex
    {
        get => _selectedConstructionIndex;
        set { _selectedConstructionIndex = value; OnPropertyChanged(); }
    }

    public IReadOnlyList<AtopDownlinkInfo> OpenDownlinks => _openDownlinks;

    public int? ReplyToDownlinkId
    {
        get => _replyToDownlinkId;
        set { _replyToDownlinkId = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsReplyMode)); }
    }

    public bool IsReplyMode => _replyToDownlinkId.HasValue;

    public IReadOnlyList<TemplateDisplayItem> AutomatedResponseTemplates =>
        IsReplyMode && _openDownlinks.Count > 0
            ? ComputeAutomatedResponses(_openDownlinks[0])
            : Array.Empty<TemplateDisplayItem>();

    public string ResponseText
    {
        get => _responseText;
        private set { _responseText = value; OnPropertyChanged(); }
    }

    public bool IsProbed
    {
        get => _isProbed;
        private set { _isProbed = value; OnPropertyChanged(); }
    }

    public bool ConflictDetected
    {
        get => _conflictDetected;
        private set { _conflictDetected = value; OnPropertyChanged(); }
    }

    public bool OverrideActive
    {
        get => _overrideActive;
        private set { _overrideActive = value; OnPropertyChanged(); }
    }

    public bool IsSent
    {
        get => _isSent;
        private set { _isSent = value; OnPropertyChanged(); }
    }

    public bool HasActiveProbeState =>
        !string.IsNullOrWhiteSpace(_callsign)
        && ProposedProfileBridge.GetVisualState(_callsign) != StripProfileVisualState.None;

    // -------------------------------------------------------------------------
    // Constructor
    // -------------------------------------------------------------------------
    public ClearanceViewModel()
    {
        ConflictProbe.VirtualProbeResultsReceived += OnVirtualProbeResults;
    }

    // -------------------------------------------------------------------------
    // Initialization
    // -------------------------------------------------------------------------
    private static string BuildRouteWithEstimates(FDP2.FDR? fdr)
    {
        if (fdr?.ParsedRoute == null) return fdr?.Route ?? "";
        var segments = fdr.ParsedRoute.ToList();
        if (segments.Count == 0) return fdr.Route ?? "";

        var parts = new List<string>();
        foreach (var seg in segments)
        {
            var name = seg.Intersection?.Name;
            if (string.IsNullOrWhiteSpace(name)) continue;

            // Skip Z-points (oceanic system waypoints starting with Z) and
            // coordinate waypoints (lat/lon format — always start with a digit).
            if (name[0] == 'Z' || char.IsDigit(name[0])) continue;

            var eto = seg.ETO;
            if (eto == DateTime.MaxValue || eto == default)
                parts.Add($"{name}/");
            else
                parts.Add($"{name} {eto:HHmm}/");
        }

        return parts.Count > 0 ? string.Join(" ", parts) : fdr.Route ?? "";
    }

    public void Load(string callsign)
    {
        Callsign = callsign;

        // Reset state
        _constructionLines.Clear();
        _selectedConstructionIndex = -1;
        _isProbed = false;
        _conflictDetected = false;
        _overrideActive = false;
        _isSent = false;
        ResponseText = "";
        OnPropertyChanged(nameof(HasActiveProbeState));

        // Load route from FDP2 — format as "FIX/HHMM FIX/HHMM ..." using parsed route estimates.
        var fdr = FDP2.GetFDRs.FirstOrDefault(f =>
            string.Equals(f.Callsign, callsign, StringComparison.OrdinalIgnoreCase));
        Route = BuildRouteWithEstimates(fdr);

        // Load message config from CPDLCPlugin
        _config = CpdlcPluginBridge.GetUplinkMessagesConfig();
        if (_config != null)
        {
            _masterLookup.Clear();
            foreach (var m in _config.MasterMessages)
                _masterLookup[m.Id] = m;
        }

        // Load open downlinks
        _openDownlinks.Clear();
        _openDownlinks.AddRange(CpdlcPluginBridge.GetOpenDownlinkDetails(callsign));
        OnPropertyChanged(nameof(OpenDownlinks));

        // Never auto-enter reply mode on a fresh open — caller must explicitly set ReplyToDownlinkId
        // when the window is opened specifically to respond to an incoming CPDLC request.
        ReplyToDownlinkId = null;
        OnPropertyChanged(nameof(AutomatedResponseTemplates));

        // Refresh everything
        _selectedTemplateIndex = -1;
        OnPropertyChanged(nameof(SubCategories));
        SelectedSubCategory = SubCategories.FirstOrDefault();
    }

    // -------------------------------------------------------------------------
    // Automated Response Area — pre-filled reply templates derived from the
    // first open downlink's content (ATOP spec Figure 9-4).
    // -------------------------------------------------------------------------
    private IReadOnlyList<TemplateDisplayItem> ComputeAutomatedResponses(AtopDownlinkInfo downlink)
    {
        var result = new List<TemplateDisplayItem>();
        var content = downlink.Content ?? "";

        // Block level pattern: e.g. "F330B350" or "330B350"
        var blockMatch = Regex.Match(content, @"[Ff]?(\d{3})B(\d{3})");
        if (blockMatch.Success &&
            int.TryParse(blockMatch.Groups[1].Value, out int lev1) &&
            int.TryParse(blockMatch.Groups[2].Value, out int lev2))
        {
            var fill = new Dictionary<string, string> { ["lev"] = lev1.ToString(), ["lev2"] = lev2.ToString() };
            AddAutoTemplate(result, 30, fill);  // MAINTAIN BLOCK [lev] TO [lev2]
            AddAutoTemplate(result, 31, fill);  // CLIMB TO AND MAINTAIN BLOCK [lev] TO [lev2]
            AddAutoTemplate(result, 32, fill);  // DESCEND TO AND MAINTAIN BLOCK [lev] TO [lev2]
            AddAutoTemplate(result, 0,  null);  // UNABLE
            return result;
        }

        // Single FL pattern: FL390, F390
        var flMatch = Regex.Match(content, @"[Ff][Ll]?(\d{3})");
        if (flMatch.Success && int.TryParse(flMatch.Groups[1].Value, out int fl))
        {
            var fill = new Dictionary<string, string> { ["lev"] = fl.ToString() };
            AddAutoTemplate(result, 19, fill);  // MAINTAIN [lev]
            AddAutoTemplate(result, 20, fill);  // CLIMB TO [lev]
            AddAutoTemplate(result, 23, fill);  // DESCENT TO [lev]
            AddAutoTemplate(result, 0,  null);  // UNABLE
            return result;
        }

        // Generic: standard CPDLC response templates
        AddAutoTemplate(result, 3, null);  // ROGER
        AddAutoTemplate(result, 4, null);  // AFFIRM
        AddAutoTemplate(result, 0, null);  // UNABLE
        AddAutoTemplate(result, 5, null);  // NEGATIVE
        AddAutoTemplate(result, 1, null);  // STANDBY
        return result;
    }

    private void AddAutoTemplate(List<TemplateDisplayItem> result, int messageId, Dictionary<string, string>? defaults)
    {
        var template = ResolveTemplate(messageId);
        if (template == null) return;
        result.Add(BuildDisplayItem(template, new AtopMessageReference { MessageId = messageId, DefaultParameters = defaults }));
    }

    // -------------------------------------------------------------------------
    // Grouped templates for category dropdown menus (MOPS structure per NASP-4508K p.282-288)
    // Returns (subgroup name or null for direct items, template list) per group
    // -------------------------------------------------------------------------
    public IReadOnlyList<(string? GroupName, IReadOnlyList<TemplateDisplayItem> Templates)> GetGroupedTemplates(string category)
    {
        var result = new List<(string?, IReadOnlyList<TemplateDisplayItem>)>();

        if (category == "Pre-Fmt")
        {
            if (_config == null) return result;
            var items = new List<TemplateDisplayItem>();
            foreach (var r in _config.PermanentMessages ?? Array.Empty<AtopMessageReference>())
            {
                var master = ResolveTemplate(r.MessageId);
                if (master != null)
                    items.Add(BuildDisplayItem(master, r));
            }
            if (items.Count > 0)
                result.Add(("Permanent", items));
            return result;
        }

        if (MopsMenuStructure.TryGetValue(category, out var groups))
        {
            foreach (var (subGroup, messageIds) in groups)
            {
                var items = new List<TemplateDisplayItem>();
                foreach (var id in messageIds)
                {
                    var master = ResolveTemplate(id);
                    if (master != null)
                        items.Add(BuildDisplayItem(master, new AtopMessageReference { MessageId = id }));
                }
                if (items.Count > 0)
                    result.Add((subGroup, items));
            }
        }

        return result;
    }

    // -------------------------------------------------------------------------
    // Template parsing → segments for inline display
    // -------------------------------------------------------------------------
    private static readonly Regex ParamRegex = new(@"\[([^\]]+)\]", RegexOptions.Compiled);

    /// <summary>
    /// Parses a template string like "CLIMB TO AND MAINTAIN [alt]"
    /// into a list of segments: text parts and parameter field parts.
    /// Default parameter values from the reference are pre-filled.
    /// </summary>
    private static List<TemplateSegment> ParseTemplateSegments(AtopUplinkTemplate master, AtopMessageReference reference)
    {
        var segments = new List<TemplateSegment>();
        var template = master.Template;
        int pos = 0;

        foreach (Match match in ParamRegex.Matches(template))
        {
            // Text before the param
            if (match.Index > pos)
            {
                segments.Add(new TemplateSegment
                {
                    IsParameter = false,
                    Text = template.Substring(pos, match.Index - pos)
                });
            }

            var paramName = match.Groups[1].Value;
            var defaultValue = "";
            if (reference.DefaultParameters != null &&
                reference.DefaultParameters.TryGetValue(paramName, out var dv))
            {
                defaultValue = dv;
            }

            segments.Add(new TemplateSegment
            {
                IsParameter = true,
                ParameterName = paramName,
                Text = defaultValue
            });

            pos = match.Index + match.Length;
        }

        // Trailing text
        if (pos < template.Length)
        {
            segments.Add(new TemplateSegment
            {
                IsParameter = false,
                Text = template.Substring(pos)
            });
        }

        return segments;
    }

    // -------------------------------------------------------------------------
    // Template selection → construction
    // -------------------------------------------------------------------------
    public void AddTemplateToConstruction(TemplateDisplayItem? item)
    {
        if (item == null) return;

        // Per spec: max 5 elements in a single uplink
        if (_constructionLines.Count >= MaxConstructionElements)
        {
            ResponseText = $"The maximum number of uplink clearances is {MaxConstructionElements}, unable to add additional clearance element.";
            return;
        }

        // If message was already sent, clear for new composition
        if (_isSent)
        {
            _constructionLines.Clear();
            _isSent = false;
            _isProbed = false;
            _conflictDetected = false;
            _overrideActive = false;
            ResponseText = "";
        }

        // Deep-copy segments from the template item (may have user-edited values)
        var segments = new List<TemplateSegment>();
        foreach (var seg in item.Segments)
        {
            segments.Add(new TemplateSegment
            {
                IsParameter = seg.IsParameter,
                ParameterName = seg.ParameterName,
                Text = seg.Text
            });
        }

        var line = new ConstructionLine
        {
            Template = item.Template,
            Reference = item.Reference,
            Segments = segments
        };

        // Build parameter values from segments
        foreach (var seg in segments)
        {
            if (seg.IsParameter && !string.IsNullOrEmpty(seg.Text))
                line.ParameterValues[seg.ParameterName] = seg.Text;
        }

        _constructionLines.Add(line);
        _selectedConstructionIndex = _constructionLines.Count - 1;

        // Reset probe state when construction changes
        _isProbed = false;
        _conflictDetected = false;
        _overrideActive = false;
        if (ResponseText.StartsWith("The maximum"))
            ResponseText = "";  // Clear limit error if we somehow got past it
        else
            ResponseText = "";

        OnPropertyChanged(nameof(ConstructionLines));
        OnPropertyChanged(nameof(SelectedConstructionIndex));
    }

    /// <summary>
    /// Inserts the selected template into the construction area at a position.
    /// If called from INS button, inserts BEFORE the selected construction line.
    /// If called from EOS/double-click, appends at end.
    /// </summary>
    public void InsertTemplateAtSelection(TemplateDisplayItem? item)
    {
        if (item == null) return;

        if (_constructionLines.Count >= MaxConstructionElements)
        {
            ResponseText = $"The maximum number of uplink clearances is {MaxConstructionElements}, unable to add additional clearance element.";
            return;
        }

        if (_isSent)
        {
            _constructionLines.Clear();
            _isSent = false;
            _isProbed = false;
            _conflictDetected = false;
            _overrideActive = false;
            ResponseText = "";
        }

        var segments = new List<TemplateSegment>();
        foreach (var seg in item.Segments)
        {
            segments.Add(new TemplateSegment
            {
                IsParameter = seg.IsParameter,
                ParameterName = seg.ParameterName,
                Text = seg.Text
            });
        }

        var line = new ConstructionLine
        {
            Template = item.Template,
            Reference = item.Reference,
            Segments = segments
        };

        foreach (var seg in segments)
        {
            if (seg.IsParameter && !string.IsNullOrEmpty(seg.Text))
                line.ParameterValues[seg.ParameterName] = seg.Text;
        }

        // INS: insert before selected construction line
        int insertAt = _selectedConstructionIndex >= 0 && _selectedConstructionIndex < _constructionLines.Count
            ? _selectedConstructionIndex
            : _constructionLines.Count;

        _constructionLines.Insert(insertAt, line);
        _selectedConstructionIndex = insertAt;

        // Reset probe state when construction changes
        _isProbed = false;
        _conflictDetected = false;
        _overrideActive = false;
        ResponseText = "";

        OnPropertyChanged(nameof(ConstructionLines));
        OnPropertyChanged(nameof(SelectedConstructionIndex));
    }

    /// <summary>
    /// INS button: inserts the currently selected template BEFORE the selected construction line.
    /// </summary>
    private void InsertConstructionLine()
    {
        var templates = VisibleTemplates;
        if (_selectedTemplateIndex >= 0 && _selectedTemplateIndex < templates.Count)
            InsertTemplateAtSelection(templates[_selectedTemplateIndex]);
    }

    /// <summary>
    /// DEL button: removes the selected construction line.
    /// </summary>
    public void DeleteSelectedConstructionLine()
    {
        if (_selectedConstructionIndex >= 0 && _selectedConstructionIndex < _constructionLines.Count)
        {
            _constructionLines.RemoveAt(_selectedConstructionIndex);
            if (_selectedConstructionIndex >= _constructionLines.Count)
                _selectedConstructionIndex = _constructionLines.Count - 1;

            // Reset probe state when construction changes
            _isProbed = false;
            _conflictDetected = false;
            _overrideActive = false;
            ResponseText = "";

            OnPropertyChanged(nameof(ConstructionLines));
            OnPropertyChanged(nameof(SelectedConstructionIndex));
        }
    }

    /// <summary>
    /// Updates a parameter value in a construction line. 
    /// Called from the UI when the user edits a variable field.
    /// </summary>
    public void UpdateConstructionParameter(int lineIndex, string paramName, string value)
    {
        if (lineIndex < 0 || lineIndex >= _constructionLines.Count) return;

        var line = _constructionLines[lineIndex];
        line.ParameterValues[paramName] = value;

        // Also update the segment text for display
        foreach (var seg in line.Segments)
        {
            if (seg.IsParameter && seg.ParameterName == paramName)
                seg.Text = value;
        }
    }

    private void ClearConstruction()
    {
        _constructionLines.Clear();
        _selectedConstructionIndex = -1;
        _isProbed = false;
        _conflictDetected = false;
        _overrideActive = false;
        ResponseText = "";
        OnPropertyChanged(nameof(ConstructionLines));
        OnPropertyChanged(nameof(SelectedConstructionIndex));
    }

    /// <summary>
    /// CAN button per spec: cancels the probed proposed profile and clears construction.
    /// </summary>
    public void ExecuteCancel()
    {
        ProposedProfileBridge.Clear(_callsign);
        ClearConstruction();
        _replyToDownlinkId = null;
        OnPropertyChanged(nameof(ReplyToDownlinkId));
        OnPropertyChanged(nameof(IsReplyMode));
        OnPropertyChanged(nameof(AutomatedResponseTemplates));
        OnPropertyChanged(nameof(HasActiveProbeState));
    }

    // -------------------------------------------------------------------------
    // Send / Unable / Override
    // -------------------------------------------------------------------------

    /// <summary>
    /// Checks for UNABLE element mixed with clearance elements.
    /// Per spec: "UNABLE" is to be used without clearance segment(s).
    /// </summary>
    private bool ValidateConstruction()
    {
        if (_constructionLines.Count == 0) return false;

        bool hasUnable = _constructionLines.Any(l => l.Template.Id == 0); // Message 0 = UNABLE
        bool hasClearance = _constructionLines.Any(l => l.Template.Id != 0);

        if (hasUnable && hasClearance)
        {
            ResponseText = "\"UNABLE\" is to be used without clearance segment(s).";
            return false;
        }

        // Check all parameter fields are filled
        foreach (var line in _constructionLines)
        {
            foreach (var seg in line.Segments)
            {
                if (seg.IsParameter && string.IsNullOrWhiteSpace(seg.Text))
                {
                    ResponseText = $"Parameter [{seg.ParameterName}] in message {line.Template.Id} must be filled.";
                    return false;
                }
            }
        }

        return true;
    }

    public void ExecuteSend()
    {
        if (_constructionLines.Count == 0) return;

        if (!ValidateConstruction()) return;

        // Build the final content string — lines separated by ". "
        // Wrap parameter values in @value@ per CPDLC protocol
        var parts = new List<string>();
        int maxResponseType = 0;

        foreach (var line in _constructionLines)
        {
            var text = line.Template.Template;
            foreach (var kvp in line.ParameterValues)
            {
                text = text.Replace($"[{kvp.Key}]", $"@{kvp.Value}@");
            }
            parts.Add(text);

            var rt = line.Reference.ResponseType ?? line.Template.ResponseType;
            if (rt > maxResponseType) maxResponseType = rt;
        }

        var content = string.Join(". ", parts);

        CpdlcPluginBridge.SendUplink(_callsign, _replyToDownlinkId, maxResponseType, content);

        ResponseText = "Message sent.";
        IsSent = true;
        _replyToDownlinkId = null;
        OnPropertyChanged(nameof(ReplyToDownlinkId));
        OnPropertyChanged(nameof(IsReplyMode));
    }

    public void ExecuteSendHf()
    {
        if (_constructionLines.Count == 0) return;
        if (!ValidateConstruction()) return;

        // Build human-readable clearance text — no CPDLC @param@ wrappers.
        var parts = new List<string>();
        foreach (var line in _constructionLines)
        {
            var text = line.Template.Template;
            foreach (var kvp in line.ParameterValues)
                text = text.Replace($"[{kvp.Key}]", kvp.Value);
            parts.Add(text);
        }

        Network.SendRadioMessage($"{_callsign} {string.Join(". ", parts)}");
        ResponseText = "Message sent (HF).";
        IsSent = true;
        _replyToDownlinkId = null;
        OnPropertyChanged(nameof(ReplyToDownlinkId));
        OnPropertyChanged(nameof(IsReplyMode));
    }

    /// <summary>
    /// UNABL button per spec: Cancels probe, places "UNABLE" + "DUE TO TRAFFIC" in construction,
    /// then sends. Controller may add free text before sending.
    /// </summary>
    public void ExecuteUnable()
    {
        if (!_replyToDownlinkId.HasValue) return;

        // Per spec: clear current construction and populate with UNABLE + DUE TO TRAFFIC
        _constructionLines.Clear();
        _isProbed = false;
        _conflictDetected = false;
        _overrideActive = false;

        // Add UNABLE (message 0) if available in master list
        if (_masterLookup.TryGetValue(0, out var unableTemplate))
        {
            var dummyRef = new AtopMessageReference { MessageId = 0, ResponseType = 0 };
            _constructionLines.Add(new ConstructionLine
            {
                Template = unableTemplate,
                Reference = dummyRef,
                Segments = ParseTemplateSegments(unableTemplate, dummyRef)
            });
        }

        // Add DUE TO TRAFFIC (message 166) if available
        if (_masterLookup.TryGetValue(166, out var dueTrafficTemplate))
        {
            var dummyRef = new AtopMessageReference { MessageId = 166, ResponseType = 0 };
            _constructionLines.Add(new ConstructionLine
            {
                Template = dueTrafficTemplate,
                Reference = dummyRef,
                Segments = ParseTemplateSegments(dueTrafficTemplate, dummyRef)
            });
        }

        _selectedConstructionIndex = _constructionLines.Count > 0 ? 0 : -1;

        ResponseText = "UNABLE response prepared. Select SND to send, or add free text.";
        OnPropertyChanged(nameof(ConstructionLines));
        OnPropertyChanged(nameof(SelectedConstructionIndex));
    }

    /// <summary>
    /// OVRD button per spec: overrides conflict detection to allow sending.
    /// </summary>
    public void ExecuteOverride()
    {
        if (!_conflictDetected) return;
        OverrideActive = true;
        ResponseText = "Override active. Select SND to send despite conflict.";
    }

    public void ExecuteVhf()
    {
        if (_constructionLines.Count == 0) return;
        if (!ValidateConstruction()) return;

        int? proposedCfl = ExtractProposedAltitude();
        if (proposedCfl == null)
        {
            var fdr0 = FDP2.GetFDRs.FirstOrDefault(f =>
                string.Equals(f.Callsign, _callsign, StringComparison.OrdinalIgnoreCase));
            proposedCfl = fdr0 != null && fdr0.CFLUpper != -1 ? fdr0.CFLUpper / 100 : fdr0?.RFL / 100;
        }

        if (proposedCfl == null || proposedCfl <= 0)
        {
            ResponseText = "Cannot determine proposed altitude.";
            return;
        }

        var fdr = FDP2.GetFDRs.FirstOrDefault(f =>
            string.Equals(f.Callsign, _callsign, StringComparison.OrdinalIgnoreCase));
        if (fdr == null)
        {
            ResponseText = "Flight not found.";
            return;
        }

        FDP2.SetCFL(fdr, proposedCfl.Value.ToString());
        IsProbed = true;
        ResponseText = "Probing...";
        ConflictProbe.RequestVirtualProbe(_callsign, proposedCfl.Value);
    }

    /// <summary>
    /// PRB button: sends the proposed clearance to the conflict worker as a virtual FDR.
    /// Extracts the proposed CFL from construction lines (altitude messages like CLIMB/DESCEND/MAINTAIN).
    /// The conflict worker temporarily injects it, runs detection, and returns results
    /// without creating any real FDR or visible artifacts in vatSys.
    /// </summary>
    public void ExecuteProbe()
    {
        if (_constructionLines.Count == 0) return;
        if (!ValidateConstruction()) return;
        if (HasActiveProbeState)
        {
            ResponseText = "Probe already active.";
            return;
        }

        // Try to extract a proposed CFL from the construction lines.
        // Look for altitude parameters in segments (e.g. [alt], [altitude], [level], [fl]).
        int? proposedCfl = ExtractProposedAltitude();

        if (proposedCfl == null)
        {
            // No altitude change found — probe with current CFL
            var fdr = FDP2.GetFDRs.FirstOrDefault(f =>
                string.Equals(f.Callsign, _callsign, StringComparison.OrdinalIgnoreCase));
            proposedCfl = fdr != null && fdr.CFLUpper != -1 ? fdr.CFLUpper / 100 : fdr?.RFL / 100;
        }

        if (proposedCfl == null || proposedCfl <= 0)
        {
            ResponseText = "Cannot determine proposed altitude for probe.";
            return;
        }

        var probeTargetExists = FDP2.GetFDRs.Any(f =>
            string.Equals(f.Callsign, _callsign, StringComparison.OrdinalIgnoreCase));
        if (!probeTargetExists)
        {
            ResponseText = "Flight not found for probe.";
            return;
        }

        if (!ProposedProfileBridge.TryBeginProbe(_callsign))
        {
            ResponseText = "Probe already active.";
            return;
        }

        IsProbed = true;
        ResponseText = "Probing...";
        OnPropertyChanged(nameof(HasActiveProbeState));
        ConflictProbe.RequestVirtualProbe(_callsign, proposedCfl.Value);
    }

    /// <summary>
    /// Extracts the proposed flight level from construction line parameters.
    /// Looks for altitude-related parameters and tries to parse them as flight levels.
    /// </summary>
    private int? ExtractProposedAltitude()
    {
        var altParamNames = new[] { "alt", "altitude", "level", "fl", "flight level" };

        foreach (var line in _constructionLines)
        {
            foreach (var seg in line.Segments)
            {
                if (!seg.IsParameter) continue;
                if (string.IsNullOrWhiteSpace(seg.Text)) continue;

                var paramLower = seg.ParameterName.ToLower();
                if (!altParamNames.Any(a => paramLower.Contains(a))) continue;

                // Try to parse: "350", "FL350", "F350"
                var val = seg.Text.Trim().ToUpper()
                    .Replace("FL", "").Replace("F", "").Trim();
                if (int.TryParse(val, out int fl) && fl > 0 && fl <= 600)
                    return fl;
            }
        }

        return null;
    }

    /// <summary>
    /// Handles virtual probe results from the conflict worker.
    /// Only processes results for the current callsign.
    /// </summary>
    private void OnVirtualProbeResults(string callsign, ConflictProbe.Conflicts conflicts)
    {
        if (!string.Equals(callsign, _callsign, StringComparison.OrdinalIgnoreCase)) return;
        if (!_isProbed) return;  // We didn't request this probe

        var totalConflicts = conflicts.ActualConflicts.Count
                           + conflicts.ImminentConflicts.Count
                           + conflicts.AdvisoryConflicts.Count;

        if (totalConflicts == 0)
        {
            ConflictDetected = false;
            ResponseText = NoProceduralConflictsFoundMessage;
        }
        else
        {
            ConflictDetected = true;

            // ATOP page 291 format: aircraft count + airspace count.
            // The current worker returns aircraft conflicts but no airspace details.
            var aircraftCount = conflicts.ActualConflicts
                .Concat(conflicts.ImminentConflicts)
                .Concat(conflicts.AdvisoryConflicts)
                .Select(c => c.Intruder?.Callsign == callsign ? c.Active?.Callsign : c.Intruder?.Callsign)
                .Where(cs => cs != null)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            var airspaceCount = 0;

            ResponseText = string.Format(ConflictWithCountsMessageTemplate, aircraftCount, airspaceCount);
        }

        OnPropertyChanged(nameof(HasActiveProbeState));
    }

    // -------------------------------------------------------------------------
    // INotifyPropertyChanged
    // -------------------------------------------------------------------------
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    // -------------------------------------------------------------------------
    // Nested types
    // -------------------------------------------------------------------------

    /// <summary>
    /// A segment of a template: either literal text or an editable parameter field.
    /// </summary>
    public class TemplateSegment
    {
        public bool IsParameter { get; set; }
        public string ParameterName { get; set; } = "";
        public string Text { get; set; } = "";
    }

    /// <summary>
    /// A template row in the template list area.
    /// Contains parsed segments for inline field rendering.
    /// </summary>
    public class TemplateDisplayItem
    {
        public int MessageId { get; set; }
        public AtopUplinkTemplate Template { get; set; } = null!;
        public AtopMessageReference Reference { get; set; } = null!;
        public List<TemplateSegment> Segments { get; set; } = new();
    }

    /// <summary>
    /// A line in the construction area.
    /// </summary>
    public class ConstructionLine
    {
        public AtopUplinkTemplate Template { get; set; } = null!;
        public AtopMessageReference Reference { get; set; } = null!;
        public List<TemplateSegment> Segments { get; set; } = new();
        public Dictionary<string, string> ParameterValues { get; } = new();

        /// <summary>
        /// Display text: "(ID) MESSAGE TEXT param_value"
        /// </summary>
        public string DisplayText
        {
            get
            {
                var text = Template.Template;
                foreach (var kvp in ParameterValues)
                    text = text.Replace($"[{kvp.Key}]", kvp.Value);
                return $"({Template.Id}) {text}";
            }
        }
    }

}
