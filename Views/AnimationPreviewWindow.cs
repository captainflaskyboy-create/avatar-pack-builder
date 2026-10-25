using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace AvatarPackBuilder.Views;

internal sealed record AnimationPreviewFrame(string Label, Bitmap? Image, bool Missing, int Width, int Height);

public sealed class AnimationPreviewWindow : Window
{
    private readonly MainWindow _owner;
    private readonly DispatcherTimer _timer;
    private readonly ComboBox _modeCombo = new();
    private readonly Image _previewImage = new() { Stretch = Stretch.Uniform };
    private readonly TextBlock _infoText = new() { Foreground = Brushes.LightGray, TextWrapping = TextWrapping.Wrap };
    private readonly TextBox _speedText = new() { Text = "120", Width = 82 };
    private readonly Button _playPauseButton = SecondaryButton("Play");
    private IReadOnlyList<AnimationPreviewFrame> _frames = Array.Empty<AnimationPreviewFrame>();
    private int _index;
    private bool _isPlaying;

    public AnimationPreviewWindow(MainWindow owner)
    {
        _owner = owner;
        Title = "Animation Preview";
        Width = 620;
        Height = 620;
        MinWidth = 420;
        MinHeight = 420;
        Background = Brush("#151515");
        RenderOptions.SetBitmapInterpolationMode(_previewImage, BitmapInterpolationMode.None);

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
        _timer.Tick += (_, _) => Advance(1);

        Content = BuildUi();
        _owner.PreviewDataChanged += OwnerPreviewDataChanged;
    }

    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop();
        _owner.PreviewDataChanged -= OwnerPreviewDataChanged;
        base.OnClosed(e);
    }

    public void RefreshPreview()
    {
        _frames = IsRawMode()
            ? _owner.GetRawPreviewFrames()
            : _owner.GetAssignedPreviewFrames();

        if (_index >= _frames.Count)
            _index = 0;

        ShowFrame();
    }

    private Control BuildUi()
    {
        var root = new DockPanel();

        var top = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(12),
            VerticalAlignment = VerticalAlignment.Center
        };

        _modeCombo.ItemsSource = new[] { "Raw Sliced Frames", "Assigned Animation" };
        _modeCombo.SelectedIndex = 0;
        _modeCombo.MinWidth = 180;
        _modeCombo.SelectionChanged += (_, _) =>
        {
            _index = 0;
            RefreshPreview();
        };
        top.Children.Add(_modeCombo);

        var previousButton = SecondaryButton("Prev");
        previousButton.Click += (_, _) => Advance(-1);
        top.Children.Add(previousButton);

        _playPauseButton.Click += (_, _) => TogglePlayback();
        top.Children.Add(_playPauseButton);

        var nextButton = SecondaryButton("Next");
        nextButton.Click += (_, _) => Advance(1);
        top.Children.Add(nextButton);

        top.Children.Add(new TextBlock
        {
            Text = "Speed ms",
            Foreground = Brushes.LightGray,
            VerticalAlignment = VerticalAlignment.Center
        });
        top.Children.Add(_speedText);

        DockPanel.SetDock(top, Dock.Top);
        root.Children.Add(top);

        var bottom = new Border
        {
            Padding = new Thickness(12),
            BorderBrush = Brush("#444444"),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Child = _infoText
        };
        DockPanel.SetDock(bottom, Dock.Bottom);
        root.Children.Add(bottom);

        root.Children.Add(new Border
        {
            Margin = new Thickness(12, 0, 12, 12),
            Padding = new Thickness(10),
            Background = Brush("#252525"),
            BorderBrush = Brush("#444444"),
            BorderThickness = new Thickness(1),
            Child = _previewImage
        });

        return root;
    }

    private void OwnerPreviewDataChanged(object? sender, EventArgs e)
    {
        RefreshPreview();
    }

    private bool IsRawMode()
    {
        return _modeCombo.SelectedIndex == 0;
    }

    private void TogglePlayback()
    {
        if (_isPlaying)
        {
            _timer.Stop();
            _isPlaying = false;
            _playPauseButton.Content = "Play";
            return;
        }

        UpdateSpeed();
        _timer.Start();
        _isPlaying = true;
        _playPauseButton.Content = "Pause";
    }

    private void Advance(int delta)
    {
        if (_frames.Count == 0)
            return;

        _index = (_index + delta + _frames.Count) % _frames.Count;
        ShowFrame();
    }

    private void ShowFrame()
    {
        if (_frames.Count == 0)
        {
            _previewImage.Source = null;
            _infoText.Text = "Import and slice a sheet to preview animation.";
            return;
        }

        var frame = _frames[_index];
        _previewImage.Source = frame.Image;
        _infoText.Text = frame.Missing
            ? $"{_index + 1}/{_frames.Count}  {frame.Label}"
            : $"{_index + 1}/{_frames.Count}  {frame.Label}  {frame.Width}x{frame.Height}px";
    }

    private void UpdateSpeed()
    {
        if (!int.TryParse(_speedText.Text, out int speed))
            speed = 120;

        _timer.Interval = TimeSpan.FromMilliseconds(Math.Clamp(speed, 30, 2000));
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

    private static IBrush Brush(string color)
    {
        return new SolidColorBrush(Color.Parse(color));
    }
}
