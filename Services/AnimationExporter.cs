using AvatarPackBuilder.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace AvatarPackBuilder.Services;

public static class AnimationExporter
{
    public static void ExportSpriteSheet(IReadOnlyList<SlicedFrame> frames, string outputPath)
    {
        if (frames.Count == 0)
            throw new InvalidOperationException("Add at least one animation frame before exporting.");

        var (cellWidth, cellHeight) = GetCellSize(frames);
        using var output = new Image<Rgba32>(cellWidth * frames.Count, cellHeight, Color.Transparent);

        for (int i = 0; i < frames.Count; i++)
        {
            using var frame = ImageSlicer.ExtractFrame(frames[i]);
            var location = CenteredLocation(i, frames[i], cellWidth, cellHeight);
            output.Mutate(ctx => ctx.DrawImage(frame, location, 1f));
        }

        output.SaveAsPng(outputPath);
    }

    public static void ExportGif(IReadOnlyList<SlicedFrame> frames, int speedMs, string outputPath)
    {
        if (frames.Count == 0)
            throw new InvalidOperationException("Add at least one animation frame before exporting.");

        var (cellWidth, cellHeight) = GetCellSize(frames);
        int frameDelay = Math.Max(1, (int)Math.Round(Math.Clamp(speedMs, 30, 2000) / 10.0));
        using var gif = CreateGifFrame(frames[0], cellWidth, cellHeight);

        gif.Metadata.GetGifMetadata().RepeatCount = 0;
        gif.Frames.RootFrame.Metadata.GetGifMetadata().FrameDelay = frameDelay;
        gif.Frames.RootFrame.Metadata.GetGifMetadata().DisposalMethod = GifDisposalMethod.RestoreToBackground;

        for (int i = 1; i < frames.Count; i++)
        {
            using var next = CreateGifFrame(frames[i], cellWidth, cellHeight);
            next.Frames.RootFrame.Metadata.GetGifMetadata().FrameDelay = frameDelay;
            next.Frames.RootFrame.Metadata.GetGifMetadata().DisposalMethod = GifDisposalMethod.RestoreToBackground;
            gif.Frames.AddFrame(next.Frames.RootFrame);
        }

        gif.SaveAsGif(outputPath);
    }

    private static Image<Rgba32> CreateGifFrame(SlicedFrame sourceFrame, int cellWidth, int cellHeight)
    {
        var output = new Image<Rgba32>(cellWidth, cellHeight, Color.Transparent);
        using var frame = ImageSlicer.ExtractFrame(sourceFrame);
        output.Mutate(ctx => ctx.DrawImage(frame, CenteredLocation(0, sourceFrame, cellWidth, cellHeight), 1f));
        return output;
    }

    private static (int Width, int Height) GetCellSize(IReadOnlyList<SlicedFrame> frames)
    {
        return (frames.Max(frame => frame.Width), frames.Max(frame => frame.Height));
    }

    private static Point CenteredLocation(int frameIndex, SlicedFrame frame, int cellWidth, int cellHeight)
    {
        int x = (frameIndex * cellWidth) + ((cellWidth - frame.Width) / 2);
        int y = (cellHeight - frame.Height) / 2;
        return new Point(x, y);
    }
}
