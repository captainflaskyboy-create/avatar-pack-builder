using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using AvatarPackBuilder.Models;
using AvatarPackBuilder.Services;
using SixLabors.ImageSharp.PixelFormats;
using SharpRgbaImage = SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>;

namespace AvatarPackBuilder.Views;

public sealed class MainWindow : Window
{
    private readonly GridSettings _gridSettings = new();
    private readonly GridSettings _importGridSettings = new();
    private readonly Dictionary<string, SlicedFrame> _assignments = new(StringComparer.OrdinalIgnoreCase);
    private readonly DispatcherTimer _playbackTimer;
    private readonly DispatcherTimer _animateTimer;

    private SharpRgbaImage? _sheetImage;
    private Bitmap? _sheetPreviewBitmap;
    private string? _sheetPath;
    private int? _iconFrameIndex;
    private IReadOnlyList<SlicedFrame> _frames = Array.Empty<SlicedFrame>();
    private SlicedFrame? _selectedFrame;
    private SpriteState _selectedState = SpriteCatalog.States[0];
    private int _playbackIndex;
    private bool _isPlaying;
    private readonly List<int> _animationFrameIndexes = new();
    private int _selectedAnimationEntryIndex = -1;
    private int _animatePlaybackIndex;
    private bool _isAnimatePlaying;

    private readonly NumericUpDown _rowsText = SliceNumber(3, 1);
    private readonly NumericUpDown _columnsText = SliceNumber(3, 1);
    private readonly NumericUpDown _cellWidthText = SliceNumber(512, 1);
    private readonly NumericUpDown _cellHeightText = SliceNumber(512, 1);
    private readonly NumericUpDown _offsetXText = SliceNumber(0);
    private readonly NumericUpDown _offsetYText = SliceNumber(0);
    private readonly NumericUpDown _spacingXText = SliceNumber(0);
    private readonly NumericUpDown _spacingYText = SliceNumber(0);
    private readonly NumericUpDown _importRowsText = SliceNumber(3, 1);
    private readonly NumericUpDown _importColumnsText = SliceNumber(3, 1);
    private readonly NumericUpDown _importCellWidthText = SliceNumber(512, 1);
    private readonly NumericUpDown _importCellHeightText = SliceNumber(512, 1);
    private readonly NumericUpDown _importOffsetXText = SliceNumber(0);
    private readonly NumericUpDown _importOffsetYText = SliceNumber(0);
    private readonly NumericUpDown _importSpacingXText = SliceNumber(0);
    private readonly NumericUpDown _importSpacingYText = SliceNumber(0);
    private readonly TextBox _searchText = new();
    private readonly TextBox _packNameText = new() { Text = "new_avatar_pack" };
    private readonly TextBox _outputRootText = new();
    private readonly TextBox _speedText = new() { Text = "120" };
    private readonly TextBox _animationNameText = new() { Text = "custom_animation" };
    private readonly TextBox _animateSpeedText = new() { Text = "120" };

    private readonly TextBlock _sheetInfoText = new() { Text = "Drop a sprite sheet here or click Import Sheet.", Foreground = Brushes.LightGray, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _sheetFileSizeText = new() { Text = "File size: -", Foreground = Brushes.LightGray, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _importGridStatusText = new() { Foreground = Brushes.Gold, TextWrapping = TextWrapping.Wrap, FontSize = 12 };
    private readonly TextBlock _iconInfoText = new() { Text = "No app icon frame selected.", Foreground = Brushes.LightGray, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _selectedFrameText = new() { Text = "No frame selected.", Foreground = Brushes.LightGray, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _exportStatusText = new() { Foreground = Brushes.Gold, TextWrapping = TextWrapping.Wrap };
    private readonly Image _iconPreviewImage = new() { Width = 64, Height = 64, Stretch = Stretch.Uniform };
    private readonly CheckBox _icon16Check = new() { Content = "16x16", IsChecked = true };
    private readonly CheckBox _icon32Check = new() { Content = "32x32", IsChecked = true };
    private readonly CheckBox _icon48Check = new() { Content = "48x48" };
    private readonly CheckBox _icon64Check = new() { Content = "64x64" };
    private readonly CheckBox _icon128Check = new() { Content = "128x128" };
    private readonly CheckBox _icon256Check = new() { Content = "256x256" };
    private readonly WrapPanel _framePanel = new() { Margin = new Thickness(8) };
    private readonly StackPanel _assignmentOptionsPanel = new() { Spacing = 6 };
    private readonly StackPanel _selectedFrameRolesPanel = new() { Spacing = 6 };
    private readonly ComboBox _stateCombo = new();
    private readonly ComboBox _playbackModeCombo = new();
    private readonly StackPanel _playbackStrip = new() { Orientation = Orientation.Horizontal, Spacing = 4 };
    private readonly Image _playbackImage = new() { Stretch = Stretch.Uniform, MaxWidth = 320, MaxHeight = 300 };
    private readonly TextBlock _playbackInfoText = new() { Text = "No playback frames.", Foreground = Brushes.LightGray };
    private readonly Button _playPauseButton = new() { Content = "Play" };
    private readonly StackPanel _animationAssignedPanel = new() { Spacing = 6 };
    private readonly Image _animatePlaybackImage = new() { Stretch = Stretch.Uniform, MaxWidth = 320, MaxHeight = 280 };
    private readonly TextBlock _animateInfoText = new() { Text = "No animation frames assigned.", Foreground = Brushes.LightGray, TextWrapping = TextWrapping.Wrap };
    private readonly Button _animatePlayPauseButton = new() { Content = "Play" };
    private readonly TabControl _setupTabs = new();
    private readonly TabControl _rightTabs = new();
    private readonly Grid _centerWorkspaceHost = new();
    private readonly SpriteSheetPreviewControl _sheetPreviewControl = new();
    private Control? _importWorkspacePanel;
    private Control? _slicedFramesWorkspacePanel;
    private AnimationPreviewWindow? _popOutPreviewWindow;

    internal event EventHandler? PreviewDataChanged;

    public MainWindow()
    {
        Title = "Avatar Pack Builder";
        Width = 1180;
        Height = 720;
        MinWidth = 960;
        MinHeight = 600;
        Background = Brush("#151515");
        SetPixelArtRendering(_iconPreviewImage);
        SetPixelArtRendering(_playbackImage);
        SetPixelArtRendering(_animatePlaybackImage);

        _outputRootText.Text = FindDefaultExportFolder();

        _playbackTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
        _playbackTimer.Tick += (_, _) => AdvancePlayback(1);
        _animateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
        _animateTimer.Tick += (_, _) => AdvanceAnimationPlayback(1);

        Content = BuildUi();
        SetupEvents();
        RefreshAssignmentOptions();
        RefreshPlayback();
        RefreshAnimatePanel();
        ShowAnimateFrame();

        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    protected override void OnClosed(EventArgs e)
    {
        _playbackTimer.Stop();
        _animateTimer.Stop();
        _popOutPreviewWindow?.Close();
        DisposeFramePreviews();
        _sheetImage?.Dispose();
        _sheetImage = null;
        _sheetPreviewBitmap?.Dispose();
        _sheetPreviewBitmap = null;
        base.OnClosed(e);
    }

    private Control BuildUi()
    {
        var root = new DockPanel();

        var header = new Border
        {
            Background = Brush("#2b2b2b"),
            BorderBrush = Brush("#555555"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(14, 10),
            Child = new DockPanel
            {
                Children =
                {
                    new TextBlock
                    {
                        Text = "Avatar Pack Builder",
                        FontSize = 26,
                        FontWeight = FontWeight.Bold,
                        Foreground = Brushes.White,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                }
            }
        };
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        var mainGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("320,*,6,360"),
            RowDefinitions = new RowDefinitions("*"),
            Margin = new Thickness(8)
        };

        mainGrid.Children.Add(BuildLeftPanel());
        Grid.SetColumn(mainGrid.Children[^1], 0);
        mainGrid.Children.Add(BuildCenterPanel());
        Grid.SetColumn(mainGrid.Children[^1], 1);
        mainGrid.Children.Add(new GridSplitter
        {
            Background = Brush("#333333"),
            ResizeDirection = GridResizeDirection.Columns,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Width = 6
        });
        Grid.SetColumn(mainGrid.Children[^1], 2);
        mainGrid.Children.Add(BuildRightPanel());
        Grid.SetColumn(mainGrid.Children[^1], 3);

        root.Children.Add(mainGrid);
        return root;
    }

    private Control BuildLeftPanel()
    {
        _setupTabs.Margin = new Thickness(0, 8, 0, 0);
        _setupTabs.Items.Add(new TabItem { Header = TabHeader("1 Import"), Content = ScrollablePanel(BuildSheetSetupPanel()) });
        _setupTabs.Items.Add(new TabItem { Header = TabHeader("2 Slice"), Content = ScrollablePanel(BuildGridSetupPanel()) });
        _setupTabs.Items.Add(new TabItem { Header = TabHeader("3 App Icon"), Content = ScrollablePanel(BuildAppIconSetupPanel()) });
        _setupTabs.Items.Add(new TabItem { Header = TabHeader("4 Export"), Content = ScrollablePanel(BuildExportSetupPanel()) });
        _setupTabs.Items.Add(new TabItem { Header = TabHeader("5 Animate"), Content = ScrollablePanel(BuildAnimateSetupPanel()) });
        _setupTabs.SelectedIndex = 0;
        _setupTabs.SelectionChanged += (_, _) => RefreshCenterWorkspace();

        var layout = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,*"),
            RowSpacing = 2
        };

        var heading = Heading("Setup Steps");
        layout.Children.Add(heading);
        Grid.SetRow(heading, 0);

        var helper = new TextBlock
        {
            Text = "Choose a step to prepare your avatar pack.",
            Foreground = Brushes.LightGray,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12
        };
        layout.Children.Add(helper);
        Grid.SetRow(helper, 1);

        layout.Children.Add(_setupTabs);
        Grid.SetRow(_setupTabs, 2);

        return PanelCard(layout);
    }

    private Control BuildSheetSetupPanel()
    {
        var stack = new StackPanel { Spacing = 8 };
        stack.Children.Add(Heading("Sprite Sheet"));

        var importButton = PrimaryButton("Import Sheet...");
        importButton.Click += async (_, _) => await ImportSheetAsync();
        stack.Children.Add(importButton);

        stack.Children.Add(_sheetInfoText);
        stack.Children.Add(_sheetFileSizeText);
        stack.Children.Add(Heading("Preview Slice Grid"));
        stack.Children.Add(new TextBlock
        {
            Text = "Adjust these values until the green grid matches the sheet.",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Foreground = Brushes.LightGray
        });
        stack.Children.Add(GridFields(
            ("Rows", _importRowsText), ("Columns", _importColumnsText),
            ("Cell W", _importCellWidthText), ("Cell H", _importCellHeightText),
            ("Offset X", _importOffsetXText), ("Offset Y", _importOffsetYText),
            ("Gap X", _importSpacingXText), ("Gap Y", _importSpacingYText)));

        var useSettingsButton = SecondaryButton("Use These Slice Settings");
        useSettingsButton.Click += (_, _) => UseImportSliceSettings();
        stack.Children.Add(useSettingsButton);
        stack.Children.Add(_importGridStatusText);
        return stack;
    }

    private Control BuildGridSetupPanel()
    {
        var stack = new StackPanel { Spacing = 8 };
        stack.Children.Add(Heading("Grid"));

        stack.Children.Add(GridFields(
            ("Rows", _rowsText), ("Columns", _columnsText),
            ("Cell W", _cellWidthText), ("Cell H", _cellHeightText),
            ("Offset X", _offsetXText), ("Offset Y", _offsetYText),
            ("Gap X", _spacingXText), ("Gap Y", _spacingYText)));

        var applyButton = SecondaryButton("Apply Slice Grid");
        applyButton.Click += (_, _) => ApplyGridFromUi();
        stack.Children.Add(applyButton);
        return stack;
    }

    private Control BuildExportSetupPanel()
    {
        var stack = new StackPanel { Spacing = 7 };
        stack.Children.Add(Heading("Output"));
        stack.Children.Add(new TextBlock
        {
            Text = "Exports a SAS-ready avatar pack.",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Foreground = Brushes.LightGray
        });
        stack.Children.Add(Labelled("Pack name", _packNameText));
        stack.Children.Add(Labelled("Export folder", _outputRootText));

        var browseOutput = SecondaryButton("Choose Export Folder...");
        browseOutput.Click += async (_, _) => await ChooseOutputRootAsync();
        stack.Children.Add(browseOutput);

        var exportButton = PrimaryButton("Export Pack");
        exportButton.Click += async (_, _) => await ExportPackAsync();
        stack.Children.Add(exportButton);
        stack.Children.Add(_exportStatusText);

        return stack;
    }

    private Control BuildAnimateSetupPanel()
    {
        var stack = new StackPanel { Spacing = 8 };
        stack.Children.Add(Heading("Animate"));
        stack.Children.Add(new TextBlock
        {
            Text = "Click sliced frames in the order you want them to play. Use the Animate panel on the right to preview and export.",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Foreground = Brushes.LightGray
        });

        stack.Children.Add(new TextBlock
        {
            Text = "Frames can be added more than once to hold a pose longer.",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Foreground = Brushes.LightGray
        });

        return stack;
    }

    private Control BuildAppIconSetupPanel()
    {
        var stack = new StackPanel { Spacing = 7 };
        stack.Children.Add(Heading("App Icon"));
        stack.Children.Add(new TextBlock
        {
            Text = "Optional icon for the exported avatar app.",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Foreground = Brushes.LightGray
        });

        var useFrameAsIconButton = CompactSecondaryButton("Use Selected Frame as Icon");
        useFrameAsIconButton.Click += (_, _) => UseSelectedFrameAsIcon();
        stack.Children.Add(useFrameAsIconButton);
        stack.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children =
            {
                new Border
                {
                    Width = 56,
                    Height = 56,
                    Padding = new Thickness(4),
                    Background = Brush("#252525"),
                    BorderBrush = Brush("#444444"),
                    BorderThickness = new Thickness(1),
                    Child = _iconPreviewImage
                },
                new StackPanel
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    Spacing = 6,
                    Children =
                    {
                        _iconInfoText,
                        CompactSecondaryButtonWithClick("Clear Icon", ClearIcon)
                    }
                }
            }
        });
        stack.Children.Add(new WrapPanel
        {
            Margin = new Thickness(0, -2, 0, 0),
            Children =
            {
                _icon16Check,
                _icon32Check,
                _icon48Check,
                _icon64Check,
                _icon128Check,
                _icon256Check
            }
        });

        return stack;
    }

    private Control BuildCenterPanel()
    {
        _importWorkspacePanel = BuildImportWorkspacePanel();
        _slicedFramesWorkspacePanel = BuildSlicedFramesWorkspacePanel();
        _centerWorkspaceHost.Children.Add(_importWorkspacePanel);
        _centerWorkspaceHost.Children.Add(_slicedFramesWorkspacePanel);
        RefreshCenterWorkspace();
        return PanelCard(_centerWorkspaceHost);
    }

    private void RefreshCenterWorkspace()
    {
        var showImportWorkspace = _setupTabs.SelectedIndex == 0;
        if (IsAnimateWorkflowSelected() && _rightTabs.ItemCount > 2)
            _rightTabs.SelectedIndex = 2;

        if (_importWorkspacePanel is not null)
        {
            _importWorkspacePanel.IsVisible = showImportWorkspace;
        }

        if (_slicedFramesWorkspacePanel is not null)
        {
            _slicedFramesWorkspacePanel.IsVisible = !showImportWorkspace;
        }
    }

    private Control BuildImportWorkspacePanel()
    {
        var dock = new DockPanel();
        var top = new StackPanel { Spacing = 4, Margin = new Thickness(0, 0, 0, 8) };
        top.Children.Add(Heading("Sprite Sheet Preview"));
        top.Children.Add(new TextBlock
        {
            Text = "Use the controls on the left to line up the green preview grid before slicing.",
            Foreground = Brushes.LightGray,
            TextWrapping = TextWrapping.Wrap
        });
        DockPanel.SetDock(top, Dock.Top);
        dock.Children.Add(top);

        dock.Children.Add(new Border
        {
            Background = Brush("#101010"),
            BorderBrush = Brush("#444444"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8),
            Child = _sheetPreviewControl
        });

        return dock;
    }

    private Control BuildSlicedFramesWorkspacePanel()
    {
        var dock = new DockPanel();
        var top = new StackPanel { Spacing = 4, Margin = new Thickness(0, 0, 0, 6) };
        top.Children.Add(Heading("Sliced Frames"));
        top.Children.Add(_selectedFrameText);
        DockPanel.SetDock(top, Dock.Top);
        dock.Children.Add(top);

        var scroll = new ScrollViewer
        {
            Content = _framePanel,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        dock.Children.Add(scroll);
        return dock;
    }

    private Control BuildRightPanel()
    {
        _rightTabs.Items.Add(new TabItem
        {
            Header = "Assign",
            Content = BuildAssignPanel()
        });
        _rightTabs.Items.Add(new TabItem
        {
            Header = "Playback",
            Content = BuildPlaybackPanel()
        });
        _rightTabs.Items.Add(new TabItem
        {
            Header = "Animate",
            Content = BuildAnimatePanel()
        });

        return new Border
        {
            Background = Brush("#1d1d1d"),
            BorderBrush = Brush("#444444"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10),
            Child = _rightTabs
        };
    }

    private Control BuildAssignPanel()
    {
        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,*,2*"),
            RowSpacing = 8
        };
        var stack = new StackPanel { Spacing = 8 };
        stack.Children.Add(Heading("Mood / Pose"));

        _stateCombo.ItemsSource = SpriteCatalog.States;
        _stateCombo.SelectedIndex = 0;
        _stateCombo.SelectionChanged += (_, _) =>
        {
            if (_stateCombo.SelectedItem is SpriteState state)
                _selectedState = state;
            RefreshAssignmentOptions();
            RefreshPlayback();
        };
        stack.Children.Add(_stateCombo);
        var customMood = SecondaryButton("Add Custom Mood / Pose…");
        customMood.Click += async (_, _) => await AddCustomMoodAsync();
        stack.Children.Add(customMood);

        _searchText.Watermark = "Quick search filename...";
        _searchText.TextChanged += (_, _) => RefreshAssignmentOptions();
        stack.Children.Add(_searchText);

        var unassignButton = SecondaryButton("Clear Roles From Selected Frame");
        unassignButton.Click += (_, _) => ClearRolesFromSelectedFrame();
        stack.Children.Add(unassignButton);

        var clearButton = SecondaryButton("Clear All Assignments");
        clearButton.Click += async (_, _) =>
        {
            if (await ConfirmAsync("Clear every frame assignment?"))
            {
                _assignments.Clear();
                foreach (var frame in _frames)
                    frame.AssignedFileNames.Clear();
                RefreshFramesPanel();
                RefreshSelectionText();
                RefreshSelectedFrameRoles();
                RefreshAssignmentOptions();
                RefreshPlayback();
            }
        };
        stack.Children.Add(clearButton);
        stack.Children.Add(Heading("Add Frame Slots"));
        stack.Children.Add(BuildFrameSlotButtons());
        Grid.SetRow(stack, 0);
        root.Children.Add(stack);

        var rolesHeading = Heading("Assigned to Selected Frame");
        Grid.SetRow(rolesHeading, 1);
        root.Children.Add(rolesHeading);

        var rolesPanel = new Border
        {
            Background = Brush("#141414"),
            BorderBrush = Brush("#333333"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6),
            Child = new ScrollViewer
            {
                Content = _selectedFrameRolesPanel,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            }
        };
        Grid.SetRow(rolesPanel, 2);
        root.Children.Add(rolesPanel);

        var optionsPanel = new Border
        {
            Background = Brush("#141414"),
            BorderBrush = Brush("#333333"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6),
            Child = new ScrollViewer
            {
                Content = _assignmentOptionsPanel,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            }
        };
        Grid.SetRow(optionsPanel, 3);
        root.Children.Add(optionsPanel);

        return root;
    }

    private Control BuildPlaybackPanel()
    {
        var stack = new StackPanel { Spacing = 8 };
        stack.Children.Add(Heading("Sprite Playback"));

        _playbackModeCombo.ItemsSource = new[]
        {
            "Idle loop",
            "Blink check",
            "Talk small",
            "Talk normal",
            "Talk large",
            "Mouth test"
        };
        _playbackModeCombo.SelectedIndex = 0;
        _playbackModeCombo.SelectionChanged += (_, _) => RefreshPlayback();
        stack.Children.Add(_playbackModeCombo);

        stack.Children.Add(new ScrollViewer
        {
            Content = _playbackStrip,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Height = 76
        });

        stack.Children.Add(new Border
        {
            Background = Brush("#252525"),
            BorderBrush = Brush("#444444"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6),
            Child = _playbackImage
        });
        stack.Children.Add(_playbackInfoText);

        var controls = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        var previousButton = SecondaryButton("Prev");
        previousButton.Click += (_, _) => AdvancePlayback(-1);
        _playPauseButton.Click += (_, _) => TogglePlayback();
        var nextButton = SecondaryButton("Next");
        nextButton.Click += (_, _) => AdvancePlayback(1);
        controls.Children.Add(previousButton);
        controls.Children.Add(_playPauseButton);
        controls.Children.Add(nextButton);
        stack.Children.Add(controls);

        stack.Children.Add(Labelled("Speed ms", _speedText));
        var popOutButton = SecondaryButton("Pop Out Preview");
        popOutButton.Click += (_, _) => OpenPopOutPreview();
        stack.Children.Add(popOutButton);
        return new ScrollViewer
        {
            Content = stack,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
    }

    private Control BuildAnimatePanel()
    {
        var stack = new StackPanel
        {
            Spacing = 8,
            Margin = new Thickness(0, 0, 14, 0)
        };
        stack.Children.Add(Heading("Custom Animation"));
        stack.Children.Add(new TextBlock
        {
            Text = "Select 5 Animate, then click sliced frames to append them here.",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Foreground = Brushes.LightGray
        });
        stack.Children.Add(Labelled("Animation name", _animationNameText));
        stack.Children.Add(Labelled("Speed ms", _animateSpeedText));

        stack.Children.Add(Heading("Assigned Frames"));
        stack.Children.Add(new Border
        {
            Background = Brush("#141414"),
            BorderBrush = Brush("#333333"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6),
            MinHeight = 150,
            Child = new ScrollViewer
            {
                Content = _animationAssignedPanel,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            }
        });

        var orderControls = new WrapPanel { Margin = new Thickness(0, -2, 0, 0) };
        var removeButton = CompactSecondaryButton("Remove");
        removeButton.Margin = new Thickness(0, 0, 6, 6);
        removeButton.Click += (_, _) => RemoveSelectedAnimationFrame();
        var upButton = CompactSecondaryButton("Move Up");
        upButton.Margin = new Thickness(0, 0, 6, 6);
        upButton.Click += (_, _) => MoveSelectedAnimationFrame(-1);
        var downButton = CompactSecondaryButton("Move Down");
        downButton.Margin = new Thickness(0, 0, 6, 6);
        downButton.Click += (_, _) => MoveSelectedAnimationFrame(1);
        var clearButton = CompactSecondaryButton("Clear");
        clearButton.Margin = new Thickness(0, 0, 6, 6);
        clearButton.Click += (_, _) => ClearAnimationFrames();
        orderControls.Children.Add(removeButton);
        orderControls.Children.Add(upButton);
        orderControls.Children.Add(downButton);
        orderControls.Children.Add(clearButton);
        stack.Children.Add(orderControls);

        stack.Children.Add(Heading("Playback"));
        stack.Children.Add(new Border
        {
            Background = Brush("#252525"),
            BorderBrush = Brush("#444444"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6),
            Child = _animatePlaybackImage
        });
        stack.Children.Add(_animateInfoText);

        var playbackControls = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        var previousButton = SecondaryButton("Prev");
        previousButton.Click += (_, _) => AdvanceAnimationPlayback(-1);
        _animatePlayPauseButton.Click += (_, _) => ToggleAnimationPlayback();
        var nextButton = SecondaryButton("Next");
        nextButton.Click += (_, _) => AdvanceAnimationPlayback(1);
        playbackControls.Children.Add(previousButton);
        playbackControls.Children.Add(_animatePlayPauseButton);
        playbackControls.Children.Add(nextButton);
        stack.Children.Add(playbackControls);

        var exportGifButton = PrimaryButton("Export GIF");
        exportGifButton.Click += async (_, _) => await ExportAnimationGifAsync();
        stack.Children.Add(exportGifButton);
        var exportToPackButton = SecondaryButton("Export GIF to Avatar Pack");
        exportToPackButton.Click += async (_, _) => await ExportAnimationAsync("gif-pack");
        stack.Children.Add(exportToPackButton);

        var exportSheetButton = SecondaryButton("Export Sprite Sheet");
        exportSheetButton.Click += async (_, _) => await ExportAnimationSpriteSheetAsync();
        stack.Children.Add(exportSheetButton);

        return new ScrollViewer
        {
            Content = stack,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
    }

    private void SetupEvents()
    {
        foreach (var textBox in new[] { _rowsText, _columnsText, _cellWidthText, _cellHeightText, _offsetXText, _offsetYText, _spacingXText, _spacingYText })
        {
            textBox.LostFocus += (_, _) => ApplyGridFromUi();
            textBox.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter)
                    ApplyGridFromUi();
            };
        }

        foreach (var textBox in new[] { _importRowsText, _importColumnsText, _importCellWidthText, _importCellHeightText, _importOffsetXText, _importOffsetYText, _importSpacingXText, _importSpacingYText })
        {
            textBox.ValueChanged += (_, _) => RefreshImportPreviewFromUi();
            textBox.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter)
                    UseImportSliceSettings();
            };
        }

        _speedText.LostFocus += (_, _) => UpdatePlaybackSpeed();
        _animateSpeedText.LostFocus += (_, _) => UpdateAnimationSpeed();
    }

    private async Task ImportSheetAsync()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null)
            return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import sprite sheet",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("PNG images") { Patterns = new[] { "*.png" } },
                new FilePickerFileType("All images") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp" } }
            }
        });

        var file = files.FirstOrDefault();
        if (file?.Path.LocalPath is { Length: > 0 } path)
            await LoadSheetAsync(path);
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        var file = e.Data.GetFiles()?.FirstOrDefault();
        if (file?.Path.LocalPath is { Length: > 0 } path)
            await LoadSheetAsync(path);
    }

    private async Task LoadSheetAsync(string path)
    {
        try
        {
            DisposeFramePreviews();
            _assignments.Clear();
            ClearIcon();
            _sheetImage?.Dispose();
            _sheetPreviewBitmap?.Dispose();
            _sheetImage = SixLabors.ImageSharp.Image.Load<Rgba32>(path);
            _sheetPreviewBitmap = new Bitmap(path);
            _sheetPath = path;
            _sheetInfoText.Text = $"{Path.GetFileName(path)}\nWidth: {_sheetImage.Width}px\nHeight: {_sheetImage.Height}px";
            long decodedBytes = (long)_sheetImage.Width * _sheetImage.Height * 4;
            _sheetFileSizeText.Text = $"File size: {FormatFileSize(new FileInfo(path).Length)}\nImage memory: {FormatFileSize(decodedBytes)}";
            ClearAnimationFrames();
            _sheetPreviewControl.SetSheet(_sheetPreviewBitmap, _sheetImage.Width, _sheetImage.Height);
            GuessImportGridFromSheet();
            RefreshImportPreviewFromUi();
            RefreshFramesPanel();
            RefreshSelectionText();
            RefreshSelectedFrameRoles();
            RefreshAssignmentOptions();
            RefreshPlayback();
        }
        catch (Exception ex)
        {
            await ShowMessageAsync("Import failed", ex.Message);
        }
    }

    private void GuessImportGridFromSheet()
    {
        if (_sheetImage == null)
            return;

        CopySliceFieldsToImportFields();

        int columns = ReadNumber(_importColumnsText);
        if (columns > 0)
            SetNumber(_importCellWidthText, Math.Max(1, _sheetImage.Width / columns));
        int rows = ReadNumber(_importRowsText);
        if (rows > 0)
            SetNumber(_importCellHeightText, Math.Max(1, _sheetImage.Height / rows));
    }

    private void CopySliceFieldsToImportFields()
    {
        SetNumber(_importRowsText, ReadNumber(_rowsText));
        SetNumber(_importColumnsText, ReadNumber(_columnsText));
        SetNumber(_importCellWidthText, ReadNumber(_cellWidthText));
        SetNumber(_importCellHeightText, ReadNumber(_cellHeightText));
        SetNumber(_importOffsetXText, ReadNumber(_offsetXText));
        SetNumber(_importOffsetYText, ReadNumber(_offsetYText));
        SetNumber(_importSpacingXText, ReadNumber(_spacingXText));
        SetNumber(_importSpacingYText, ReadNumber(_spacingYText));
    }

    private void UseImportSliceSettings()
    {
        if (!TryReadImportGridSettings())
            return;

        SetNumber(_rowsText, ReadNumber(_importRowsText));
        SetNumber(_columnsText, ReadNumber(_importColumnsText));
        SetNumber(_cellWidthText, ReadNumber(_importCellWidthText));
        SetNumber(_cellHeightText, ReadNumber(_importCellHeightText));
        SetNumber(_offsetXText, ReadNumber(_importOffsetXText));
        SetNumber(_offsetYText, ReadNumber(_importOffsetYText));
        SetNumber(_spacingXText, ReadNumber(_importSpacingXText));
        SetNumber(_spacingYText, ReadNumber(_importSpacingYText));
        ApplyGridFromUi();
        _setupTabs.SelectedIndex = 1;
        RefreshCenterWorkspace();
    }

    private void RefreshImportPreviewFromUi()
    {
        if (!TryReadImportGridSettings())
        {
            _sheetPreviewControl.SetGrid(null);
            return;
        }

        _sheetPreviewControl.SetGrid(_importGridSettings);
        UpdateImportGridStatus();
    }

    private bool TryReadImportGridSettings()
    {
        int rows = 0;
        int columns = 0;
        int cellWidth = 0;
        int cellHeight = 0;
        int offsetX = 0;
        int offsetY = 0;
        int spacingX = 0;
        int spacingY = 0;

        bool ok =
            TryReadPositive(_importRowsText, out rows) &&
            TryReadPositive(_importColumnsText, out columns) &&
            TryReadPositive(_importCellWidthText, out cellWidth) &&
            TryReadPositive(_importCellHeightText, out cellHeight) &&
            TryReadInt(_importOffsetXText, out offsetX) &&
            TryReadInt(_importOffsetYText, out offsetY) &&
            TryReadInt(_importSpacingXText, out spacingX) &&
            TryReadInt(_importSpacingYText, out spacingY);

        if (!ok)
        {
            _importGridStatusText.Text = "Preview grid values must be valid numbers.";
            return false;
        }

        _importGridSettings.Rows = rows;
        _importGridSettings.Columns = columns;
        _importGridSettings.CellWidth = cellWidth;
        _importGridSettings.CellHeight = cellHeight;
        _importGridSettings.OffsetX = offsetX;
        _importGridSettings.OffsetY = offsetY;
        _importGridSettings.SpacingX = spacingX;
        _importGridSettings.SpacingY = spacingY;
        return true;
    }

    private void UpdateImportGridStatus()
    {
        if (_sheetImage == null)
        {
            _importGridStatusText.Text = "Import a sprite sheet to preview the slice grid.";
            return;
        }

        int expectedCells = _importGridSettings.Rows * _importGridSettings.Columns;
        int visibleCells = CountVisibleImportGridCells();
        _importGridStatusText.Text = visibleCells == expectedCells
            ? $"Previewing {visibleCells} slice box(es)."
            : $"Previewing {visibleCells}/{expectedCells} slice box(es); some fall outside the sheet.";
    }

    private int CountVisibleImportGridCells()
    {
        if (_sheetImage == null)
            return 0;

        int visible = 0;
        for (int row = 0; row < _importGridSettings.Rows; row++)
        {
            for (int column = 0; column < _importGridSettings.Columns; column++)
            {
                int x = _importGridSettings.OffsetX + column * (_importGridSettings.CellWidth + _importGridSettings.SpacingX);
                int y = _importGridSettings.OffsetY + row * (_importGridSettings.CellHeight + _importGridSettings.SpacingY);
                if (x < _sheetImage.Width &&
                    y < _sheetImage.Height &&
                    x + _importGridSettings.CellWidth > 0 &&
                    y + _importGridSettings.CellHeight > 0)
                {
                    visible++;
                }
            }
        }

        return visible;
    }

    private void GuessGridFromSheet()
    {
        if (_sheetImage == null)
            return;

        int columns = ReadNumber(_columnsText);
        if (columns > 0)
            SetNumber(_cellWidthText, Math.Max(1, _sheetImage.Width / columns));
        int rows = ReadNumber(_rowsText);
        if (rows > 0)
            SetNumber(_cellHeightText, Math.Max(1, _sheetImage.Height / rows));
    }

    private void ApplyGridFromUi()
    {
        if (!TryReadGridSettings())
            return;

        if (_sheetImage == null)
        {
            _framePanel.Children.Clear();
            DisposeFramePreviews();
            return;
        }

        var previousAssignments = _assignments
            .Select(pair => new { FileName = pair.Key, FrameIndex = pair.Value.Index })
            .ToList();

        DisposeFramePreviews();
        _frames = ImageSlicer.Slice(_sheetImage, _gridSettings);
        _assignments.Clear();
        foreach (var frame in _frames)
            frame.AssignedFileNames.Clear();

        foreach (var assignment in previousAssignments)
        {
            var frame = _frames.FirstOrDefault(candidate => candidate.Index == assignment.FrameIndex);
            if (frame == null)
                continue;

            _assignments[assignment.FileName] = frame;
            frame.AssignedFileNames.Add(assignment.FileName);
            frame.AssignedFileNames.Sort(StringComparer.OrdinalIgnoreCase);
        }

        if (_iconFrameIndex.HasValue && !_frames.Any(frame => frame.Index == _iconFrameIndex.Value))
            ClearIcon();
        else
            RefreshIconPreview();

        PruneMissingAnimationFrames();
        _selectedFrame = _frames.FirstOrDefault();
        RefreshFramesPanel();
        RefreshSelectionText();
        RefreshSelectedFrameRoles();
        RefreshPlayback();
        RefreshAnimatePanel();
        ShowAnimateFrame();
    }

    private bool TryReadGridSettings()
    {
        int rows = 0;
        int columns = 0;
        int cellWidth = 0;
        int cellHeight = 0;
        int offsetX = 0;
        int offsetY = 0;
        int spacingX = 0;
        int spacingY = 0;

        bool ok =
            TryReadPositive(_rowsText, out rows) &&
            TryReadPositive(_columnsText, out columns) &&
            TryReadPositive(_cellWidthText, out cellWidth) &&
            TryReadPositive(_cellHeightText, out cellHeight) &&
            TryReadInt(_offsetXText, out offsetX) &&
            TryReadInt(_offsetYText, out offsetY) &&
            TryReadInt(_spacingXText, out spacingX) &&
            TryReadInt(_spacingYText, out spacingY);

        if (!ok)
        {
            _exportStatusText.Text = "Grid values must be valid numbers.";
            return false;
        }

        _gridSettings.Rows = rows;
        _gridSettings.Columns = columns;
        _gridSettings.CellWidth = cellWidth;
        _gridSettings.CellHeight = cellHeight;
        _gridSettings.OffsetX = offsetX;
        _gridSettings.OffsetY = offsetY;
        _gridSettings.SpacingX = spacingX;
        _gridSettings.SpacingY = spacingY;
        _exportStatusText.Text = string.Empty;
        return true;
    }

    private void RefreshFramesPanel()
    {
        _framePanel.Children.Clear();

        foreach (var frame in _frames)
            _framePanel.Children.Add(CreateFrameTile(frame));
    }

    private Control CreateFrameTile(SlicedFrame frame)
    {
        var border = new Border
        {
            Width = 132,
            Height = 154,
            Margin = new Thickness(4),
            Padding = new Thickness(4),
            Background = frame == _selectedFrame ? Brush("#304f6f") : Brush("#242424"),
            BorderBrush = frame.HasAssignments ? Brushes.Gold : Brush("#555555"),
            BorderThickness = new Thickness(frame == _selectedFrame ? 2 : 1),
            Child = new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    PixelArtImage(frame.Preview, 112, 112),
                    new TextBlock
                    {
                        Text = frame.Label,
                        FontSize = 10,
                        TextWrapping = TextWrapping.Wrap,
                        TextAlignment = TextAlignment.Center,
                        Foreground = frame.HasAssignments ? Brushes.Gold : Brushes.LightGray
                    }
                }
            }
        };

        border.PointerPressed += (_, _) =>
        {
            _selectedFrame = frame;
            if (IsAnimateWorkflowSelected())
            {
                AddFrameToAnimation(frame);
                return;
            }

            RefreshFramesPanel();
            RefreshSelectionText();
            RefreshSelectedFrameRoles();
        };

        return border;
    }

    private void RefreshSelectionText()
    {
        if (_selectedFrame == null)
        {
            _selectedFrameText.Text = "No frame selected.";
            return;
        }

        string roles = _selectedFrame.AssignedFileNames.Count == 0
            ? "unassigned"
            : string.Join(", ", _selectedFrame.AssignedFileNames);

        _selectedFrameText.Text = $"Selected frame {_selectedFrame.Index + 1} ({_selectedFrame.Width}x{_selectedFrame.Height}) - {roles}";
    }

    private void RefreshSelectedFrameRoles()
    {
        _selectedFrameRolesPanel.Children.Clear();

        if (_selectedFrame == null)
        {
            _selectedFrameRolesPanel.Children.Add(new TextBlock { Text = "No frame selected.", Foreground = Brushes.LightGray });
            return;
        }

        if (_selectedFrame.AssignedFileNames.Count == 0)
        {
            _selectedFrameRolesPanel.Children.Add(new TextBlock { Text = "No roles assigned.", Foreground = Brushes.LightGray });
            return;
        }

        foreach (string fileName in _selectedFrame.AssignedFileNames.ToList())
        {
            var row = new DockPanel { LastChildFill = true };
            var removeButton = SecondaryButton("Remove");
            removeButton.MinWidth = 74;
            removeButton.Click += (_, _) => UnassignRoleFromSelectedFrame(fileName);
            DockPanel.SetDock(removeButton, Dock.Right);
            row.Children.Add(removeButton);
            row.Children.Add(new TextBlock
            {
                Text = fileName,
                Foreground = Brushes.Gold,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            });
            _selectedFrameRolesPanel.Children.Add(row);
        }
    }

    private Control BuildFrameSlotButtons()
    {
        var panel = new WrapPanel
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, -2, 0, 0)
        };

        foreach (var group in SpriteCatalog.FrameGroups)
        {
            var button = CompactSecondaryButton($"+ {group.DisplayName}");
            button.Margin = new Thickness(0, 0, 6, 6);
            button.Click += (_, _) => AddFrameSlot(group);
            panel.Children.Add(button);
        }

        return panel;
    }

    private void AddFrameSlot(SpriteFrameGroup group)
    {
        int nextCount = SpriteCatalog.AddFrameSlot(group.Id);
        _exportStatusText.Text = $"Added {group.DisplayName} slot {nextCount:000} for every mood / pose.";
        RefreshAssignmentOptions();
        RefreshPlayback();
        RefreshAnimatePanel();
    }

    private void RefreshAssignmentOptions()
    {
        _assignmentOptionsPanel.Children.Clear();
        string query = _searchText.Text?.Trim() ?? string.Empty;

        foreach (string fileName in SpriteCatalog.FileNamesFor(_selectedState))
        {
            if (!string.IsNullOrWhiteSpace(query) && fileName.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            bool assigned = _assignments.ContainsKey(fileName);
            var button = assigned ? SecondaryButton(fileName) : PrimaryButton(fileName);
            button.IsEnabled = !assigned;
            button.Opacity = assigned ? 0.45 : 1.0;
            button.Click += (_, _) => AssignSelectedFrame(fileName);
            _assignmentOptionsPanel.Children.Add(button);
        }
    }

    private void AssignSelectedFrame(string fileName)
    {
        if (_selectedFrame == null)
        {
            _exportStatusText.Text = "Select a sliced frame first.";
            return;
        }

        if (_assignments.ContainsKey(fileName))
            return;

        _assignments[fileName] = _selectedFrame;
        _selectedFrame.AssignedFileNames.Add(fileName);
        _selectedFrame.AssignedFileNames.Sort(StringComparer.OrdinalIgnoreCase);
        _exportStatusText.Text = $"Assigned frame {_selectedFrame.Index + 1} to {fileName}.";

        RefreshFramesPanel();
        RefreshSelectionText();
        RefreshSelectedFrameRoles();
        RefreshAssignmentOptions();
        RefreshPlayback();
    }

    private void UnassignRoleFromSelectedFrame(string fileName)
    {
        if (_selectedFrame == null)
            return;

        _assignments.Remove(fileName);
        _selectedFrame.AssignedFileNames.Remove(fileName);
        RefreshFramesPanel();
        RefreshSelectionText();
        RefreshSelectedFrameRoles();
        RefreshAssignmentOptions();
        RefreshPlayback();
    }

    private void ClearRolesFromSelectedFrame()
    {
        if (_selectedFrame == null || _selectedFrame.AssignedFileNames.Count == 0)
            return;

        foreach (string fileName in _selectedFrame.AssignedFileNames.ToList())
            _assignments.Remove(fileName);

        _selectedFrame.AssignedFileNames.Clear();
        RefreshFramesPanel();
        RefreshSelectionText();
        RefreshSelectedFrameRoles();
        RefreshAssignmentOptions();
        RefreshPlayback();
    }

    private bool IsAnimateWorkflowSelected()
    {
        return _setupTabs.SelectedIndex == 4;
    }

    private void AddFrameToAnimation(SlicedFrame frame)
    {
        _animationFrameIndexes.Add(frame.Index);
        _selectedAnimationEntryIndex = _animationFrameIndexes.Count - 1;
        _animatePlaybackIndex = _selectedAnimationEntryIndex;
        _animateInfoText.Text = $"Added frame {frame.Index + 1}.";

        RefreshFramesPanel();
        RefreshSelectionText();
        RefreshSelectedFrameRoles();
        RefreshAnimatePanel();
        ShowAnimateFrame();
    }

    private void RefreshAnimatePanel()
    {
        PruneMissingAnimationFrames();
        _animationAssignedPanel.Children.Clear();

        if (_animationFrameIndexes.Count == 0)
        {
            _animationAssignedPanel.Children.Add(new TextBlock
            {
                Text = "No frames assigned. Select 5 Animate, then click sliced frames.",
                Foreground = Brushes.LightGray,
                TextWrapping = TextWrapping.Wrap
            });
            return;
        }

        for (int i = 0; i < _animationFrameIndexes.Count; i++)
        {
            int entryIndex = i;
            var frame = FindFrameByIndex(_animationFrameIndexes[i]);
            if (frame == null)
                continue;

            var row = new Border
            {
                Background = entryIndex == _selectedAnimationEntryIndex ? Brush("#304f6f") : Brush("#242424"),
                BorderBrush = entryIndex == _selectedAnimationEntryIndex ? Brushes.Gold : Brush("#555555"),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(5),
                Child = new DockPanel
                {
                    LastChildFill = true,
                    Children =
                    {
                        PixelArtImage(frame.Preview, 44, 44, new Thickness(0, 0, 8, 0)),
                        new TextBlock
                        {
                            Text = $"{entryIndex + 1}. Frame {frame.Index + 1}",
                            Foreground = Brushes.White,
                            VerticalAlignment = VerticalAlignment.Center
                        }
                    }
                }
            };

            row.PointerPressed += (_, _) =>
            {
                _selectedAnimationEntryIndex = entryIndex;
                _animatePlaybackIndex = entryIndex;
                RefreshAnimatePanel();
                ShowAnimateFrame();
            };

            _animationAssignedPanel.Children.Add(row);
        }
    }

    private void RemoveSelectedAnimationFrame()
    {
        if (!IsValidAnimationEntryIndex(_selectedAnimationEntryIndex))
            return;

        _animationFrameIndexes.RemoveAt(_selectedAnimationEntryIndex);
        if (_selectedAnimationEntryIndex >= _animationFrameIndexes.Count)
            _selectedAnimationEntryIndex = _animationFrameIndexes.Count - 1;
        _animatePlaybackIndex = Math.Clamp(_selectedAnimationEntryIndex, 0, Math.Max(0, _animationFrameIndexes.Count - 1));
        RefreshAnimatePanel();
        ShowAnimateFrame();
    }

    private void MoveSelectedAnimationFrame(int direction)
    {
        if (!IsValidAnimationEntryIndex(_selectedAnimationEntryIndex))
            return;

        int targetIndex = _selectedAnimationEntryIndex + direction;
        if (targetIndex < 0 || targetIndex >= _animationFrameIndexes.Count)
            return;

        (_animationFrameIndexes[_selectedAnimationEntryIndex], _animationFrameIndexes[targetIndex]) =
            (_animationFrameIndexes[targetIndex], _animationFrameIndexes[_selectedAnimationEntryIndex]);
        _selectedAnimationEntryIndex = targetIndex;
        _animatePlaybackIndex = targetIndex;
        RefreshAnimatePanel();
        ShowAnimateFrame();
    }

    private void ClearAnimationFrames()
    {
        _animateTimer.Stop();
        _isAnimatePlaying = false;
        _animatePlayPauseButton.Content = "Play";
        _animationFrameIndexes.Clear();
        _selectedAnimationEntryIndex = -1;
        _animatePlaybackIndex = 0;
        RefreshAnimatePanel();
        ShowAnimateFrame();
    }

    private void ToggleAnimationPlayback()
    {
        if (_isAnimatePlaying)
        {
            _animateTimer.Stop();
            _isAnimatePlaying = false;
            _animatePlayPauseButton.Content = "Play";
            return;
        }

        if (_animationFrameIndexes.Count == 0)
        {
            _animateInfoText.Text = "Add frames before playing the custom animation.";
            return;
        }

        UpdateAnimationSpeed();
        _animateTimer.Start();
        _isAnimatePlaying = true;
        _animatePlayPauseButton.Content = "Pause";
    }

    private void AdvanceAnimationPlayback(int delta)
    {
        var frames = GetAnimationFrames();
        if (frames.Count == 0)
            return;

        _animatePlaybackIndex = (_animatePlaybackIndex + delta + frames.Count) % frames.Count;
        _selectedAnimationEntryIndex = _animatePlaybackIndex;
        RefreshAnimatePanel();
        ShowAnimateFrame();
    }

    private void ShowAnimateFrame()
    {
        var frames = GetAnimationFrames();
        if (frames.Count == 0)
        {
            _animatePlaybackImage.Source = null;
            _animateInfoText.Text = "No animation frames assigned.";
            return;
        }

        _animatePlaybackIndex = Math.Clamp(_animatePlaybackIndex, 0, frames.Count - 1);
        var frame = frames[_animatePlaybackIndex];
        _animatePlaybackImage.Source = frame.Preview;
        _animateInfoText.Text = $"{_animatePlaybackIndex + 1}/{frames.Count}  Frame {frame.Index + 1}  {frame.Width}x{frame.Height}px";
    }

    private void UpdateAnimationSpeed()
    {
        if (TryReadPositive(_animateSpeedText.Text, out int speed))
            _animateTimer.Interval = TimeSpan.FromMilliseconds(Math.Clamp(speed, 30, 2000));
    }

    private IReadOnlyList<SlicedFrame> GetAnimationFrames()
    {
        var frames = new List<SlicedFrame>();
        foreach (int frameIndex in _animationFrameIndexes)
        {
            var frame = FindFrameByIndex(frameIndex);
            if (frame != null)
                frames.Add(frame);
        }

        return frames;
    }

    private SlicedFrame? FindFrameByIndex(int frameIndex)
    {
        return _frames.FirstOrDefault(frame => frame.Index == frameIndex);
    }

    private bool IsValidAnimationEntryIndex(int entryIndex)
    {
        return entryIndex >= 0 && entryIndex < _animationFrameIndexes.Count;
    }

    private void PruneMissingAnimationFrames()
    {
        if (_animationFrameIndexes.Count == 0)
            return;

        var validIndexes = _frames.Select(frame => frame.Index).ToHashSet();
        _animationFrameIndexes.RemoveAll(index => !validIndexes.Contains(index));

        if (_selectedAnimationEntryIndex >= _animationFrameIndexes.Count)
            _selectedAnimationEntryIndex = _animationFrameIndexes.Count - 1;
        if (_animationFrameIndexes.Count == 0)
            _selectedAnimationEntryIndex = -1;
        _animatePlaybackIndex = Math.Clamp(_animatePlaybackIndex, 0, Math.Max(0, _animationFrameIndexes.Count - 1));
    }

    private void RefreshPlayback()
    {
        _playbackStrip.Children.Clear();
        var names = GetPlaybackFileNames().ToList();

        foreach (string name in names)
        {
            _playbackStrip.Children.Add(new Border
            {
                Width = 72,
                Height = 72,
                Background = _assignments.TryGetValue(name, out var frame) ? Brush("#24384c") : Brush("#333333"),
                BorderBrush = _assignments.ContainsKey(name) ? Brushes.Gold : Brush("#555555"),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(3),
                Child = _assignments.TryGetValue(name, out frame)
                    ? PixelArtImage(frame.Preview)
                    : new TextBlock { Text = "Missing", Foreground = Brushes.Gray, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
            });
        }

        _playbackIndex = Math.Clamp(_playbackIndex, 0, Math.Max(0, names.Count - 1));
        ShowPlaybackFrame();
        PreviewDataChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OpenPopOutPreview()
    {
        if (_popOutPreviewWindow != null)
        {
            _popOutPreviewWindow.Activate();
            _popOutPreviewWindow.RefreshPreview();
            return;
        }

        _popOutPreviewWindow = new AnimationPreviewWindow(this);
        _popOutPreviewWindow.Closed += (_, _) => _popOutPreviewWindow = null;
        _popOutPreviewWindow.Show(this);
        _popOutPreviewWindow.RefreshPreview();
    }

    internal IReadOnlyList<AnimationPreviewFrame> GetRawPreviewFrames()
    {
        return _frames
            .OrderBy(frame => frame.Index)
            .Select(frame => new AnimationPreviewFrame(
                $"Frame {frame.Index + 1}",
                frame.Preview,
                false,
                frame.Width,
                frame.Height))
            .ToList();
    }

    internal IReadOnlyList<AnimationPreviewFrame> GetAssignedPreviewFrames()
    {
        return GetPlaybackFileNames()
            .Select(name =>
            {
                if (_assignments.TryGetValue(name, out var frame) && frame.Preview != null)
                {
                    return new AnimationPreviewFrame(name, frame.Preview, false, frame.Width, frame.Height);
                }

                return new AnimationPreviewFrame($"Missing: {name}", null, true, 0, 0);
            })
            .ToList();
    }

    private IEnumerable<string> GetPlaybackFileNames()
    {
        string prefix = _selectedState.Prefix;
        string mode = _playbackModeCombo.SelectedItem?.ToString() ?? "Idle loop";

        return mode switch
        {
            "Blink check" => new[] { $"{prefix}_idle001.png" }
                .Concat(SpriteCatalog.FileNamesFor(_selectedState, "idle_blink"))
                .Concat(new[] { $"{prefix}_idle001.png" }),
            "Talk small" => SpriteCatalog.FileNamesFor(_selectedState, "talk_small"),
            "Talk normal" => SpriteCatalog.FileNamesFor(_selectedState, "talk"),
            "Talk large" => SpriteCatalog.FileNamesFor(_selectedState, "talk_large"),
            "Mouth test" => GetAssignedMouthTestFileNames(prefix),
            _ => SpriteCatalog.FileNamesFor(_selectedState, "idle")
        };
    }

    private IEnumerable<string> GetAssignedMouthTestFileNames(string prefix)
    {
        var state = SpriteCatalog.States.FirstOrDefault(candidate => candidate.Prefix == prefix) ?? _selectedState;
        var small = AssignedOnly(SpriteCatalog.FileNamesFor(state, "talk_small")).ToList();
        var normal = AssignedOnly(SpriteCatalog.FileNamesFor(state, "talk")).ToList();
        var large = AssignedOnly(SpriteCatalog.FileNamesFor(state, "talk_large")).ToList();

        return small
            .Concat(normal)
            .Concat(large)
            .Concat(normal)
            .Concat(small)
            .ToList();
    }

    private IEnumerable<string> AssignedOnly(IEnumerable<string> fileNames)
    {
        return fileNames.Where(fileName => _assignments.ContainsKey(fileName));
    }

    private void TogglePlayback()
    {
        if (_isPlaying)
        {
            _playbackTimer.Stop();
            _isPlaying = false;
            _playPauseButton.Content = "Play";
            return;
        }

        UpdatePlaybackSpeed();
        _playbackTimer.Start();
        _isPlaying = true;
        _playPauseButton.Content = "Pause";
    }

    private void AdvancePlayback(int delta)
    {
        var names = GetPlaybackFileNames().ToList();
        if (names.Count == 0)
            return;

        _playbackIndex = (_playbackIndex + delta + names.Count) % names.Count;
        ShowPlaybackFrame();
    }

    private void ShowPlaybackFrame()
    {
        var names = GetPlaybackFileNames().ToList();
        if (names.Count == 0)
        {
            _playbackImage.Source = null;
            _playbackInfoText.Text = "No playback frames.";
            return;
        }

        string name = names[_playbackIndex];
        if (_assignments.TryGetValue(name, out var frame) && frame.Preview != null)
        {
            _playbackImage.Source = frame.Preview;
            _playbackInfoText.Text = $"{_playbackIndex + 1}/{names.Count}  {name}  {frame.Width}x{frame.Height}px";
        }
        else
        {
            _playbackImage.Source = null;
            _playbackInfoText.Text = $"{_playbackIndex + 1}/{names.Count}  {name} is missing.";
        }
    }

    private void UpdatePlaybackSpeed()
    {
        if (TryReadPositive(_speedText.Text, out int speed))
            _playbackTimer.Interval = TimeSpan.FromMilliseconds(Math.Clamp(speed, 30, 2000));
    }

    private async Task ChooseOutputRootAsync()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null)
            return;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Choose export folder",
            AllowMultiple = false
        });

        var folder = folders.FirstOrDefault();
        if (folder?.Path.LocalPath is { Length: > 0 } path)
            _outputRootText.Text = path;
    }

    private void UseSelectedFrameAsIcon()
    {
        if (_selectedFrame == null)
        {
            _exportStatusText.Text = "Select a sliced frame before setting the app icon.";
            return;
        }

        _iconFrameIndex = _selectedFrame.Index;
        RefreshIconPreview();
    }

    private void ClearIcon()
    {
        _iconFrameIndex = null;
        _iconPreviewImage.Source = null;
        _iconInfoText.Text = "No app icon frame selected.";
    }

    private void RefreshIconPreview()
    {
        if (!_iconFrameIndex.HasValue)
        {
            ClearIcon();
            return;
        }

        var frame = _frames.FirstOrDefault(candidate => candidate.Index == _iconFrameIndex.Value);
        if (frame == null)
        {
            ClearIcon();
            return;
        }

        _iconPreviewImage.Source = frame.Preview;
        _iconInfoText.Text = $"Frame {frame.Index + 1} - {frame.Width}x{frame.Height}";
    }

    private IReadOnlyList<int> GetSelectedIconSizes()
    {
        var sizes = new List<int>();
        if (_icon16Check.IsChecked == true) sizes.Add(16);
        if (_icon32Check.IsChecked == true) sizes.Add(32);
        if (_icon48Check.IsChecked == true) sizes.Add(48);
        if (_icon64Check.IsChecked == true) sizes.Add(64);
        if (_icon128Check.IsChecked == true) sizes.Add(128);
        if (_icon256Check.IsChecked == true) sizes.Add(256);
        return sizes;
    }

    private async Task ExportPackAsync()
    {
        if (_sheetImage == null)
        {
            await ShowMessageAsync("Export failed", "Import a sprite sheet before exporting.");
            return;
        }

        string packName = _packNameText.Text?.Trim() ?? string.Empty;
        string outputRoot = _outputRootText.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(packName) || string.IsNullOrWhiteSpace(outputRoot))
        {
            await ShowMessageAsync("Export failed", "Set a pack name and export folder first.");
            return;
        }

        if (!IsValidPackName(packName))
        {
            await ShowMessageAsync("Export failed", "Pack name can only use letters, numbers, hyphens, and underscores.");
            return;
        }

        string outputFolder = Path.Combine(outputRoot, packName);
        string iconOutputPath = Path.Combine(outputFolder, $"{packName}_app_icon.ico");

        try
        {
            var existing = _assignments.Keys.Where(name => File.Exists(Path.Combine(outputFolder, name))).ToList();
            bool iconWouldOverwrite = _iconFrameIndex.HasValue && File.Exists(iconOutputPath);
            int overwriteCount = existing.Count + (iconWouldOverwrite ? 1 : 0);

            if (overwriteCount > 0 && !await ConfirmAsync($"Overwrite {overwriteCount} existing file(s) in {outputFolder}?"))
                return;
        }
        catch (Exception ex)
        {
            await ShowMessageAsync("Export failed", $"Could not check existing files: {ex.Message}");
            return;
        }

        string iconMessage = string.Empty;

        try
        {
            Directory.CreateDirectory(outputFolder);

            foreach (var (fileName, frame) in _assignments)
            {
                ImageSlicer.ExportFrame(_sheetImage, frame, Path.Combine(outputFolder, fileName));
            }

            if (_iconFrameIndex.HasValue)
            {
                try
                {
                    var iconFrame = _frames.FirstOrDefault(frame => frame.Index == _iconFrameIndex.Value);
                    if (iconFrame == null)
                        throw new InvalidOperationException("Selected icon frame no longer exists.");

                    var iconSizes = GetSelectedIconSizes();
                    IconExporter.ExportIcon(_sheetImage, iconFrame, iconSizes, iconOutputPath);
                    iconMessage = $" and app icon ({string.Join(", ", iconSizes)})";
                }
                catch (Exception ex)
                {
                    iconMessage = $", but icon export failed: {ex.Message}";
                }
            }
        }
        catch (Exception ex)
        {
            await ShowMessageAsync("Export failed", ex.Message);
            return;
        }

        var missingRequired = new[] { "normal_idle001.png", "normal_talk001.png" }
            .Where(name => !_assignments.ContainsKey(name) && !File.Exists(Path.Combine(outputFolder, name)))
            .ToList();

        _exportStatusText.Text = missingRequired.Count == 0
            ? $"Exported {_assignments.Count} frame(s){iconMessage} to {outputFolder}."
            : $"Exported {_assignments.Count} frame(s){iconMessage}, but missing: {string.Join(", ", missingRequired)}";
    }

    private async Task ExportAnimationGifAsync()
    {
        await ExportAnimationAsync("gif");
    }

    private async Task AddCustomMoodAsync()
    {
        var name = new TextBox { Watermark = "Example: Pointing Finger" };
        var add = PrimaryButton("Add Mood");
        var cancel = SecondaryButton("Cancel");
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8,
            Children = { add, cancel } };
        var dialog = new Window { Title = "Add Custom Mood / Pose", Width = 360, Height = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel { Margin = new Thickness(16), Spacing = 10,
                Children = { new TextBlock { Text = "Name the new mood. Exported frames use its lowercase filename prefix.", TextWrapping = TextWrapping.Wrap }, name, buttons } } };
        add.Click += (_, _) => dialog.Close(name.Text);
        cancel.Click += (_, _) => dialog.Close(null);
        var label = (await dialog.ShowDialog<string?>(this))?.Trim();
        if (string.IsNullOrWhiteSpace(label)) return;
        var prefix = Regex.Replace(label.ToLowerInvariant(), @"[\s-]+", "_");
        if (!Regex.IsMatch(prefix, @"^[a-z][a-z0-9_]{0,63}$") ||
            SpriteCatalog.States.Any(s => s.Prefix.Equals(prefix, StringComparison.OrdinalIgnoreCase)))
        { await ShowMessageAsync("Custom mood", "Use a unique name made of letters, numbers, spaces, or underscores (up to 64 filename characters)."); return; }
        var state = new SpriteState(label, prefix);
        SpriteCatalog.States.Add(state);
        _stateCombo.ItemsSource = null;
        _stateCombo.ItemsSource = SpriteCatalog.States;
        _stateCombo.SelectedItem = state;
    }

    private async Task ExportAnimationSpriteSheetAsync()
    {
        await ExportAnimationAsync("spritesheet");
    }

    private async Task ExportAnimationAsync(string exportKind)
    {
        if (_sheetImage == null)
        {
            await ShowMessageAsync("Animation export failed", "Import and slice a sprite sheet before exporting an animation.");
            return;
        }

        var frames = GetAnimationFrames();
        if (frames.Count == 0)
        {
            await ShowMessageAsync("Animation export failed", "Add at least one frame to the custom animation first.");
            return;
        }

        string animationName = _animationNameText.Text?.Trim() ?? string.Empty;
        string outputRoot = _outputRootText.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(animationName) || string.IsNullOrWhiteSpace(outputRoot))
        {
            await ShowMessageAsync("Animation export failed", "Set an animation name and export folder first.");
            return;
        }

        if (!IsValidPackName(animationName))
        {
            await ShowMessageAsync("Animation export failed", "Animation name can only use letters, numbers, hyphens, and underscores.");
            return;
        }

        bool gif = exportKind is "gif" or "gif-pack";
        if (exportKind == "gif-pack" && (!IsValidPackName(_packNameText.Text?.Trim() ?? "") ||
            string.IsNullOrWhiteSpace(_packNameText.Text)))
        { await ShowMessageAsync("Animation export failed", "Set a valid avatar pack name first."); return; }
        string extension = gif ? ".gif" : "_spritesheet.png";
        string folder = exportKind == "gif-pack" ? Path.Combine(outputRoot, _packNameText.Text!.Trim()) : outputRoot;
        string outputPath = Path.Combine(folder, $"{animationName}{extension}");

        try
        {
            if (File.Exists(outputPath) && !await ConfirmAsync($"Overwrite {Path.GetFileName(outputPath)}?"))
                return;

            Directory.CreateDirectory(folder);
            int speed = ReadAnimationSpeed();

            if (gif)
                AnimationExporter.ExportGif(_sheetImage, frames, speed, outputPath);
            else
                AnimationExporter.ExportSpriteSheet(_sheetImage, frames, outputPath);

            _animateInfoText.Text = gif
                ? $"Exported GIF to {outputPath}."
                : $"Exported sprite sheet to {outputPath}.";
        }
        catch (Exception ex)
        {
            await ShowMessageAsync("Animation export failed", ex.Message);
        }
    }

    private int ReadAnimationSpeed()
    {
        return TryReadPositive(_animateSpeedText.Text, out int speed)
            ? Math.Clamp(speed, 30, 2000)
            : 120;
    }

    private static string FindDefaultExportFolder()
    {
        const string outputFolderName = "output";

        foreach (var startPath in GetDefaultOutputSearchPaths())
        {
            var dir = new DirectoryInfo(startPath);
            while (dir != null)
            {
                string candidate = Path.Combine(dir.FullName, outputFolderName);
                if (Directory.Exists(candidate))
                    return candidate;

                dir = dir.Parent;
            }
        }

        string workingOutput = Path.Combine(Environment.CurrentDirectory, outputFolderName);
        if (!Path.GetFullPath(workingOutput).StartsWith(Path.GetTempPath(), StringComparison.OrdinalIgnoreCase))
            return workingOutput;

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), outputFolderName);
    }

    private static IEnumerable<string> GetDefaultOutputSearchPaths()
    {
        yield return Environment.CurrentDirectory;
        yield return AppContext.BaseDirectory;

        var dir = new DirectoryInfo(Environment.CurrentDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "normal_spritepack.png")) ||
                File.Exists(Path.Combine(dir.FullName, "captain-ragdoll-template-pack.piskel")))
            {
                yield return dir.FullName;
                yield break;
            }
            dir = dir.Parent;
        }
    }

    private void DisposeFramePreviews()
    {
        foreach (var frame in _frames)
            frame.Preview?.Dispose();

        _frames = Array.Empty<SlicedFrame>();
        _selectedFrame = null;
        _framePanel.Children.Clear();
        _playbackImage.Source = null;
        _animatePlaybackImage.Source = null;
        _playbackStrip.Children.Clear();
    }

    private static bool IsValidPackName(string packName)
    {
        return Regex.IsMatch(packName, @"^[A-Za-z0-9_-]+$");
    }

    private static bool TryReadPositive(string? text, out int value)
    {
        return int.TryParse(text, out value) && value > 0;
    }

    private static bool TryReadInt(string? text, out int value)
    {
        return int.TryParse(text, out value);
    }

    private static bool TryReadPositive(NumericUpDown input, out int value)
    {
        value = ReadNumber(input);
        return value > 0;
    }

    private static bool TryReadInt(NumericUpDown input, out int value)
    {
        value = ReadNumber(input);
        return true;
    }

    private static int ReadNumber(NumericUpDown input)
    {
        return (int)Math.Round(input.Value ?? 0);
    }

    private static void SetNumber(NumericUpDown input, int value)
    {
        input.Value = Math.Clamp(value, (int)input.Minimum, (int)input.Maximum);
    }

    private static NumericUpDown SliceNumber(int value, int minimum = 0)
    {
        return new NumericUpDown
        {
            Value = value,
            Minimum = minimum,
            Maximum = 100000,
            Increment = 1,
            FormatString = "0",
            MinWidth = 128,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
    }

    private static string FormatFileSize(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB" };
        double size = bytes;
        int unitIndex = 0;

        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return unitIndex == 0
            ? $"{bytes} {units[unitIndex]}"
            : $"{size:0.#} {units[unitIndex]}";
    }

    private async Task<bool> ConfirmAsync(string message)
    {
        var result = await ShowDialogAsync("Confirm", message, includeCancel: true);
        return result;
    }

    private Task ShowMessageAsync(string title, string message)
    {
        return ShowDialogAsync(title, message, includeCancel: false);
    }

    private async Task<bool> ShowDialogAsync(string title, string message, bool includeCancel)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 420,
            Height = 190,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = Brush("#1d1d1d"),
            Content = new StackPanel
            {
                Margin = new Thickness(18),
                Spacing = 16,
                Children =
                {
                    new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.White },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Spacing = 8,
                        Children =
                        {
                            DialogButton("OK", true),
                            includeCancel ? DialogButton("Cancel", false) : new Border()
                        }
                    }
                }
            }
        };

        bool accepted = !includeCancel;
        foreach (var button in ((StackPanel)((StackPanel)dialog.Content!).Children[1]).Children.OfType<Button>())
        {
            button.Click += (_, _) =>
            {
                accepted = button.Tag is true;
                dialog.Close();
            };
        }

        await dialog.ShowDialog(this);
        return accepted;
    }

    private static Button DialogButton(string text, bool result)
    {
        return new Button
        {
            Content = text,
            Tag = result,
            MinWidth = 82,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };
    }

    private static Border PanelCard(Control child)
    {
        return new Border
        {
            Background = Brush("#1d1d1d"),
            BorderBrush = Brush("#444444"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8),
            Margin = new Thickness(0, 0, 8, 0),
            Child = child
        };
    }

    private static Control ScrollablePanel(Control child)
    {
        return new ScrollViewer
        {
            Content = child,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Padding = new Thickness(0, 4, 0, 0)
        };
    }

    private static TextBlock Heading(string text)
    {
        return new TextBlock
        {
            Text = text,
            FontSize = 15,
            FontWeight = FontWeight.Bold,
            LineHeight = 22,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 4, 0, 0)
        };
    }

    private static TextBlock TabHeader(string text)
    {
        return new TextBlock
        {
            Text = text,
            FontSize = 17,
            LineHeight = 24
        };
    }

    private static Control GridFields(params (string Label, NumericUpDown Box)[] fields)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            RowDefinitions = new RowDefinitions(string.Join(",", Enumerable.Repeat("Auto", fields.Length))),
            ColumnSpacing = 8,
            RowSpacing = 6
        };

        for (int i = 0; i < fields.Length; i++)
        {
            var labelled = Labelled(fields[i].Label, fields[i].Box);
            Grid.SetColumn(labelled, i % 2);
            Grid.SetRow(labelled, i / 2);
            grid.Children.Add(labelled);
        }

        return grid;
    }

    private static Control Labelled(string label, Control control)
    {
        return new StackPanel
        {
            Spacing = 3,
            Children =
            {
                new TextBlock { Text = label, Foreground = Brushes.LightGray, FontSize = 11 },
                control
            }
        };
    }

    private static Button PrimaryButton(string text)
    {
        return new Button
        {
            Content = text,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = Brush("#ffd11a"),
            Foreground = Brushes.Black,
            FontWeight = FontWeight.Bold,
            Padding = new Thickness(10, 7),
            MinHeight = 36
        };
    }

    private static Button SecondaryButton(string text)
    {
        return new Button
        {
            Content = text,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = Brush("#22394d"),
            Foreground = Brushes.Gold,
            BorderBrush = Brushes.Gold,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10, 6),
            MinHeight = 34
        };
    }

    private static Button SecondaryButtonWithClick(string text, Action click)
    {
        var button = SecondaryButton(text);
        button.Click += (_, _) => click();
        return button;
    }

    private static Button CompactSecondaryButton(string text)
    {
        return new Button
        {
            Content = text,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = Brush("#22394d"),
            Foreground = Brushes.Gold,
            BorderBrush = Brushes.Gold,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8, 4),
            MinHeight = 30
        };
    }

    private static Button CompactSecondaryButtonWithClick(string text, Action click)
    {
        var button = CompactSecondaryButton(text);
        button.Click += (_, _) => click();
        return button;
    }

    private static Image PixelArtImage(Bitmap? source, double? width = null, double? height = null, Thickness? margin = null)
    {
        var image = new Image
        {
            Source = source,
            Stretch = Stretch.Uniform
        };

        if (width.HasValue)
            image.Width = width.Value;
        if (height.HasValue)
            image.Height = height.Value;
        if (margin.HasValue)
            image.Margin = margin.Value;

        SetPixelArtRendering(image);
        return image;
    }

    private static void SetPixelArtRendering(Image image)
    {
        RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.None);
    }

    private static IBrush Brush(string color)
    {
        return new SolidColorBrush(Color.Parse(color));
    }
}

public sealed class SpriteSheetPreviewControl : Control
{
    private readonly Pen _gridPen = new(new SolidColorBrush(Color.FromRgb(80, 255, 90)), 1);
    private readonly IBrush _emptyTextBrush = Brushes.LightGray;
    private Bitmap? _sheet;
    private GridSettings? _grid;
    private int _sheetWidth;
    private int _sheetHeight;

    public void SetSheet(Bitmap sheet, int sheetWidth, int sheetHeight)
    {
        _sheet = sheet;
        _sheetWidth = sheetWidth;
        _sheetHeight = sheetHeight;
        InvalidateVisual();
    }

    public void SetGrid(GridSettings? grid)
    {
        _grid = grid == null
            ? null
            : new GridSettings
            {
                Rows = grid.Rows,
                Columns = grid.Columns,
                CellWidth = grid.CellWidth,
                CellHeight = grid.CellHeight,
                OffsetX = grid.OffsetX,
                OffsetY = grid.OffsetY,
                SpacingX = grid.SpacingX,
                SpacingY = grid.SpacingY
            };
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var bounds = Bounds;
        if (_sheet == null || _sheetWidth <= 0 || _sheetHeight <= 0 || bounds.Width <= 0 || bounds.Height <= 0)
        {
            context.DrawText(
                new FormattedText(
                    "Import a sprite sheet to preview it here.",
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    Typeface.Default,
                    12,
                    _emptyTextBrush),
                new Point(8, 8));
            return;
        }

        double scale = Math.Min(bounds.Width / _sheetWidth, bounds.Height / _sheetHeight);
        double imageWidth = _sheetWidth * scale;
        double imageHeight = _sheetHeight * scale;
        double imageX = (bounds.Width - imageWidth) / 2;
        double imageY = (bounds.Height - imageHeight) / 2;
        var imageRect = new Rect(imageX, imageY, imageWidth, imageHeight);

        context.DrawImage(_sheet, imageRect);
        DrawGrid(context, imageRect, scale);
    }

    private void DrawGrid(DrawingContext context, Rect imageRect, double scale)
    {
        if (_grid == null || _grid.Rows <= 0 || _grid.Columns <= 0 || _grid.CellWidth <= 0 || _grid.CellHeight <= 0)
            return;

        var imageBounds = new Rect(0, 0, _sheetWidth, _sheetHeight);
        for (int row = 0; row < _grid.Rows; row++)
        {
            for (int column = 0; column < _grid.Columns; column++)
            {
                double sourceX = _grid.OffsetX + column * (_grid.CellWidth + _grid.SpacingX);
                double sourceY = _grid.OffsetY + row * (_grid.CellHeight + _grid.SpacingY);
                var sourceRect = new Rect(sourceX, sourceY, _grid.CellWidth, _grid.CellHeight);
                var clipped = sourceRect.Intersect(imageBounds);
                if (clipped.Width <= 0 || clipped.Height <= 0)
                    continue;

                var drawRect = new Rect(
                    imageRect.X + clipped.X * scale,
                    imageRect.Y + clipped.Y * scale,
                    clipped.Width * scale,
                    clipped.Height * scale);
                context.DrawRectangle(null, _gridPen, drawRect);
            }
        }
    }
}
