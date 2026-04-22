using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows.Input;
using AtopPlugin.Conflict;
using AtopPlugin.Helpers;
using AtopPlugin.Models;
using vatsys;

namespace AtopPlugin.UI.Wpf;

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
    // Category → Group name mapping
    // -------------------------------------------------------------------------
    private static readonly Dictionary<string, string[]> CategoryGroupPatterns = new()
    {
        ["Urgent"]  = new[] { "Urgent", "Emergency" },
        ["Rpt"]     = new[] { "Report" },
        ["Negot"]   = new[] { "Negotiat", "Request" },
        ["Rspn"]    = new[] { "Response", "Acknowledge", "Affirm", "Roger", "Unable", "Standby" },
        ["Misc"]    = Array.Empty<string>(), // catch-all
        ["Vert"]    = new[] { "Climb", "Descend", "Level", "Altitude", "Block", "Cruise", "Expect" },
        ["Route"]   = new[] { "Route", "Direct", "Proceed", "Offset", "Deviat", "Track", "Cleared" },
        ["Speed"]   = new[] { "Speed", "Mach" },
        ["X-ing"]   = new[] { "Cross" },
        ["Comm"]    = new[] { "Contact", "Monitor", "Frequency", "Squawk" },
        ["Pre-Fmt"] = new[] { "Pre-Formatted", "PreFormatted", "Permanent" }
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

    // Category → list of groups
    private readonly Dictionary<string, List<AtopMessageGroup>> _categoryGroups = new();

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

            if (_categoryGroups.TryGetValue(_selectedCategory, out var groups))
                return groups.Select(g => g.Name).ToArray();

            return Array.Empty<string>();
        }
    }

    public IReadOnlyList<TemplateDisplayItem> VisibleTemplates
    {
        get
        {
            if (_config == null) return Array.Empty<TemplateDisplayItem>();

            AtopMessageReference[] refs;

            if (_selectedCategory == "Pre-Fmt")
            {
                refs = _config.PermanentMessages;
            }
            else if (_selectedSubCategory != null
                     && _categoryGroups.TryGetValue(_selectedCategory, out var groups))
            {
                var group = groups.FirstOrDefault(g => g.Name == _selectedSubCategory);
                refs = group?.Messages ?? Array.Empty<AtopMessageReference>();
            }
            else
            {
                refs = Array.Empty<AtopMessageReference>();
            }

            var items = new List<TemplateDisplayItem>();
            foreach (var r in refs)
            {
                if (_masterLookup.TryGetValue(r.MessageId, out var master))
                {
                    var display = new TemplateDisplayItem
                    {
                        MessageId = master.Id,
                        Template = master,
                        Reference = r,
                        Segments = ParseTemplateSegments(master, r)
                    };
                    items.Add(display);
                }
            }
            return items;
        }
    }

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

    // -------------------------------------------------------------------------
    // Commands
    // -------------------------------------------------------------------------
    public ICommand SelectTemplateCommand { get; }
    public ICommand InsertConstructionLineCommand { get; }
    public ICommand DeleteConstructionLineCommand { get; }
    public ICommand SendCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand UnableCommand { get; }
    public ICommand CloseCommand { get; }
    public ICommand OverrideCommand { get; }
    public ICommand ProbeCommand { get; }

    public event Action? RequestClose;

    // -------------------------------------------------------------------------
    // Constructor
    // -------------------------------------------------------------------------
    public ClearanceViewModel()
    {
        SelectTemplateCommand = new RelayCommand<TemplateDisplayItem>(AddTemplateToConstruction);
        InsertConstructionLineCommand = new RelayCommand(InsertConstructionLine, () => _selectedTemplateIndex >= 0);
        DeleteConstructionLineCommand = new RelayCommand(DeleteSelectedConstructionLine, () => _selectedConstructionIndex >= 0);
        SendCommand = new RelayCommand(ExecuteSend, () => _constructionLines.Count > 0);
        CancelCommand = new RelayCommand(ExecuteCancel);
        UnableCommand = new RelayCommand(ExecuteUnable, () => _replyToDownlinkId.HasValue);
        CloseCommand = new RelayCommand(() => RequestClose?.Invoke());
        OverrideCommand = new RelayCommand(ExecuteOverride, () => _conflictDetected && !_overrideActive);
        ProbeCommand = new RelayCommand(ExecuteProbe, () => _constructionLines.Count > 0 && !_isProbed);

        // Subscribe to virtual probe results from conflict worker
        ConflictProbe.VirtualProbeResultsReceived += OnVirtualProbeResults;
    }

    // -------------------------------------------------------------------------
    // Initialization
    // -------------------------------------------------------------------------
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

        // Load route from FDP2
        var fdr = FDP2.GetFDRs.FirstOrDefault(f =>
            string.Equals(f.Callsign, callsign, StringComparison.OrdinalIgnoreCase));
        Route = fdr?.ParsedRoute?.ToString() ?? fdr?.Route ?? "";

        // Load message config from CPDLCPlugin
        _config = CpdlcPluginBridge.GetUplinkMessagesConfig();
        if (_config != null)
        {
            _masterLookup.Clear();
            foreach (var m in _config.MasterMessages)
                _masterLookup[m.Id] = m;

            BuildCategoryGroups();
        }

        // Load open downlinks
        _openDownlinks.Clear();
        _openDownlinks.AddRange(CpdlcPluginBridge.GetOpenDownlinkDetails(callsign));
        OnPropertyChanged(nameof(OpenDownlinks));

        // Auto-select first downlink as reply target if any
        if (_openDownlinks.Count > 0)
            ReplyToDownlinkId = _openDownlinks[0].MessageId;

        // Refresh everything
        _selectedTemplateIndex = -1;
        OnPropertyChanged(nameof(SubCategories));
        SelectedSubCategory = SubCategories.FirstOrDefault();
    }

    private void BuildCategoryGroups()
    {
        _categoryGroups.Clear();
        if (_config == null) return;

        var assignedGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var catName in CategoryNames)
        {
            if (catName == "Pre-Fmt" || catName == "Misc") continue;

            var patterns = CategoryGroupPatterns[catName];
            var matching = _config.Groups
                .Where(g => patterns.Any(p => g.Name.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0))
                .ToList();

            foreach (var g in matching)
                assignedGroups.Add(g.Name);

            _categoryGroups[catName] = matching;
        }

        // Misc = everything not assigned
        _categoryGroups["Misc"] = _config.Groups
            .Where(g => !assignedGroups.Contains(g.Name))
            .ToList();
    }

    // -------------------------------------------------------------------------
    // Grouped templates for dropdown menu (does not mutate state)
    // -------------------------------------------------------------------------
    public IReadOnlyList<(string GroupName, IReadOnlyList<TemplateDisplayItem> Templates)> GetGroupedTemplates(string category)
    {
        var result = new List<(string, IReadOnlyList<TemplateDisplayItem>)>();
        if (_config == null) return result;

        if (category == "Pre-Fmt")
        {
            var items = new List<TemplateDisplayItem>();
            foreach (var r in _config.PermanentMessages ?? Array.Empty<AtopMessageReference>())
            {
                if (_masterLookup.TryGetValue(r.MessageId, out var master))
                {
                    items.Add(new TemplateDisplayItem
                    {
                        MessageId = master.Id,
                        Template = master,
                        Reference = r,
                        Segments = ParseTemplateSegments(master, r)
                    });
                }
            }
            if (items.Count > 0)
                result.Add(("Permanent", items));
            return result;
        }

        if (_categoryGroups.TryGetValue(category, out var groups))
        {
            foreach (var group in groups)
            {
                var items = new List<TemplateDisplayItem>();
                foreach (var r in group.Messages)
                {
                    if (_masterLookup.TryGetValue(r.MessageId, out var master))
                    {
                        items.Add(new TemplateDisplayItem
                        {
                            MessageId = master.Id,
                            Template = master,
                            Reference = r,
                            Segments = ParseTemplateSegments(master, r)
                        });
                    }
                }
                if (items.Count > 0)
                    result.Add((group.Name, items));
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
    private void DeleteSelectedConstructionLine()
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
    private void ExecuteCancel()
    {
        ClearConstruction();
        _replyToDownlinkId = null;
        OnPropertyChanged(nameof(ReplyToDownlinkId));
        OnPropertyChanged(nameof(IsReplyMode));
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

    private void ExecuteSend()
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

    /// <summary>
    /// UNABL button per spec: Cancels probe, places "UNABLE" + "DUE TO TRAFFIC" in construction,
    /// then sends. Controller may add free text before sending.
    /// </summary>
    private void ExecuteUnable()
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
    private void ExecuteOverride()
    {
        if (!_conflictDetected) return;
        OverrideActive = true;
        ResponseText = "Override active. Select SND to send despite conflict.";
    }

    /// <summary>
    /// PRB button: sends the proposed clearance to the conflict worker as a virtual FDR.
    /// Extracts the proposed CFL from construction lines (altitude messages like CLIMB/DESCEND/MAINTAIN).
    /// The conflict worker temporarily injects it, runs detection, and returns results
    /// without creating any real FDR or visible artifacts in vatSys.
    /// </summary>
    private void ExecuteProbe()
    {
        if (_constructionLines.Count == 0) return;
        if (!ValidateConstruction()) return;

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

        IsProbed = true;
        ResponseText = "Probing...";
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

    // -------------------------------------------------------------------------
    // Simple ICommand implementation
    // -------------------------------------------------------------------------
    private class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;

        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;
        public void Execute(object? parameter) => _execute();
    }

    private class RelayCommand<T> : ICommand
    {
        private readonly Action<T?> _execute;
        private readonly Func<T?, bool>? _canExecute;

        public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter is T t ? t : default) ?? true;

        public void Execute(object? parameter)
        {
            if (parameter is T t)
                _execute(t);
            else if (parameter is string s && typeof(T) == typeof(string))
                _execute((T)(object)s);
            else if (parameter is int i && typeof(T) == typeof(int))
                _execute((T)(object)i);
            else
                _execute(default);
        }
    }
}
