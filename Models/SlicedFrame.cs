using Avalonia.Media.Imaging;

namespace AvatarPackBuilder.Models;

public sealed class SlicedFrame
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int Index { get; set; }
    // Source lifetime is owned by the session, not the thumbnail or exporter.
    public SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32> SourceImage { get; init; } = null!;
    public bool IsImported { get; init; }
    public int Row { get; init; }
    public int Column { get; init; }
    public int X { get; init; }
    public int Y { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public Bitmap? Preview { get; set; }
    public List<string> AssignedFileNames { get; } = new();

    public bool HasAssignments => AssignedFileNames.Count > 0;

    public string Label
    {
        get
        {
            if (AssignedFileNames.Count == 0)
                return $"Frame {Index + 1}";

            string first = AssignedFileNames[0];
            return AssignedFileNames.Count == 1
                ? first
                : $"{first}\n+{AssignedFileNames.Count - 1} more";
        }
    }
}
