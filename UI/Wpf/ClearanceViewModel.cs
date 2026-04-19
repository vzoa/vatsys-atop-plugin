using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows.Input;
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
        CancelCommand = new RelayCommand(ClearConstruction);
        UnableCommand = new RelayCommand(ExecuteUnable, () => _replyToDownlinkId.HasValue);
        CloseCommand = new RelayCommand(() => RequestClose?.Invoke());
    }

    // -------------------------------------------------------------------------
    // Initialization
    // -------------------------------------------------------------------------
    public void Load(string callsign)
    {
        Callsign = callsign;

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
    // Template parsing → segments for inline display
    // -------------------------------------------------------------------------
    private static readonly Regex ParamRegex = new(@"\[(\w+)\]", RegexOptions.Compiled);

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
        OnPropertyChanged(nameof(ConstructionLines));
        OnPropertyChanged(nameof(SelectedConstructionIndex));
    }

    /// <summary>
    /// INS button: adds the currently selected template to the construction area.
    /// </summary>
    private void InsertConstructionLine()
    {
        var templates = VisibleTemplates;
        if (_selectedTemplateIndex >= 0 && _selectedTemplateIndex < templates.Count)
            AddTemplateToConstruction(templates[_selectedTemplateIndex]);
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
        _replyToDownlinkId = null;
        OnPropertyChanged(nameof(ConstructionLines));
        OnPropertyChanged(nameof(SelectedConstructionIndex));
        OnPropertyChanged(nameof(ReplyToDownlinkId));
        OnPropertyChanged(nameof(IsReplyMode));
    }

    // -------------------------------------------------------------------------
    // Send / Unable
    // -------------------------------------------------------------------------
    private void ExecuteSend()
    {
        if (_constructionLines.Count == 0) return;

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

        ClearConstruction();
    }

    private void ExecuteUnable()
    {
        if (!_replyToDownlinkId.HasValue) return;
        CpdlcPluginBridge.SendUnable(_replyToDownlinkId.Value, _callsign);
        ReplyToDownlinkId = null;
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
