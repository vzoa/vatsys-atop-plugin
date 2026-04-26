using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using AtopPlugin.Display;
using AtopPlugin.Helpers;
using vatsys;

namespace AtopPlugin.UI;

public class ClearanceWindowClassic : BaseForm
{
    private const int ActionButtonHeight = 24;
    private const int DefaultActionButtonWidth = 50;

    private readonly ClearanceViewModel _vm = new();
    private readonly Font _monoFont = ResolveMonoFont();
    private readonly Color _windowBackground = Colours.GetColour(Colours.Identities.WindowBackground);
    private readonly Color _interactiveText = Colours.GetColour(Colours.Identities.InteractiveText);
    private readonly Color _nonInteractiveText = Colours.GetColour(Colours.Identities.NonInteractiveText);
    private readonly Color _selectionBackground = Color.FromArgb(0xD8, 0xD8, 0xD8);
    private readonly Color _downlinkColor = Color.FromArgb(0xFF, 0x66, 0x00);
    private readonly Color _okColor = Color.FromArgb(0x00, 0x80, 0x00);
    private readonly Color _conflictColor = Color.FromArgb(0xCC, 0x00, 0x00);

    private readonly Dictionary<MenuButton, string> _categoryButtons = new();
    private readonly Dictionary<MenuButton, string> _shortcutButtons = new();

    private readonly Label _callsignLabel = new();
    private readonly Label _routeLabel = new();
    private readonly FlowLayoutPanel _categoryPanel = new();
    private readonly FlowLayoutPanel _shortcutPanel = new();
    private readonly Panel _templateViewport = new();
    private readonly FlowLayoutPanel _templateContent = new();
    private readonly VATSYSControls.ScrollBar _templateScrollBar = new();
    private readonly Panel _constructionViewport = new();
    private readonly FlowLayoutPanel _constructionContent = new();
    private readonly VATSYSControls.ScrollBar _constructionScrollBar = new();
    private readonly Panel _responsePanel = new();
    private readonly Label _responseLabel = new();
    private readonly Panel _downlinkViewport = new();
    private readonly FlowLayoutPanel _downlinkContent = new();
    private readonly VATSYSControls.ScrollBar _downlinkScrollBar = new();

    private readonly Panel _autoResponseViewport = new();
    private readonly FlowLayoutPanel _autoResponseContent = new();
    private readonly VATSYSControls.ScrollBar _autoResponseScrollBar = new();
    private TableLayoutPanel? _root;
    private Control? _autoResponseRowHost;

    private readonly GenericButton _probeButton;
    private readonly GenericButton _cancelButton;
    private readonly GenericButton _sendButton;
    private readonly GenericButton _unableButton;
    private readonly GenericButton _overrideButton;
    private readonly GenericButton _vhfButton;

    private MenuButton? _selectedCategoryButton;
    private MenuButton? _selectedShortcutButton;
    private List<ClearanceViewModel.TemplateDisplayItem> _currentTemplates = new();
    private ContextMenuStrip? _activeMenu;
    private bool _syncingScrollbars;

    public ClearanceWindowClassic()
    {
        Name = nameof(ClearanceWindowClassic);
        Text = "CLEARANCE";
        HideOnClose = true;
        ControlBox = false;
        MinimumSize = new Size(760, 420);
        MaximumSize = new Size(760, 520);
        Size = new Size(760, 420);
        BackColor = _windowBackground;
        Font = _monoFont;

        _root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = _windowBackground,
            ColumnCount = 1,
            RowCount = 9,
            Padding = new Padding(6),
            Margin = new Padding(0)
        };
        var root = _root;
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 54));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 24));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));  // auto response — hidden until IsReplyMode
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));

        root.Controls.Add(BuildHeaderRow(), 0, 0);
        root.Controls.Add(BuildCategoryRow(), 0, 1);
        root.Controls.Add(BuildShortcutRow(), 0, 2);
        root.Controls.Add(BuildTemplateRow(), 0, 3);
        root.Controls.Add(BuildConstructionRow(), 0, 4);
        root.Controls.Add(BuildResponseRow(), 0, 5);
        root.Controls.Add(BuildDownlinkRow(), 0, 6);
        _autoResponseRowHost = BuildAutoResponseRow();
        root.Controls.Add(_autoResponseRowHost, 0, 7);
        root.Controls.Add(BuildActionRow(), 0, 8);

        Controls.Add(root);

        _probeButton = CreateActionButton("PRB", ProbeButton_Click);
        _cancelButton = CreateActionButton("CAN", CancelButton_Click);
        _sendButton = CreateActionButton("SND", SendButton_Click);
        _sendButton.MouseDown += SendButton_MouseDown;
        _unableButton = CreateActionButton("UNABL", UnableButton_Click, width: 56);
        _overrideButton = CreateActionButton("OVRD", OverrideButton_Click);
        _vhfButton = CreateActionButton("VHF", VhfButton_Click);

        var actionHost = (FlowLayoutPanel)root.GetControlFromPosition(0, 8)!;
        actionHost.Controls.AddRange(new Control[]
        {
            _probeButton,
            _cancelButton,
            CreateActionButton("TPRB", null, enabled: false),
            _sendButton,
            _unableButton,
            _vhfButton,
            CreateActionButton("SAVE", null, enabled: false),
            CreateActionButton("EALT", null, enabled: false),
            _overrideButton,
            CreateActionButton("COORD", CoordButton_Click, width: 56),
            CreateActionButton("ACPT", null, enabled: false),
            CreateActionButton("REJ", null, enabled: false),
            CreateActionButton("HLP", null, enabled: false),
            CreateActionButton("CLS", CloseButton_Click)
        });

        _vm.PropertyChanged += Vm_PropertyChanged;
        ProposedProfileBridge.ProbeStateChanged += OnSharedProbeStateChanged;

        BuildCategoryTabs();
        RefreshResponse();
        RefreshDownlinks();
        RefreshAutoResponses();
        MeartsUiFonts.Apply(this);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            ProposedProfileBridge.ProbeStateChanged -= OnSharedProbeStateChanged;

        base.Dispose(disposing);
    }

    public void ShowForCallsign(string callsign, int? replyDownlinkId = null)
    {
        var sourceFdr = FDP2.GetFDRs.FirstOrDefault(f =>
            string.Equals(f.Callsign, callsign, StringComparison.OrdinalIgnoreCase));
        var resolvedCallsign = sourceFdr?.Callsign ?? callsign;

        _vm.Load(resolvedCallsign);
        _callsignLabel.Text = (resolvedCallsign ?? string.Empty).ToUpperInvariant();
        _routeLabel.Text = _vm.Route;

        // Set reply mode only when explicitly provided (opens from CPDLC comm icon).
        _vm.ReplyToDownlinkId = replyDownlinkId;

        RefreshDownlinks();
        RefreshShortcuts();
        RefreshTemplates();
        RefreshConstruction();
        RefreshResponse();
        RefreshAutoResponses();

        var match = _categoryButtons.FirstOrDefault(pair => pair.Value == _vm.SelectedCategory).Key;
        if (match != null)
            SelectCategoryButton(match);

        // Force layout pass so header row (callsign/route labels) calculates and renders properly
        PerformLayout();
        _callsignLabel.Invalidate();
        _routeLabel.Invalidate();

        Show(Form.ActiveForm ?? this);
        BringToFront();
        Activate();
    }

    // Crossing Arrow cursor while hovering over or dragging the title bar (ATOP cursor form 4).
    // WM_SETCURSOR fires whenever the cursor moves, including both hover and during the modal
    // move loop — unlike WM_ENTERSIZEMOVE which only reached the client-area Cursor property.
    protected override void WndProc(ref Message m)
    {
        const int WM_SETCURSOR = 0x0020;
        const int HTCAPTION = 2;
        if (m.Msg == WM_SETCURSOR && (m.LParam.ToInt32() & 0xFFFF) == HTCAPTION)
        {
            Cursor.Current = CursorManager.Move;
            m.Result = new IntPtr(1);
            return;
        }
        base.WndProc(ref m);
    }

    private static Font ResolveMonoFont()
    {
        return MeartsUiFonts.GetFont(12f, FontStyle.Bold, GraphicsUnit.Point);
    }

    private Control BuildHeaderRow()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = _windowBackground,
            ColumnCount = 2,
            Margin = new Padding(0)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _callsignLabel.BackColor = Color.Black;
        _callsignLabel.ForeColor = Color.Yellow;
        _callsignLabel.Font = _monoFont;
        _callsignLabel.TextAlign = ContentAlignment.MiddleCenter;
        _callsignLabel.BorderStyle = BorderStyle.FixedSingle;
        _callsignLabel.Dock = DockStyle.Fill;
        _callsignLabel.Margin = new Padding(0, 0, 6, 0);
        _callsignLabel.AutoSize = false;

        var routeBorder = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(4, 6, 4, 4),
            Margin = new Padding(0)
        };
        routeBorder.Paint += BorderPanel_Paint;

        _routeLabel.Dock = DockStyle.Fill;
        _routeLabel.BackColor = Color.White;
        _routeLabel.ForeColor = _interactiveText;
        _routeLabel.Font = _monoFont;
        _routeLabel.TextAlign = ContentAlignment.MiddleLeft;
        _routeLabel.AutoEllipsis = true;
        _routeLabel.AutoSize = false;

        routeBorder.Controls.Add(_routeLabel);

        panel.Controls.Add(_callsignLabel, 0, 0);
        panel.Controls.Add(routeBorder, 1, 0);
        return panel;
    }

    private Control BuildCategoryRow()
    {
        _categoryPanel.Dock = DockStyle.Fill;
        _categoryPanel.Margin = new Padding(0);
        _categoryPanel.WrapContents = false;
        _categoryPanel.AutoScroll = true;
        _categoryPanel.BackColor = _windowBackground;
        return _categoryPanel;
    }

    private Control BuildShortcutRow()
    {
        _shortcutPanel.Dock = DockStyle.Fill;
        _shortcutPanel.Margin = new Padding(0);
        _shortcutPanel.WrapContents = false;
        _shortcutPanel.AutoScroll = true;
        _shortcutPanel.BackColor = _windowBackground;
        return _shortcutPanel;
    }

    private Control BuildTemplateRow()
    {
        return BuildScrollableSection(_templateViewport, _templateContent, _templateScrollBar, TemplateViewport_MouseWheel, 2);
    }

    private Control BuildConstructionRow()
    {
        var host = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            Margin = new Padding(0)
        };
        host.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        host.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 22));
        host.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 52));

        host.Controls.Add(BuildScrollableSection(_constructionViewport, _constructionContent, _constructionScrollBar, ConstructionViewport_MouseWheel, 2), 0, 0);

        var sideButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = _windowBackground,
            Margin = new Padding(4, 2, 0, 0)
        };
        sideButtons.Controls.Add(CreateActionButton("INS", InsertButton_Click, width: 44));
        sideButtons.Controls.Add(CreateActionButton("DEL", DeleteButton_Click, width: 44));
        host.Controls.Add(sideButtons, 2, 0);

        return host;
    }

    private Control BuildResponseRow()
    {
        _responsePanel.Dock = DockStyle.Fill;
        _responsePanel.BackColor = Color.White;
        _responsePanel.Margin = new Padding(0, 4, 0, 0);
        _responsePanel.Padding = new Padding(4);
        _responsePanel.Paint += BorderPanel_Paint;

        _responseLabel.Dock = DockStyle.Fill;
        _responseLabel.BackColor = Color.White;
        _responseLabel.ForeColor = _interactiveText;
        _responseLabel.Font = _monoFont;
        _responseLabel.TextAlign = ContentAlignment.MiddleLeft;

        _responsePanel.Controls.Add(_responseLabel);
        return _responsePanel;
    }

    private Control BuildAutoResponseRow()
    {
        return BuildScrollableSection(_autoResponseViewport, _autoResponseContent, _autoResponseScrollBar, AutoResponseViewport_MouseWheel, 2);
    }

    private Control BuildDownlinkRow()
    {
        return BuildScrollableSection(_downlinkViewport, _downlinkContent, _downlinkScrollBar, DownlinkViewport_MouseWheel, 2);
    }

    private Control BuildActionRow()
    {
        return new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = _windowBackground,
            Margin = new Padding(0, 6, 0, 0)
        };
    }

    private Control BuildScrollableSection(Panel viewport, FlowLayoutPanel content, VATSYSControls.ScrollBar scrollBar,
        MouseEventHandler mouseWheelHandler, int topMargin)
    {
        var host = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Margin = new Padding(0, topMargin, 0, 0)
        };
        host.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        host.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 22));

        viewport.Dock = DockStyle.Fill;
        viewport.Margin = new Padding(0);
        viewport.Padding = new Padding(2);
        viewport.BackColor = Color.White;
        viewport.Paint += BorderPanel_Paint;
        viewport.Resize += Viewport_Resize;
        viewport.MouseWheel += mouseWheelHandler;
        viewport.MouseEnter += (_, _) => viewport.Focus();

        content.AutoSize = true;
        content.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        content.FlowDirection = FlowDirection.TopDown;
        content.WrapContents = false;
        content.BackColor = Color.White;
        content.Margin = new Padding(0);
        content.Padding = new Padding(0);
        content.Location = new Point(2, 2);
        viewport.Controls.Add(content);

        scrollBar.Dock = DockStyle.Fill;
        scrollBar.ActualHeight = 8;
        scrollBar.PreferredHeight = 8;
        scrollBar.Change = 4;
        scrollBar.MinimumSize = new Size(0, -4);
        scrollBar.Orientation = ScrollOrientation.VerticalScroll;
        scrollBar.Visible = false;
        scrollBar.Scroll += (_, _) => { if (!_syncingScrollbars) ApplyScrollPosition(viewport, content, scrollBar); };
        scrollBar.Scrolling += (_, _) => { if (!_syncingScrollbars) ApplyScrollPosition(viewport, content, scrollBar); };

        host.Controls.Add(viewport, 0, 0);
        host.Controls.Add(scrollBar, 1, 0);
        return host;
    }

    private GenericButton CreateActionButton(string text, EventHandler? clickHandler, bool enabled = true, int width = DefaultActionButtonWidth)
    {
        var button = new GenericButton
        {
            Text = text,
            Width = width,
            Height = ActionButtonHeight,
            Enabled = enabled,
            Font = _monoFont,
            BackColor = _windowBackground,
            ForeColor = _interactiveText,
            Margin = new Padding(1, 0, 1, 0),
            Padding = new Padding(2),
            SubText = string.Empty,
            SubFont = new Font("Microsoft Sans Serif", 8.25f, FontStyle.Regular, GraphicsUnit.Point)
        };
        if (clickHandler != null)
            button.Click += clickHandler;
        return button;
    }

    private MenuButton CreateTabButton(string text, EventHandler onClick)
    {
        var button = new MenuButton
        {
            Text = text,
            Width = Math.Max(56, TextRenderer.MeasureText(text, _monoFont).Width + 18),
            Height = 22,
            Margin = new Padding(1, 0, 1, 0),
            Font = _monoFont,
            BackColor = _windowBackground,
            ForeColor = _interactiveText
        };
        button.Click += onClick;
        return button;
    }

    private void BuildCategoryTabs()
    {
        _categoryPanel.Controls.Clear();
        _categoryButtons.Clear();

        foreach (var catName in ClearanceViewModel.CategoryNames)
        {
            var button = CreateTabButton(catName, CategoryButton_Click);
            _categoryButtons[button] = catName;
            _categoryPanel.Controls.Add(button);
        }
    }

    private void RefreshShortcuts()
    {
        _shortcutPanel.Controls.Clear();
        _shortcutButtons.Clear();
        _selectedShortcutButton = null;

        foreach (var subCategory in _vm.SubCategories)
        {
            var button = CreateTabButton(subCategory, ShortcutButton_Click);
            _shortcutButtons[button] = subCategory;
            _shortcutPanel.Controls.Add(button);
        }

        if (_shortcutButtons.Count > 0)
            SelectShortcutButton(_shortcutButtons.Keys.First());
    }

    private void RefreshTemplates()
    {
        _currentTemplates = _vm.VisibleTemplates.ToList();
        _templateContent.SuspendLayout();
        _templateContent.Controls.Clear();

        for (var index = 0; index < _currentTemplates.Count; index++)
            _templateContent.Controls.Add(BuildTemplateControl(_templateViewport, _currentTemplates[index], index));

        _templateContent.ResumeLayout(true);
        UpdateScrollbars();
    }

    private void RefreshConstruction()
    {
        _constructionContent.SuspendLayout();
        _constructionContent.Controls.Clear();

        for (var index = 0; index < _vm.ConstructionLines.Count; index++)
            _constructionContent.Controls.Add(BuildConstructionControl(_constructionViewport, _vm.ConstructionLines[index], index));

        _constructionContent.ResumeLayout(true);
        UpdateScrollbars();
    }

    private void RefreshAutoResponses()
    {
        _autoResponseContent.SuspendLayout();
        _autoResponseContent.Controls.Clear();

        var templates = _vm.AutomatedResponseTemplates;
        for (int i = 0; i < templates.Count; i++)
            _autoResponseContent.Controls.Add(BuildAutoResponseControl(_autoResponseViewport, templates[i], i));

        _autoResponseContent.ResumeLayout(true);

        bool show = templates.Count > 0;
        if (_root != null)
        {
            _root.RowStyles[7] = new RowStyle(SizeType.Absolute, show ? 100 : 0);
            Size = new Size(760, show ? 520 : 420);
        }
        if (_autoResponseRowHost != null)
            _autoResponseRowHost.Visible = show;

        UpdateScrollbars();
    }

    private Control BuildAutoResponseControl(Control viewport, ClearanceViewModel.TemplateDisplayItem item, int index)
    {
        var row = CreateWrappingRow(viewport, Color.White);

        row.Controls.Add(CreateRowLabel(item.MessageId.ToString(), 32));

        foreach (var segment in item.Segments)
        {
            if (segment.IsParameter)
            {
                row.Controls.Add(CreateParameterLabel($"({segment.ParameterName})"));
                var field = CreateInlineField(segment.Text);
                field.TextChanged += (_, _) =>
                {
                    var match = item.Segments.FirstOrDefault(x => x.IsParameter && x.ParameterName == segment.ParameterName);
                    if (match != null) match.Text = field.Text;
                };
                row.Controls.Add(field);
            }
            else
            {
                row.Controls.Add(CreateTextSegmentLabel(segment.Text));
            }
        }

        var eosButton = CreateTabButton("EOS", (_, _) =>
        {
            try
            {
                _vm.AddTemplateToConstruction(item);
                RefreshConstruction();
                RefreshResponse();
            }
            catch (Exception ex)
            {
                Errors.Add(new Exception($"ClearanceWindow.AutoResponseEOS_Click: {ex.Message}", ex));
            }
        });
        eosButton.Width = 44;
        row.Controls.Add(eosButton);
        return row;
    }

    private void AutoResponseViewport_MouseWheel(object? sender, MouseEventArgs e)
    {
        NudgeScrollBar(_autoResponseScrollBar, e.Delta);
    }

    private void RefreshDownlinks()
    {
        _downlinkContent.SuspendLayout();
        _downlinkContent.Controls.Clear();

        foreach (var downlink in _vm.OpenDownlinks)
            _downlinkContent.Controls.Add(BuildDownlinkControl(_downlinkViewport, downlink.Content));

        _downlinkContent.ResumeLayout(true);
        _downlinkViewport.Parent!.Visible = _vm.OpenDownlinks.Count > 0;
        UpdateScrollbars();
    }

    private void RefreshResponse()
    {
        _responsePanel.Visible = !string.IsNullOrWhiteSpace(_vm.ResponseText);
        _responseLabel.Text = _vm.ResponseText;

        if (_vm.ResponseText == "No Procedural Conflicts Found")
            _responseLabel.ForeColor = _okColor;
        else if (_vm.ConflictDetected)
            _responseLabel.ForeColor = _conflictColor;
        else
            _responseLabel.ForeColor = _interactiveText;

        _overrideButton.Enabled = _vm.ConflictDetected && !_vm.OverrideActive;
        _probeButton.Enabled = _vm.ConstructionLines.Count > 0 && !_vm.HasActiveProbeState;
        _cancelButton.Enabled = _vm.HasActiveProbeState;
        _sendButton.Enabled = _vm.ConstructionLines.Count > 0 && (!_vm.ConflictDetected || _vm.OverrideActive);
        _vhfButton.Enabled = _vm.ConstructionLines.Count > 0;
        _unableButton.Enabled = _vm.IsReplyMode;
    }

    private Control BuildTemplateControl(Control viewport, ClearanceViewModel.TemplateDisplayItem item, int index)
    {
        var row = CreateWrappingRow(viewport, index == _vm.SelectedTemplateIndex ? _selectionBackground : Color.White);
        row.Tag = index;

        row.Controls.Add(CreateRowLabel(index == _vm.SelectedTemplateIndex ? "->" : "  ", 20));
        row.Controls.Add(CreateRowLabel(item.MessageId.ToString(), 32));

        foreach (var segment in item.Segments)
        {
            if (segment.IsParameter)
            {
                row.Controls.Add(CreateParameterLabel($"({segment.ParameterName})"));
                var field = CreateInlineField(segment.Text);
                field.TextChanged += (_, _) =>
                {
                    if (index >= _currentTemplates.Count)
                        return;

                    var match = _currentTemplates[index].Segments
                        .FirstOrDefault(x => x.IsParameter && x.ParameterName == segment.ParameterName);
                    if (match != null)
                        match.Text = field.Text;
                };
                row.Controls.Add(field);
            }
            else
            {
                row.Controls.Add(CreateTextSegmentLabel(segment.Text));
            }
        }

        var eosButton = CreateTabButton("EOS", (_, _) =>
        {
            try
            {
                if (index >= _currentTemplates.Count)
                    return;

                _vm.AddTemplateToConstruction(_currentTemplates[index]);
                RefreshConstruction();
                RefreshResponse();
            }
            catch (Exception ex)
            {
                Errors.Add(new Exception($"ClearanceWindow.EOS_Click: {ex.Message}", ex));
            }
        });
        eosButton.Width = 44;
        row.Controls.Add(eosButton);

        WireSelectionHandlers(row, () =>
        {
            try
            {
                _vm.SelectedTemplateIndex = index;
                RefreshTemplates();
            }
            catch (Exception ex)
            {
                Errors.Add(new Exception($"ClearanceWindow.TemplateRow_Click: {ex.Message}", ex));
            }
        }, () =>
        {
            try
            {
                if (index >= _currentTemplates.Count)
                    return;

                _vm.AddTemplateToConstruction(_currentTemplates[index]);
                RefreshConstruction();
                RefreshResponse();
            }
            catch (Exception ex)
            {
                Errors.Add(new Exception($"ClearanceWindow.TemplateRow_DoubleClick: {ex.Message}", ex));
            }
        });

        return row;
    }

    private Control BuildConstructionControl(Control viewport, ClearanceViewModel.ConstructionLine line, int index)
    {
        var row = CreateWrappingRow(viewport, index == _vm.SelectedConstructionIndex ? _selectionBackground : Color.White);
        row.Tag = index;
        row.Controls.Add(CreateRowLabel($"({line.Template.Id})", 42));

        foreach (var segment in line.Segments)
        {
            if (segment.IsParameter)
            {
                row.Controls.Add(CreateParameterLabel($"({segment.ParameterName})"));
                var field = CreateInlineField(segment.Text);
                field.TextChanged += (_, _) => _vm.UpdateConstructionParameter(index, segment.ParameterName, field.Text);
                row.Controls.Add(field);
            }
            else
            {
                row.Controls.Add(CreateTextSegmentLabel(segment.Text));
            }
        }

        WireSelectionHandlers(row, () =>
        {
            try
            {
                _vm.SelectedConstructionIndex = index;
                RefreshConstruction();
            }
            catch (Exception ex)
            {
                Errors.Add(new Exception($"ClearanceWindow.ConstructionRow_Click: {ex.Message}", ex));
            }
        });

        return row;
    }

    private Control BuildDownlinkControl(Control viewport, string content)
    {
        var row = CreateWrappingRow(viewport, Color.White);
        row.Controls.Add(new Label
        {
            AutoSize = true,
            Font = _monoFont,
            ForeColor = _downlinkColor,
            BackColor = Color.White,
            Text = "DL : ",
            Margin = new Padding(0, 3, 0, 0)
        });
        row.Controls.Add(new Label
        {
            AutoSize = true,
            Font = _monoFont,
            ForeColor = _interactiveText,
            BackColor = Color.White,
            Text = content,
            Margin = new Padding(0, 3, 0, 0)
        });
        return row;
    }

    private FlowLayoutPanel CreateWrappingRow(Control viewport, Color background)
    {
        return new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            BackColor = background,
            Margin = new Padding(0),
            Padding = new Padding(3, 2, 3, 2),
            Width = Math.Max(120, viewport.ClientSize.Width - 8)
        };
    }

    private Label CreateRowLabel(string text, int width)
    {
        return new Label
        {
            AutoSize = false,
            Width = width,
            Height = 20,
            Text = text,
            Font = _monoFont,
            ForeColor = _interactiveText,
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 1, 4, 0)
        };
    }

    private Label CreateParameterLabel(string text)
    {
        return new Label
        {
            AutoSize = true,
            Text = text,
            Font = _monoFont,
            ForeColor = _nonInteractiveText,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 3, 2, 0)
        };
    }

    private Label CreateTextSegmentLabel(string text)
    {
        return new Label
        {
            AutoSize = true,
            Text = text,
            Font = _monoFont,
            ForeColor = _interactiveText,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 3, 0, 0)
        };
    }

    private TextField CreateInlineField(string value)
    {
        var width = Math.Max(42, Math.Min(120,
            TextRenderer.MeasureText(string.IsNullOrWhiteSpace(value) ? "MMMM" : value + "MM", _monoFont).Width));

        return new TextField
        {
            Text = value,
            Width = width,
            Height = 22,
            Font = _monoFont,
            BackColor = Color.White,
            ForeColor = _interactiveText,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(0, 0, 4, 0),
            NumericCharOnly = false,
            OctalOnly = false
        };
    }

    private void WireSelectionHandlers(Control parent, Action onClick, Action? onDoubleClick = null)
    {
        parent.Click += (_, _) => onClick();
        if (onDoubleClick != null)
            parent.DoubleClick += (_, _) => onDoubleClick();

        foreach (Control child in parent.Controls)
        {
            // Skip interactive input controls — clicking them should give them focus,
            // not trigger row selection which would rebuild the control and lose the caret.
            if (child is TextField || child is GenericButton || child is MenuButton || child is Button)
                continue;
            WireSelectionHandlers(child, onClick, onDoubleClick);
        }
    }

    private void CategoryButton_Click(object? sender, EventArgs e)
    {
        try
        {
            if (sender is not MenuButton button || !_categoryButtons.TryGetValue(button, out var category))
                return;

            SelectCategoryButton(button);
            ShowCategoryDropdown(button, category);
        }
        catch (Exception ex)
        {
            Errors.Add(new Exception($"ClearanceWindow.CategoryButton_Click: {ex.Message}", ex));
        }
    }

    private void ShortcutButton_Click(object? sender, EventArgs e)
    {
        try
        {
            if (sender is not MenuButton button)
                return;

            SelectShortcutButton(button);
        }
        catch (Exception ex)
        {
            Errors.Add(new Exception($"ClearanceWindow.ShortcutButton_Click: {ex.Message}", ex));
        }
    }

    private void SelectCategoryButton(MenuButton button)
    {
        if (_selectedCategoryButton != null && _selectedCategoryButton != button)
            _selectedCategoryButton.Depressed = false;

        _selectedCategoryButton = button;
        _selectedCategoryButton.Depressed = true;
        _vm.SelectedCategory = _categoryButtons[button];
        // RefreshShortcuts and RefreshTemplates are triggered via Vm_PropertyChanged
        // (SubCategories → RefreshShortcuts → SelectShortcutButton → VisibleTemplates → RefreshTemplates)
    }

    private void SelectShortcutButton(MenuButton button)
    {
        if (_selectedShortcutButton != null && _selectedShortcutButton != button)
            _selectedShortcutButton.Depressed = false;

        _selectedShortcutButton = button;
        _selectedShortcutButton.Depressed = true;
        _vm.SelectedSubCategory = _shortcutButtons[button];
        // RefreshTemplates is triggered via Vm_PropertyChanged (VisibleTemplates)
    }

    private void ShowCategoryDropdown(Control button, string category)
    {
        try
        {
        // Dispose any previously closed menu now that we're safely outside Win32's
        // menu-close message sequence (avoids ObjectDisposedException mid-teardown).
        _activeMenu?.Dispose();
        _activeMenu = null;

        var grouped = _vm.GetGroupedTemplates(category);
        if (grouped.Count == 0)
            return;

        var menu = new ContextMenuStrip { Font = _monoFont };
        _activeMenu = menu;

        foreach (var (groupName, templates) in grouped)
        {
            if (groupName == null)
            {
                // Direct items — add flat to the menu
                foreach (var template in templates)
                    menu.Items.Add(CreateTemplateMenuItem(template));
            }
            else
            {
                // Named sub-group — appears as a sub-menu
                var groupItem = new ToolStripMenuItem(groupName) { Font = _monoFont };
                foreach (var template in templates)
                    groupItem.DropDownItems.Add(CreateTemplateMenuItem(template));
                menu.Items.Add(groupItem);
            }
        }

        menu.Show(button.PointToScreen(new Point(0, button.Height)));
        }
        catch (Exception ex)
        {
            Errors.Add(new Exception($"ClearanceWindow.ShowCategoryDropdown: {ex.Message}", ex));
        }
    }

    private ToolStripMenuItem CreateTemplateMenuItem(ClearanceViewModel.TemplateDisplayItem template)
    {
        var item = new ToolStripMenuItem($"{template.MessageId}  {template.Template.Template}") { Font = _monoFont };
        item.Click += (_, _) =>
        {
            try
            {
                _vm.AddTemplateToConstruction(template);
                RefreshConstruction();
                RefreshResponse();
            }
            catch (Exception ex)
            {
                Errors.Add(new Exception($"ClearanceWindow.MenuItem_Click: {ex.Message}", ex));
            }
        };
        return item;
    }

    private void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() =>
            {
                try { Vm_PropertyChanged(sender, e); }
                catch (Exception ex) { Errors.Add(new Exception($"ClearanceWindow.Vm_PropertyChanged(invoke): {ex.Message}", ex)); }
            }));
            return;
        }

        try
        {
            switch (e.PropertyName)
            {
                case nameof(ClearanceViewModel.Callsign):
                    _callsignLabel.Text = (_vm.Callsign ?? string.Empty).ToUpperInvariant();
                    break;
                case nameof(ClearanceViewModel.Route):
                    _routeLabel.Text = _vm.Route;
                    break;
                case nameof(ClearanceViewModel.ConstructionLines):
                    RefreshConstruction();
                    break;
                case nameof(ClearanceViewModel.SubCategories):
                    RefreshShortcuts();
                    break;
                case nameof(ClearanceViewModel.VisibleTemplates):
                    RefreshTemplates();
                    break;
                case nameof(ClearanceViewModel.ResponseText):
                case nameof(ClearanceViewModel.ConflictDetected):
                case nameof(ClearanceViewModel.OverrideActive):
                case nameof(ClearanceViewModel.IsProbed):
                    RefreshResponse();
                    break;
                case nameof(ClearanceViewModel.AutomatedResponseTemplates):
                    RefreshAutoResponses();
                    break;
            }
        }
        catch (Exception ex)
        {
            Errors.Add(new Exception($"ClearanceWindow.Vm_PropertyChanged({e.PropertyName}): {ex.Message}", ex));
        }
    }

    private void OnSharedProbeStateChanged(string callsign)
    {
        if (!string.Equals(callsign, _vm.Callsign, StringComparison.OrdinalIgnoreCase))
            return;

        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => OnSharedProbeStateChanged(callsign)));
            return;
        }

        RefreshResponse();
    }

    private void ProbeButton_Click(object? sender, EventArgs e)
    {
        try
        {
            _vm.ExecuteProbe();
            var fdr = FDP2.GetFDRs.FirstOrDefault(f =>
                string.Equals(f.Callsign, _vm.Callsign, StringComparison.OrdinalIgnoreCase));
            if (fdr != null)
                ProbeRouteRenderer.ShowForTrack(MMI.FindTrack(fdr));
            RefreshResponse();
        }
        catch (Exception ex)
        {
            Errors.Add(new Exception($"ClearanceWindow.ProbeButton_Click: {ex.Message}", ex));
        }
    }

    private void CancelButton_Click(object? sender, EventArgs e)
    {
        try
        {
            _vm.ExecuteCancel();
            ProbeRouteRenderer.HideForCallsign(_vm.Callsign);
            RefreshConstruction();
            RefreshResponse();
        }
        catch (Exception ex)
        {
            Errors.Add(new Exception($"ClearanceWindow.CancelButton_Click: {ex.Message}", ex));
        }
    }

    private void SendButton_Click(object? sender, EventArgs e)
    {
        try
        {
            if (_sendButton.Text == "HF")
                _vm.ExecuteSendHf();
            else
                _vm.ExecuteSend();
            ProbeRouteRenderer.HideForCallsign(_vm.Callsign);
            RefreshConstruction();
            RefreshResponse();
        }
        catch (Exception ex)
        {
            Errors.Add(new Exception($"ClearanceWindow.SendButton_Click: {ex.Message}", ex));
        }
    }

    private void SendButton_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right)
            _sendButton.Text = _sendButton.Text == "SND" ? "HF" : "SND";
    }

    private void UnableButton_Click(object? sender, EventArgs e)
    {
        try
        {
            _vm.ExecuteUnable();
            RefreshConstruction();
            RefreshResponse();
        }
        catch (Exception ex)
        {
            Errors.Add(new Exception($"ClearanceWindow.UnableButton_Click: {ex.Message}", ex));
        }
    }

    private void OverrideButton_Click(object? sender, EventArgs e)
    {
        try
        {
            _vm.ExecuteOverride();
            RefreshResponse();
        }
        catch (Exception ex)
        {
            Errors.Add(new Exception($"ClearanceWindow.OverrideButton_Click: {ex.Message}", ex));
        }
    }

    private void VhfButton_Click(object? sender, EventArgs e)
    {
        try
        {
            _vm.ExecuteVhf();
            var fdr = FDP2.GetFDRs.FirstOrDefault(f =>
                string.Equals(f.Callsign, _vm.Callsign, StringComparison.OrdinalIgnoreCase));
            if (fdr != null)
                ProbeRouteRenderer.ShowForTrack(MMI.FindTrack(fdr));
            RefreshResponse();
        }
        catch (Exception ex)
        {
            Errors.Add(new Exception($"ClearanceWindow.VhfButton_Click: {ex.Message}", ex));
        }
    }

    private void InsertButton_Click(object? sender, EventArgs e)
    {
        try
        {
            if (_vm.SelectedTemplateIndex < 0 || _vm.SelectedTemplateIndex >= _currentTemplates.Count)
                return;

            _vm.InsertTemplateAtSelection(_currentTemplates[_vm.SelectedTemplateIndex]);
            RefreshConstruction();
            RefreshResponse();
        }
        catch (Exception ex)
        {
            Errors.Add(new Exception($"ClearanceWindow.InsertButton_Click: {ex.Message}", ex));
        }
    }

    private void DeleteButton_Click(object? sender, EventArgs e)
    {
        try
        {
            _vm.DeleteSelectedConstructionLine();
            RefreshConstruction();
            RefreshResponse();
        }
        catch (Exception ex)
        {
            Errors.Add(new Exception($"ClearanceWindow.DeleteButton_Click: {ex.Message}", ex));
        }
    }

    private void CoordButton_Click(object? sender, EventArgs e)
    {
        try
        {
            var fdr = vatsys.FDP2.GetFDRs.FirstOrDefault(f =>
                string.Equals(f.Callsign, _vm.Callsign, StringComparison.OrdinalIgnoreCase));
            if (fdr != null)
                MMI.OpenFPWindow(fdr);
        }
        catch
        {
        }
    }

    private void CloseButton_Click(object? sender, EventArgs e)
    {
        Hide();
    }

    private void BorderPanel_Paint(object? sender, PaintEventArgs e)
    {
        if (sender is not Panel panel)
            return;

        var bounds = panel.ClientRectangle;
        bounds.Width -= 1;
        bounds.Height -= 1;
        using var pen = new Pen(Color.Black);
        e.Graphics.DrawRectangle(pen, bounds);
    }

    private void Viewport_Resize(object? sender, EventArgs e)
    {
        UpdateScrollbars();
    }

    private void TemplateViewport_MouseWheel(object? sender, MouseEventArgs e)
    {
        NudgeScrollBar(_templateScrollBar, e.Delta);
    }

    private void ConstructionViewport_MouseWheel(object? sender, MouseEventArgs e)
    {
        NudgeScrollBar(_constructionScrollBar, e.Delta);
    }

    private void DownlinkViewport_MouseWheel(object? sender, MouseEventArgs e)
    {
        NudgeScrollBar(_downlinkScrollBar, e.Delta);
    }

    private void NudgeScrollBar(VATSYSControls.ScrollBar bar, int delta)
    {
        if (_syncingScrollbars) return;
        var next = bar.Value + (delta < 0 ? bar.Change : -bar.Change);
        bar.Value = Math.Max(0, Math.Min(100, next));
        if (bar == _templateScrollBar)
            ApplyScrollPosition(_templateViewport, _templateContent, _templateScrollBar);
        else if (bar == _constructionScrollBar)
            ApplyScrollPosition(_constructionViewport, _constructionContent, _constructionScrollBar);
        else if (bar == _downlinkScrollBar)
            ApplyScrollPosition(_downlinkViewport, _downlinkContent, _downlinkScrollBar);
        else if (bar == _autoResponseScrollBar)
            ApplyScrollPosition(_autoResponseViewport, _autoResponseContent, _autoResponseScrollBar);
    }

    private void UpdateScrollbars()
    {
        UpdateScrollSection(_templateViewport, _templateContent, _templateScrollBar);
        UpdateScrollSection(_constructionViewport, _constructionContent, _constructionScrollBar);
        UpdateScrollSection(_downlinkViewport, _downlinkContent, _downlinkScrollBar);
        UpdateScrollSection(_autoResponseViewport, _autoResponseContent, _autoResponseScrollBar);
    }

    private void UpdateScrollSection(Panel viewport, FlowLayoutPanel content, VATSYSControls.ScrollBar scrollBar)
    {
        if (viewport.Width <= 0)
            return;

        foreach (Control child in content.Controls)
            child.Width = Math.Max(120, viewport.ClientSize.Width - 8);

        var contentHeight = content.PreferredSize.Height + 4;
        var viewportHeight = viewport.ClientSize.Height;
        var maxOffset = Math.Max(0, contentHeight - viewportHeight);

        _syncingScrollbars = true;
        scrollBar.Visible = maxOffset > 0;
        if (maxOffset == 0)
        {
            scrollBar.Value = 0;
            content.Location = new Point(2, 2);
        }
        else
        {
            var currentOffset = Math.Max(0, -content.Top + 2);
            var percent = Math.Max(0, Math.Min(100, (int)Math.Round(currentOffset * 100d / maxOffset)));
            scrollBar.Value = percent;
            ApplyScrollPosition(viewport, content, scrollBar);
        }
        _syncingScrollbars = false;
    }

    private static void ApplyScrollPosition(Panel viewport, Control content, VATSYSControls.ScrollBar scrollBar)
    {
        var contentHeight = content.PreferredSize.Height + 4;
        var viewportHeight = viewport.ClientSize.Height;
        var maxOffset = Math.Max(0, contentHeight - viewportHeight);
        var offset = maxOffset == 0 ? 0 : (int)Math.Round(maxOffset * (scrollBar.Value / 100d));
        content.Location = new Point(2, 2 - offset);
    }
}