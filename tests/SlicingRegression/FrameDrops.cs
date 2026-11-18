using Avalonia.Controls;
using AvatarPackBuilder.Models;
using AvatarPackBuilder.Services;
using AvatarPackBuilder.Views;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.PixelFormats;

internal static partial class Program
{
    private static void CheckFrameDrops()
    {
        string folder = Path.Combine(Path.GetTempPath(), "apb-frame-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(folder);
        var window = new MainWindow();
        try
        {
            string png = Path.Combine(folder, "red.png");
            string bmp = Path.Combine(folder, "blue.bmp");
            string jpg = Path.Combine(folder, "green.jpg");
            string bad = Path.Combine(folder, "broken.png");
            string unsupported = Path.Combine(folder, "notes.txt");
            using (var image = new Image<Rgba32>(8, 6, new Rgba32(255, 0, 0)))
            {
                image[0, 0] = new Rgba32(0, 0, 0, 0);
                image.SaveAsPng(png);
            }
            using (var image = new Image<Rgba32>(12, 10, new Rgba32(0, 0, 255))) image.SaveAsBmp(bmp);
            using (var image = new Image<Rgba32>(4, 4, new Rgba32(0, 255, 0))) image.SaveAsJpeg(jpg);
            File.WriteAllText(bad, "invalid image");
            File.WriteAllText(unsupported, "not an image");

            var errors = (IReadOnlyList<string>)Invoke(window, "AppendImageFiles", (object)new[] { png, bad, bmp, unsupported, jpg })!;
            var frames = Field<IReadOnlyList<SlicedFrame>>(window, "_frames");
            Check(frames.Count == 3 && errors.Count == 2 && frames.All(f => f.IsImported),
                "Mixed batch imports PNG/BMP/JPEG and reports bad/unsupported files");
            Check(frames[0].Width == 8 && frames[1].Width == 12 && frames[2].Width == 4 &&
                Field<SlicedFrame>(window, "_selectedFrame") == frames[0], "Native sizes/order retained; first added frame selected");
            Check(Field<WrapPanel>(window, "_framePanel").Children.Count == 3 &&
                Field<object?>(window, "_sheetImage") == null, "Frame-only session creates selectable tiles without a sheet");
            var assignments = Field<Dictionary<string, SlicedFrame>>(window, "_assignments");
            SetField(window, "_selectedFrame", frames[0]);
            Invoke(window, "AssignSelectedFrame", "normal_idle001.png");
            SetField(window, "_selectedFrame", frames[1]);
            Invoke(window, "AssignSelectedFrame", "normal_talk001.png");
            Check(assignments.Count == 2, "Individual images accept sprite-state roles");
            Invoke(window, "AddFrameToAnimation", frames[0]);
            Invoke(window, "AddFrameToAnimation", frames[1]);
            Invoke(window, "UseSelectedFrameAsIcon");
            Guid iconId = Field<Guid?>(window, "_iconFrameId")!.Value;
            var ids = Field<List<Guid>>(window, "_animationFrameIds").ToArray();
            Invoke(window, "ApplyGridFromUi");
            Check(Field<IReadOnlyList<SlicedFrame>>(window, "_frames").Count == 3 && assignments.Count == 2,
                "Applying grid without a sheet preserves imported frames and roles");

            Field<TextBox>(window, "_packNameText").Text = "image_only";
            Field<TextBox>(window, "_outputRootText").Text = folder;
            ((Task)Invoke(window, "ExportPackAsync")!).GetAwaiter().GetResult();
            using (var output = SixLabors.ImageSharp.Image.Load<Rgba32>(Path.Combine(folder, "image_only", "normal_idle001.png")))
                Check(output.Width == 8 && output[0, 0].A == 0 && output[1, 1] == new Rgba32(255, 0, 0),
                    "Image-only pack export preserves original pixels and alpha");
            Check(File.Exists(Path.Combine(folder, "image_only", "image_only_app_icon.ico")),
                "Image-only pack exports selected icon");
            ((Task)Invoke(window, "ExportAnimationAsync", "gif")!).GetAwaiter().GetResult();
            Check(File.Exists(Path.Combine(folder, "custom_animation.gif")), "Image-only animation export is enabled");

            Invoke(window, "AppendImageFiles", (object)new[] { png });
            var afterDuplicate = Field<IReadOnlyList<SlicedFrame>>(window, "_frames");
            Check(afterDuplicate.Count == 4 && afterDuplicate[0].Id != afterDuplicate[3].Id &&
                assignments.Count == 2, "Repeated drops append unique frames without changing roles");
            // Source files are no longer needed after import.
            File.WriteAllText(png, "source changed on disk");
            using (var copy = ImageSlicer.ExtractFrame(frames[0]))
                Check(copy[1, 1] == new Rgba32(255, 0, 0), "Imported originals stay available independently of source files");

            var sheet = new Image<Rgba32>(24, 10, new Rgba32(0, 255, 0));
            SetField(window, "_sheetImage", sheet);
            SetGrid(window, "_", 12, 10, 1, 2);
            Invoke(window, "ApplyGridFromUi");
            var mixed = Field<IReadOnlyList<SlicedFrame>>(window, "_frames");
            Check(mixed.Count == 6 && mixed.Take(2).All(f => !f.IsImported) && mixed.Skip(2).All(f => f.IsImported),
                "Re-slicing places sheet frames before retained imported frames");
            Check(assignments["normal_idle001.png"].Id == frames[0].Id &&
                Field<List<Guid>>(window, "_animationFrameIds").SequenceEqual(ids) &&
                Field<Guid?>(window, "_iconFrameId") == iconId,
                "Re-slicing preserves imported roles, animation identities and icon selection");
            var firstSheetId = mixed[0].Id;
            Invoke(window, "AddFrameToAnimation", mixed[0]);
            Invoke(window, "ApplyGridFromUi");
            Check(Field<IReadOnlyList<SlicedFrame>>(window, "_frames")[0].Id == firstSheetId &&
                Field<List<Guid>>(window, "_animationFrameIds").Last() == firstSheetId,
                "Sheet animation identity survives re-slicing by cell position");

            var sequence = new[] { Field<IReadOnlyList<SlicedFrame>>(window, "_frames")[0], frames[0], frames[1] };
            AnimationExporter.ExportSpriteSheet(sequence, Path.Combine(folder, "mixed.png"));
            using (var output = SixLabors.ImageSharp.Image.Load<Rgba32>(Path.Combine(folder, "mixed.png")))
                Check(output.Width == 36 && output.Height == 10 &&
                    output[1, 1] == new Rgba32(0, 255, 0) && output[15, 3] == new Rgba32(255, 0, 0) &&
                    output[25, 1] == new Rgba32(0, 0, 255) && output[12, 0].A == 0,
                    "Mixed-source sprite sheet uses correct centered pixels and transparent padding");
            AnimationExporter.ExportGif(sequence, 120, Path.Combine(folder, "mixed.gif"));
            using (var gif = SixLabors.ImageSharp.Image.Load<Rgba32>(Path.Combine(folder, "mixed.gif")))
                Check(gif.Frames.Count == 3 && gif.Frames[1][3, 3].R == 255 &&
                    gif.Frames[2][1, 1].B == 255 && gif.Frames[1].Metadata.GetGifMetadata().FrameDelay == 12,
                    "Mixed-source GIF preserves frame order, artwork and timing");

            window.Show();
            var previousFrames = Field<IReadOnlyList<SlicedFrame>>(window, "_frames");
            var failedImport = (Task)Invoke(window, "LoadSheetAsync", bad)!;
            Check(ReferenceEquals(previousFrames, Field<IReadOnlyList<SlicedFrame>>(window, "_frames")) &&
                assignments.Count == 2 && ReferenceEquals(sheet, Field<object>(window, "_sheetImage")),
                "Failed sheet import preserves mixed frames, assignments and source sheet");
            window.OwnedWindows.Single().Close(true);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            failedImport.GetAwaiter().GetResult();

            // Successful sheet import retains the existing start-fresh semantics.
            string newSheet = Path.Combine(folder, "replacement.png");
            sheet.SaveAsPng(newSheet);
            ((Task)Invoke(window, "LoadSheetAsync", newSheet)!).GetAwaiter().GetResult();
            Check(Field<IReadOnlyList<SlicedFrame>>(window, "_frames").Count == 0 && assignments.Count == 0 &&
                Field<List<Guid>>(window, "_animationFrameIds").Count == 0 && Field<Guid?>(window, "_iconFrameId") == null,
                "Importing a new sheet resets frames, assignments, animation and icon");

            Invoke(window, "AppendImageFiles", (object)Enumerable.Repeat(bmp, 80).ToArray());
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Check(Field<WrapPanel>(window, "_framePanel").Children.Count == 80,
                "Large individual-image batch retains all selectable tiles");
        }
        finally
        {
            window.Close();
            Directory.Delete(folder, recursive: true);
        }
    }
}
