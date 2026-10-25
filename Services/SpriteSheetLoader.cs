using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace AvatarPackBuilder.Services;

public static class SpriteSheetLoader
{
    // Match the import picker's supported formats, including for drag and drop.
    // Do not register TIFF or retain source EXIF/ICC metadata in exported packs.
    public static Image<Rgba32> Load(string path)
    {
        var configuration = new Configuration(
            new PngConfigurationModule(), new JpegConfigurationModule(), new BmpConfigurationModule());
        configuration.ImageFormatsManager.SetEncoder(GifFormat.Instance, new GifEncoder());
        return Image.Load<Rgba32>(new DecoderOptions
        {
            Configuration = configuration,
            SkipMetadata = true
        }, path);
    }
}
