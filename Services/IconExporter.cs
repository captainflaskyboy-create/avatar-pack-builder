using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using AvatarPackBuilder.Models;

namespace AvatarPackBuilder.Services;

public static class IconExporter
{
    public static void ExportIcon(Image<Rgba32> sourceSheet, SlicedFrame sourceFrame, IEnumerable<int> iconSizes, string outputIconPath)
    {
        var sizes = iconSizes.Distinct().Where(size => size > 0).OrderBy(size => size).ToList();
        if (sizes.Count == 0)
            throw new InvalidOperationException("Select at least one icon size.");

        using var source = sourceSheet.Clone(ctx => ctx.Crop(new Rectangle(sourceFrame.X, sourceFrame.Y, sourceFrame.Width, sourceFrame.Height)));
        using var stream = File.Create(outputIconPath);
        using var writer = new BinaryWriter(stream);

        writer.Write((ushort)0); // reserved
        writer.Write((ushort)1); // icon
        writer.Write((ushort)sizes.Count);

        var encodedImages = sizes.Select(size => EncodePngIcon(source, size)).ToList();
        int offset = 6 + (16 * encodedImages.Count);

        foreach (var image in encodedImages)
        {
            int size = image.Size;
            writer.Write((byte)(size >= 256 ? 0 : size));
            writer.Write((byte)(size >= 256 ? 0 : size));
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write((ushort)1);
            writer.Write((ushort)32);
            writer.Write(image.Bytes.Length);
            writer.Write(offset);
            offset += image.Bytes.Length;
        }

        foreach (var image in encodedImages)
            writer.Write(image.Bytes);
    }

    private static IconImage EncodePngIcon(Image<Rgba32> source, int size)
    {
        using var clone = source.Clone(ctx =>
        {
            var visibleBounds = GetVisibleBounds(source);
            int paddedX = Math.Max(0, visibleBounds.X - (int)Math.Round(visibleBounds.Width * 0.08));
            int paddedY = Math.Max(0, visibleBounds.Y - (int)Math.Round(visibleBounds.Height * 0.08));
            int paddedRight = Math.Min(source.Width, visibleBounds.Right + (int)Math.Round(visibleBounds.Width * 0.08));
            int paddedBottom = Math.Min(source.Height, visibleBounds.Bottom + (int)Math.Round(visibleBounds.Height * 0.08));
            var paddedBounds = new Rectangle(paddedX, paddedY, Math.Max(1, paddedRight - paddedX), Math.Max(1, paddedBottom - paddedY));

            int square = Math.Max(paddedBounds.Width, paddedBounds.Height);
            square = Math.Min(square, Math.Min(source.Width, source.Height));
            int x = paddedBounds.X + (paddedBounds.Width - square) / 2;
            int y = paddedBounds.Y + (paddedBounds.Height - square) / 2;
            x = Math.Clamp(x, 0, Math.Max(0, source.Width - square));
            y = Math.Clamp(y, 0, Math.Max(0, source.Height - square));

            ctx.Crop(new Rectangle(x, y, square, square));
            ctx.Resize(new ResizeOptions
            {
                Size = new Size(size, size),
                Mode = ResizeMode.Stretch,
                Sampler = KnownResamplers.NearestNeighbor
            });
        });

        using var stream = new MemoryStream();
        clone.SaveAsPng(stream);
        return new IconImage(size, stream.ToArray());
    }

    private static Rectangle GetVisibleBounds(Image<Rgba32> source)
    {
        int minX = source.Width;
        int minY = source.Height;
        int maxX = -1;
        int maxY = -1;

        source.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = 0; x < row.Length; x++)
                {
                    if (row[x].A == 0)
                        continue;

                    minX = Math.Min(minX, x);
                    minY = Math.Min(minY, y);
                    maxX = Math.Max(maxX, x);
                    maxY = Math.Max(maxY, y);
                }
            }
        });

        if (maxX < minX || maxY < minY)
            return new Rectangle(0, 0, source.Width, source.Height);

        return new Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    private sealed record IconImage(int Size, byte[] Bytes);
}
