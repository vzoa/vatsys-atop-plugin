using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using vatsys;

namespace AtopPlugin.UI.Wpf;

public partial class ClearanceWindow : Window
{
    private static readonly FontFamily MonoFont = ResolveVatSysFont();
    private static readonly SolidColorBrush ParamLabelBrush = Brushes.Gray;
    private static readonly SolidColorBrush DownlinkBrush =
        new(Color.FromRgb(0xFF, 0x66, 0x00));
    private static readonly SolidColorBrush SelectionBrush =
        new(Color.FromRgb(0xD0, 0xD0, 0xFF));

    private readonly ClearanceViewModel _vm;
    private ToggleButton? _selectedCategoryButton;
    private ToggleButton? _selectedShortcutButton;
    private List<ClearanceViewModel.TemplateDisplayItem> _currentTemplates = new();

    private static FontFamily ResolveVatSysFont()
    {
        try
        {
            var name = MMI.eurofont_xsml?.FontFamily?.Name;
            if (!string.IsNullOrEmpty(name))
                return new FontFamily(name);
        }
        catch { }
        return new FontFamily("Consolas");
    }

    public ClearanceWindow()
    {
        InitializeComponent();
        FontFamily = MonoFont;
        _vm = new ClearanceViewModel();
        _vm.RequestClose += () => Hide();
        _vm.PropertyChanged += Vm_PropertyChanged;
        BuildCategoryTabs();
    }

    public void ShowForCallsign(string callsign)
    {
        _vm.Load(callsign);
        CallsignText.Text = _vm.Callsign;
        RouteText.Text = _vm.Route;

        RefreshDownlinks();
        RefreshShortcuts();
        RefreshTemplates();
        RefreshConstruction();
        RefreshResponse();

        var matchTab = CategoryPanel.Children.OfType<ToggleButton>()
            .FirstOrDefault(tb => tb.Content as string == _vm.SelectedCategory);
        if (matchTab != null)
            SelectCategoryButton(matchTab);

        Show();
        Activate();
    }

    // -------------------------------------------------------------------------
    // Title bar
    // -------------------------------------------------------------------------
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1) DragMove();
    }

    // -------------------------------------------------------------------------
    // Category tabs
    // -------------------------------------------------------------------------
    private void BuildCategoryTabs()
    {
        CategoryPanel.Children.Clear();
        foreach (var catName in ClearanceViewModel.CategoryNames)
        {
            var tb = new ToggleButton
            {
                Content = catName,
                Style = FindResource("MotifTab") as Style,
                Margin = new Thickness(1, 0, 1, 0),
                Tag = catName
            };
            tb.Click += CategoryTab_Click;
            CategoryPanel.Children.Add(tb);
        }
    }

    private void CategoryTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton tb)
        {
            SelectCategoryButton(tb);
            ShowCategoryDropdown(tb);
        }
    }

    private void SelectCategoryButton(ToggleButton button)
    {
        if (_selectedCategoryButton != null && _selectedCategoryButton != button)
            _selectedCategoryButton.IsChecked = false;

        button.IsChecked = true;
        _selectedCategoryButton = button;
        _vm.SelectedCategory = button.Tag as string ?? "Vert";

        RefreshShortcuts();
        RefreshTemplates();
    }

    // -------------------------------------------------------------------------
    // Category dropdown menu
    // -------------------------------------------------------------------------
    private void ShowCategoryDropdown(ToggleButton button)
    {
        var catName = button.Tag as string;
        if (catName == null) return;

        var grouped = _vm.GetGroupedTemplates(catName);
        if (grouped.Count == 0) return;

        var menu = new ContextMenu
        {
            FontFamily = MonoFont,
            FontSize = 11
        };

        foreach (var (groupName, templates) in grouped)
        {
            if (grouped.Count == 1)
            {
                // Single group — flat list
                foreach (var tmpl in templates)
                {
                    var mi = new MenuItem
                    {
                        Header = $"{tmpl.MessageId}  {tmpl.Template.Template}",
                        Tag = tmpl
                    };
                    mi.Click += DropdownMessage_Click;
                    menu.Items.Add(mi);
                }
            }
            else
            {
                // Sub-menu per group
                var groupItem = new MenuItem
                {
                    Header = groupName,
                    FontWeight = FontWeights.Bold
                };
                foreach (var tmpl in templates)
                {
                    var mi = new MenuItem
                    {
                        Header = $"{tmpl.MessageId}  {tmpl.Template.Template}",
                        Tag = tmpl,
                        FontWeight = FontWeights.Normal
                    };
                    mi.Click += DropdownMessage_Click;
                    groupItem.Items.Add(mi);
                }
                menu.Items.Add(groupItem);
            }
        }

        menu.PlacementTarget = button;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private void DropdownMessage_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.Tag is ClearanceViewModel.TemplateDisplayItem tmpl)
        {
            _vm.AddTemplateToConstruction(tmpl);
            RefreshConstruction();
            RefreshResponse();
        }
    }

    // -------------------------------------------------------------------------
    // Shortcut bar (sub-category group tabs)
    // -------------------------------------------------------------------------
    private void RefreshShortcuts()
    {
        SubCategoryPanel.Children.Clear();
        _selectedShortcutButton = null;

        foreach (var sub in _vm.SubCategories)
        {
            var tb = new ToggleButton
            {
                Content = sub,
                Style = FindResource("MotifTab") as Style,
                FontSize = 10,
                Margin = new Thickness(1, 0, 1, 0),
                Tag = sub
            };
            tb.Click += Shortcut_Click;
            SubCategoryPanel.Children.Add(tb);
        }

        if (SubCategoryPanel.Children.Count > 0)
        {
            var first = SubCategoryPanel.Children[0] as ToggleButton;
            if (first != null) SelectShortcutButton(first);
        }
    }

    private void Shortcut_Click(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton tb) SelectShortcutButton(tb);
    }

    private void SelectShortcutButton(ToggleButton button)
    {
        if (_selectedShortcutButton != null && _selectedShortcutButton != button)
            _selectedShortcutButton.IsChecked = false;

        button.IsChecked = true;
        _selectedShortcutButton = button;
        _vm.SelectedSubCategory = button.Tag as string;

        RefreshTemplates();
    }

    // -------------------------------------------------------------------------
    // Template list — inline parameter fields + EOS markers
    // -------------------------------------------------------------------------
    private void RefreshTemplates()
    {
        TemplatePanel.Children.Clear();
        _currentTemplates = _vm.VisibleTemplates.ToList();

        for (int i = 0; i < _currentTemplates.Count; i++)
            TemplatePanel.Children.Add(BuildTemplateRow(_currentTemplates[i], i));
    }

    private UIElement BuildTemplateRow(ClearanceViewModel.TemplateDisplayItem item, int index)
    {
        var row = new Border
        {
            Padding = new Thickness(2),
            Background = Brushes.Transparent,
            Tag = index
        };

        var sp = new StackPanel { Orientation = Orientation.Horizontal };

        // Arrow indicator → for selected template
        sp.Children.Add(new TextBlock
        {
            Text = index == _vm.SelectedTemplateIndex ? "→" : " ",
            FontFamily = MonoFont, FontSize = 12, Width = 16,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 2, 0)
        });

        // Message ID
        sp.Children.Add(new TextBlock
        {
            Text = item.MessageId.ToString(),
            FontFamily = MonoFont, FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 4, 0)
        });

        // Template segments: text parts + (paramName) label + editable field
        foreach (var seg in item.Segments)
        {
            if (seg.IsParameter)
            {
                sp.Children.Add(new TextBlock
                {
                    Text = $"({seg.ParameterName})",
                    FontFamily = MonoFont, FontSize = 12,
                    Foreground = ParamLabelBrush,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(1, 0, 1, 0)
                });

                var paramName = seg.ParameterName;
                var tmplIndex = index;
                var tb = new TextBox
                {
                    Text = seg.Text,
                    MinWidth = 30,
                    FontFamily = MonoFont, FontSize = 12,
                    CharacterCasing = CharacterCasing.Upper,
                    BorderThickness = new Thickness(1),
                    BorderBrush = Brushes.Black,
                    Background = Brushes.White,
                    Padding = new Thickness(2, 0, 2, 0),
                    Margin = new Thickness(1, 0, 1, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                tb.TextChanged += (_, _) =>
                {
                    if (tmplIndex < _currentTemplates.Count)
                    {
                        var s = _currentTemplates[tmplIndex].Segments
                            .FirstOrDefault(x => x.IsParameter && x.ParameterName == paramName);
                        if (s != null) s.Text = tb.Text;
                    }
                };
                sp.Children.Add(tb);
            }
            else
            {
                sp.Children.Add(new TextBlock
                {
                    Text = seg.Text,
                    FontFamily = MonoFont, FontSize = 12,
                    VerticalAlignment = VerticalAlignment.Center
                });
            }
        }

        // EOS button (clickable — adds template to construction per spec)
        var eosIndex = index;
        var eosButton = new Border
        {
            BorderThickness = new Thickness(1),
            BorderBrush = Brushes.Black,
            Background = Brushes.LightGray,
            Margin = new Thickness(4, 0, 0, 0),
            Padding = new Thickness(3, 0, 3, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Cursor = Cursors.Hand,
            Child = new TextBlock
            {
                Text = "EOS",
                FontFamily = MonoFont, FontSize = 10
            }
        };
        eosButton.MouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;
            if (eosIndex < _currentTemplates.Count)
            {
                _vm.AddTemplateToConstruction(_currentTemplates[eosIndex]);
                RefreshConstruction();
                RefreshResponse();
            }
        };
        sp.Children.Add(eosButton);

        row.Child = sp;
        row.MouseLeftButtonDown += TemplateRow_Click;
        return row;
    }

    private void TemplateRow_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border border || border.Tag is not int index) return;

        _vm.SelectedTemplateIndex = index;
        RefreshTemplateArrows();

        if (e.ClickCount == 2 && index < _currentTemplates.Count)
        {
            _vm.AddTemplateToConstruction(_currentTemplates[index]);
            RefreshConstruction();
        }
    }

    private void RefreshTemplateArrows()
    {
        for (int i = 0; i < TemplatePanel.Children.Count; i++)
        {
            if (TemplatePanel.Children[i] is Border border
                && border.Child is StackPanel sp
                && sp.Children.Count > 0
                && sp.Children[0] is TextBlock arrow)
            {
                arrow.Text = i == _vm.SelectedTemplateIndex ? "→" : " ";
            }
        }
    }

    // -------------------------------------------------------------------------
    // Construction area — (ID)  message text with inline editable fields
    // -------------------------------------------------------------------------
    private void RefreshConstruction()
    {
        ConstructionPanel.Children.Clear();

        for (int i = 0; i < _vm.ConstructionLines.Count; i++)
            ConstructionPanel.Children.Add(BuildConstructionRow(_vm.ConstructionLines[i], i));
    }

    private UIElement BuildConstructionRow(ClearanceViewModel.ConstructionLine line, int index)
    {
        var row = new Border
        {
            Padding = new Thickness(2),
            Background = index == _vm.SelectedConstructionIndex
                ? SelectionBrush : Brushes.Transparent,
            Tag = index
        };

        var sp = new StackPanel { Orientation = Orientation.Horizontal };

        // (ID) prefix
        sp.Children.Add(new TextBlock
        {
            Text = $"({line.Template.Id})",
            FontFamily = MonoFont, FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        });

        // Segments with inline editable fields
        foreach (var seg in line.Segments)
        {
            if (seg.IsParameter)
            {
                sp.Children.Add(new TextBlock
                {
                    Text = $"({seg.ParameterName})",
                    FontFamily = MonoFont, FontSize = 12,
                    Foreground = ParamLabelBrush,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(1, 0, 1, 0)
                });

                var paramName = seg.ParameterName;
                var lineIndex = index;
                var tb = new TextBox
                {
                    Text = seg.Text,
                    MinWidth = 30,
                    FontFamily = MonoFont, FontSize = 12,
                    CharacterCasing = CharacterCasing.Upper,
                    BorderThickness = new Thickness(1),
                    BorderBrush = Brushes.Black,
                    Background = Brushes.White,
                    Padding = new Thickness(2, 0, 2, 0),
                    Margin = new Thickness(1, 0, 1, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                tb.TextChanged += (_, _) =>
                {
                    _vm.UpdateConstructionParameter(lineIndex, paramName, tb.Text);
                };
                sp.Children.Add(tb);
            }
            else
            {
                sp.Children.Add(new TextBlock
                {
                    Text = seg.Text,
                    FontFamily = MonoFont, FontSize = 12,
                    VerticalAlignment = VerticalAlignment.Center
                });
            }
        }

        row.Child = sp;

        row.MouseLeftButtonDown += (_, _) =>
        {
            _vm.SelectedConstructionIndex = index;
            RefreshConstruction();
        };

        return row;
    }

    // -------------------------------------------------------------------------
    // Downlink display (at bottom, per spec)
    // -------------------------------------------------------------------------
    private void RefreshDownlinks()
    {
        DownlinkList.Children.Clear();

        if (_vm.OpenDownlinks.Count == 0)
        {
            DownlinkPanel.Visibility = Visibility.Collapsed;
            return;
        }

        DownlinkPanel.Visibility = Visibility.Visible;

        foreach (var dl in _vm.OpenDownlinks)
        {
            var sp = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 1, 0, 1)
            };
            sp.Children.Add(new TextBlock
            {
                Text = "DL : ",
                FontFamily = MonoFont,
                FontWeight = FontWeights.Bold,
                Foreground = DownlinkBrush
            });
            sp.Children.Add(new TextBlock
            {
                Text = dl.Content,
                FontFamily = MonoFont,
                Foreground = Brushes.Black
            });
            DownlinkList.Children.Add(sp);
        }
    }

    // -------------------------------------------------------------------------
    // Response / feedback display area
    // -------------------------------------------------------------------------
    private void RefreshResponse()
    {
        if (string.IsNullOrEmpty(_vm.ResponseText))
        {
            ResponsePanel.Visibility = Visibility.Collapsed;
        }
        else
        {
            ResponsePanel.Visibility = Visibility.Visible;
            ResponseText.Text = _vm.ResponseText;

            // Color code: green for ATOP no-conflict response, red for conflict, default for info
            if (_vm.ResponseText == "No Procedural Conflicts Found")
                ResponseText.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0x80, 0x00));
            else if (_vm.ConflictDetected)
                ResponseText.Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0x00, 0x00));
            else
                ResponseText.Foreground = Brushes.Black;
        }

        // Enable/disable OVRD button based on state
        OvrdButton.IsEnabled = _vm.ConflictDetected && !_vm.OverrideActive;
        // Enable/disable PRB button
        PrbButton.IsEnabled = _vm.ConstructionLines.Count > 0 && !_vm.IsProbed;
    }

    // -------------------------------------------------------------------------
    // ViewModel property change handler
    // -------------------------------------------------------------------------
    private void Vm_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => Vm_PropertyChanged(sender, e));
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(ClearanceViewModel.ConstructionLines):
                RefreshConstruction();
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
        }
    }

    // -------------------------------------------------------------------------
    // Button handlers
    // -------------------------------------------------------------------------
    private void Send_Click(object sender, RoutedEventArgs e)
    {
        _vm.SendCommand.Execute(null);
        RefreshConstruction();
        RefreshResponse();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        _vm.CancelCommand.Execute(null);
        RefreshConstruction();
        RefreshResponse();
    }

    private void Probe_Click(object sender, RoutedEventArgs e)
    {
        _vm.ProbeCommand.Execute(null);
        RefreshResponse();
    }

    private void Unable_Click(object sender, RoutedEventArgs e)
    {
        _vm.UnableCommand.Execute(null);
        RefreshConstruction();
        RefreshResponse();
    }

    private void Insert_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedTemplateIndex >= 0 && _vm.SelectedTemplateIndex < _currentTemplates.Count)
        {
            _vm.InsertTemplateAtSelection(_currentTemplates[_vm.SelectedTemplateIndex]);
            RefreshConstruction();
            RefreshResponse();
        }
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        _vm.DeleteConstructionLineCommand.Execute(null);
        RefreshConstruction();
        RefreshResponse();
    }

    private void Ovrd_Click(object sender, RoutedEventArgs e)
    {
        _vm.OverrideCommand.Execute(null);
        RefreshResponse();
    }

    private void Coord_Click(object sender, RoutedEventArgs e)
    {
        // Per spec: COORD opens the Coordination window pre-filled for this ACID.
        // vatSys doesn't expose a public coord window API — open the FP window as fallback.
        try
        {
            var fdr = vatsys.FDP2.GetFDRs.FirstOrDefault(f =>
                string.Equals(f.Callsign, _vm.Callsign, System.StringComparison.OrdinalIgnoreCase));
            if (fdr != null)
                MMI.OpenFPWindow(fdr);
        }
        catch { }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Hide();

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }
}
