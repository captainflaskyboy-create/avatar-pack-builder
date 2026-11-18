using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using AvatarPackBuilder;
using AvatarPackBuilder.Models;
using AvatarPackBuilder.Services;
using AvatarPackBuilder.Views;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

internal static partial class Program
{
    private static int _checks;

    [STAThread]
    private static void Main(string[] args)
    {
        AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().SetupWithoutStarting();
        using var sheet = args.Length > 0
            ? SixLabors.ImageSharp.Image.Load<Rgba32>(args[0])
            : new Image<Rgba32>(944, 1464);
        Check(sheet.Width == 944 && sheet.Height == 1464, "Screenshot sheet dimensions");
        var grid = new GridSettings { Rows = 3, Columns = 2, CellWidth = 474, CellHeight = 488 };
        Check(grid.GetBoundsError(sheet.Width, sheet.Height)?.Contains("472px") == true,
            "Overwide cells suggest 472px");
        Check(Enumerable.Range(0, 6).Count(i => grid.ContainsCell(i / 2, i % 2, 944, 1464)) == 3,
            "Original settings only fit frames 1, 3 and 5");
        bool rejected = false;
        try { ImageSlicer.Slice(sheet, grid); }
        catch (ArgumentException) { rejected = true; }
        Check(rejected, "Slicer rejects partial grids instead of silently dropping cells");

        grid.CellWidth = 472;
        var frames = ImageSlicer.Slice(sheet, grid);
        Check(frames.Count == 6 && frames.Select(f => f.Index).SequenceEqual(Enumerable.Range(0, 6)),
            "Corrected settings return all six consecutive frames");
        Check(frames[5].X == 472 && frames[5].Y == 976 && frames.All(f => f.Preview != null),
            "Bottom-right frame has correct bounds and a selectable preview");
        foreach (var frame in frames) frame.Preview?.Dispose();

        grid.CellHeight = 489;
        Check(grid.GetBoundsError(944, 1464)?.Contains("488px") == true, "Bottom overflow suggests correct height");
        grid.CellHeight = 488;
        grid.OffsetX = 1;
        Check(grid.GetBoundsError(944, 1464) != null, "Offset contributes to bounds");
        grid.OffsetX = 0;
        grid.SpacingX = 1;
        Check(grid.GetBoundsError(944, 1464) != null, "Horizontal gaps contribute to bounds");
        grid.SpacingX = 0;
        grid.SpacingY = 1;
        Check(grid.GetBoundsError(944, 1464) != null, "Vertical gaps contribute to bounds");
        grid.SpacingY = 0;
        Check(grid.GetBoundsError(944, 1464) == null, "Cells exactly on right/bottom edge are accepted");
        grid.Columns = 100000;
        grid.CellWidth = 100000;
        Check(grid.GetBoundsError(944, 1464) != null, "Large grid arithmetic does not wrap");

        var window = new MainWindow();
        SetField(window, "_sheetImage", sheet);
        SetGrid(window, "_import", 474, 488, 3, 2);
        Invoke(window, "RefreshImportPreviewFromUi");
        Check(Field<TextBlock>(window, "_importGridStatusText").Text!.Contains("3/6 whole cells fit"),
            "Preview reports complete cells, not intersecting cells");
        Invoke(window, "UseImportSliceSettings");
        Check(Field<IReadOnlyList<SlicedFrame>>(window, "_frames").Count == 0,
            "Import action blocks invalid grid");
        SetGrid(window, "_import", 472, 488, 3, 2);
        Invoke(window, "UseImportSliceSettings");
        var validFrames = Field<IReadOnlyList<SlicedFrame>>(window, "_frames");
        Check(validFrames.Count == 6 && Field<WrapPanel>(window, "_framePanel").Children.Count == 6,
            "Import action creates all six frame tiles");
        var assignments = Field<Dictionary<string, SlicedFrame>>(window, "_assignments");
        assignments["normal_idle001.png"] = validFrames[0];
        validFrames[0].AssignedFileNames.Add("normal_idle001.png");
        Field<NumericUpDown>(window, "_cellWidthText").Value = 474;
        Invoke(window, "ApplyGridFromUi");
        Check(ReferenceEquals(validFrames, Field<IReadOnlyList<SlicedFrame>>(window, "_frames")) &&
            assignments["normal_idle001.png"] == validFrames[0] && validFrames[0].Preview != null,
            "Invalid reapply preserves frames, previews and assigned roles");
        Check(Field<TextBlock>(window, "_sliceGridStatusText").Text!.Contains("Grid not applied"),
            "Slice action explains rejected settings in the visible workspace");
        SetGrid(window, "_", 472, 488, 3, 2);
        Invoke(window, "ApplyGridFromUi");
        Check(Field<IReadOnlyList<SlicedFrame>>(window, "_frames").Count == 6 &&
            assignments["normal_idle001.png"].Index == 0, "Valid reapply retains roles");
        SetGrid(window, "_", 24, 24, 60, 2);
        Invoke(window, "ApplyGridFromUi");
        Check(Field<WrapPanel>(window, "_framePanel").Children.Count == 120,
            "Tall valid grid creates all 120 selectable tiles");
        Invoke(window, "DisposeFramePreviews");
        SetField(window, "_sheetImage", null);
        window.Close();
        CheckFrameDrops();
        Console.WriteLine($"PASS: {_checks} regression checks.");
    }

    private static void SetGrid(MainWindow window, string prefix, int width, int height, int rows, int columns)
    {
        foreach (var (name, value) in new[] { ("CellWidth", width), ("CellHeight", height), ("Rows", rows), ("Columns", columns) })
        {
            string field = prefix == "_" ? "_" + char.ToLowerInvariant(name[0]) + name[1..] + "Text" : prefix + name + "Text";
            Field<NumericUpDown>(window, field).Value = value;
        }
    }

    private static T Field<T>(object instance, string name) => (T)typeof(MainWindow).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(instance)!;
    private static void SetField(object instance, string name, object? value) => typeof(MainWindow).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(instance, value);
    private static object? Invoke(object instance, string name, params object?[] args) => typeof(MainWindow).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(instance, args);
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("FAIL: " + message);
        _checks++;
        Console.WriteLine("PASS: " + message);
    }
}
