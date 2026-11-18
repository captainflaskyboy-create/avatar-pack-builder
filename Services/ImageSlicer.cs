using Avalonia.Media.Imaging;
using AvatarPackBuilder.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace AvatarPackBuilder.Services;

public static class ImageSlicer
{
    public static IReadOnlyList<SlicedFrame> Slice(Image<Rgba32> source, GridSettings settings)
    {
        if (settings.GetBoundsError(source.Width, source.Height) is { } error)
            throw new ArgumentException(error, nameof(settings));

        var frames = new List<SlicedFrame>();
        int index = 0;

        for (int row = 0; row < settings.Rows; row++)
        {
            for (int column = 0; column < settings.Columns; column++)
            {
                int x = settings.OffsetX + column * (settings.CellWidth + settings.SpacingX);
                int y = settings.OffsetY + row * (settings.CellHeight + settings.SpacingY);

                var frame = new SlicedFrame
                {
                    SourceImage = source,
                    Index = index,
                    Row = row,
                    Column = column,
                    X = x,
                    Y = y,
                    Width = settings.CellWidth,
                    Height = settings.CellHeight,
                    Preview = CreatePreviewBitmap(source, x, y, settings.CellWidth, settings.CellHeight, 512)
                };
                frames.Add(frame);
                index++;
            }
        }

        return frames;
    }

    public static Bitmap CreateBitmap(Image<Rgba32> source, SlicedFrame frame, int maxSize)
    {
        return CreatePreviewBitmap(source, frame.X, frame.Y, frame.Width, frame.Height, maxSize);
    }

    public static Image<Rgba32> ExtractFrame(SlicedFrame frame)
    {
        return frame.SourceImage.Clone(ctx => ctx.Crop(new Rectangle(frame.X, frame.Y, frame.Width, frame.Height)));
    }

    public static void ExportFrame(SlicedFrame frame, string outputPath)
    {
        using var clone = ExtractFrame(frame);
        clone.SaveAsPng(outputPath);
    }

    public static SlicedFrame ImportFrame(string path, int index)
    {
        var source = Image.Load<Rgba32>(path);
        try
        {
            // APB imports static images, not animated frame containers.
            if (source.Frames.Count != 1)
                throw new InvalidOperationException("Animated images are not supported as individual frames.");
            return new SlicedFrame
            {
                SourceImage = source, IsImported = true, Index = index,
                Width = source.Width, Height = source.Height,
                Preview = CreatePreviewBitmap(source, 0, 0, source.Width, source.Height, 512)
            };
        }
        catch
        {
            source.Dispose();
            throw;
        }
    }

    private static Bitmap CreatePreviewBitmap(Image<Rgba32> source, int x, int y, int width, int height, int maxSize)
    {
        using var clone = source.Clone(ctx =>
        {
            ctx.Crop(new Rectangle(x, y, width, height));
            int largest = Math.Max(width, height);
            if (largest > maxSize)
            {
                double scale = maxSize / (double)largest;
                ctx.Resize(new ResizeOptions
                {
                    Size = new Size(
                        Math.Max(1, (int)Math.Round(width * scale)),
                        Math.Max(1, (int)Math.Round(height * scale))),
                    Mode = ResizeMode.Stretch,
                    Sampler = KnownResamplers.NearestNeighbor
                });
            }
        });

        using var stream = new MemoryStream();
        clone.SaveAsPng(stream);
        stream.Position = 0;
        return new Bitmap(stream);
    }
}
