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
        var frames = new List<SlicedFrame>();
        int index = 0;

        for (int row = 0; row < settings.Rows; row++)
        {
            for (int column = 0; column < settings.Columns; column++)
            {
                int x = settings.OffsetX + column * (settings.CellWidth + settings.SpacingX);
                int y = settings.OffsetY + row * (settings.CellHeight + settings.SpacingY);

                if (x < 0 || y < 0 || x + settings.CellWidth > source.Width || y + settings.CellHeight > source.Height)
                {
                    index++;
                    continue;
                }

                var frame = new SlicedFrame
                {
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

    public static void ExportFrame(Image<Rgba32> source, SlicedFrame frame, string outputPath)
    {
        using var clone = source.Clone(ctx => ctx.Crop(new Rectangle(frame.X, frame.Y, frame.Width, frame.Height)));
        clone.SaveAsPng(outputPath);
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
