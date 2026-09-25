using SkiaSharp;
using ImageMagick;
using System.Buffers.Binary;
using System.Text;

namespace Full_Metal_Paintball_Carmagnola.Services;

public sealed class PhotoWatermarker(IWebHostEnvironment environment)
{
    public const long MaxFileBytes = 15 * 1024 * 1024;
    public const int MaxOutputSide = 2200;

    public byte[] Process(Stream input)
    {
        using var buffered = new MemoryStream();
        input.CopyTo(buffered);
        if (buffered.Length > MaxFileBytes) throw new InvalidDataException("Ogni foto deve pesare al massimo 15 MB.");
        var bytes = buffered.ToArray();
        using var normalized = IsHeifStill(bytes) ? ConvertHeif(bytes) : new MemoryStream(bytes);
        return Watermark(normalized);
    }

    private static bool IsHeifStill(byte[] bytes)
    {
        if (bytes.Length < 16 || Encoding.ASCII.GetString(bytes, 4, 4) != "ftyp") return false;
        var size = BinaryPrimitives.ReadUInt32BigEndian(bytes);
        if (size < 16 || size > bytes.Length || size % 4 != 0) return false;
        var image = false;
        for (var offset = 8; offset < size; offset += 4)
        {
            if (offset == 12) continue;
            var brand = Encoding.ASCII.GetString(bytes, offset, 4);
            if (brand is "msf1" or "avis" || offset == 8 && brand is ("hevc" or "hevx"))
                throw new InvalidDataException("Video e sequenze animate non ammessi.");
            image |= brand is "heic" or "heix" or "mif1";
        }
        if (!image) return false;
        // HEIF stills use item metadata; a movie track is never accepted as a photo.
        for (long offset = size; offset + 8 <= bytes.Length;)
        {
            var boxSize = (long)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan((int)offset));
            var type = Encoding.ASCII.GetString(bytes, (int)offset + 4, 4);
            if (type == "moov") throw new InvalidDataException("Video non ammessi.");
            if (boxSize == 1 && offset + 16 <= bytes.Length)
            {
                var extended = BinaryPrimitives.ReadUInt64BigEndian(bytes.AsSpan((int)offset + 8));
                if (extended > (ulong)bytes.Length) break;
                boxSize = (long)extended;
            }
            if (boxSize < 8 || boxSize > bytes.Length - offset) break;
            offset += boxSize;
        }
        return true;
    }

    private static MemoryStream ConvertHeif(byte[] bytes)
    {
        try
        {
            var settings = new MagickReadSettings { Format = MagickFormat.Heic, FrameIndex = 0, FrameCount = 1 };
            using var image = new MagickImage();
            image.Ping(bytes, settings);
            if (image.Width < 16 || image.Height < 16 || (long)image.Width * image.Height > 32_000_000)
                throw new InvalidDataException("La foto deve avere al massimo 32 megapixel.");
            image.Read(bytes, settings);
            image.AutoOrient();
            image.Resize(new MagickGeometry(MaxOutputSide, MaxOutputSide) { Greater = true });
            image.Strip();
            return new MemoryStream(image.ToByteArray(MagickFormat.Png));
        }
        catch (MagickException e) { throw new InvalidDataException("Foto HEIC/HEIF danneggiata o non decodificabile.", e); }
    }

    private byte[] Watermark(Stream input)
    {
        using var codec = SKCodec.Create(input);
        if (codec == null || codec.EncodedFormat is not (SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Png or SKEncodedImageFormat.Webp or SKEncodedImageFormat.Bmp or SKEncodedImageFormat.Gif)
            || codec.FrameCount > 1)
            throw new InvalidDataException("Sono ammesse solo immagini JPG, PNG, WebP, BMP, GIF statiche e HEIC/HEIF. Video esclusi.");
        var info = codec.Info;
        if (info.Width < 16 || info.Height < 16 || (long)info.Width * info.Height > 32_000_000)
            throw new InvalidDataException("La foto deve avere al massimo 32 megapixel.");

        var scale = Math.Min(1f, (float)MaxOutputSide / Math.Max(info.Width, info.Height));
        var dimensions = codec.GetScaledDimensions(scale);
        using var decoded = new SKBitmap(new SKImageInfo(dimensions.Width, dimensions.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        if (codec.GetPixels(decoded.Info, decoded.GetPixels()) != SKCodecResult.Success)
            throw new InvalidDataException("Foto danneggiata o incompleta.");

        // Normalize all eight EXIF orientations before discarding the source metadata.
        var w = decoded.Width;
        var h = decoded.Height;
        var origin = (int)codec.EncodedOrigin;
        var swap = origin >= 5;
        var ow = swap ? h : w;
        var oh = swap ? w : h;
        var outputScale = Math.Min(1f, (float)MaxOutputSide / Math.Max(ow, oh));
        using var output = new SKBitmap(Math.Max(1, (int)(ow * outputScale)), Math.Max(1, (int)(oh * outputScale)));
        using var canvas = new SKCanvas(output);
        canvas.Clear(SKColors.White);
        canvas.Scale(outputScale);
        var matrix = origin switch
        {
            2 => new SKMatrix(-1, 0, w, 0, 1, 0, 0, 0, 1),
            3 => new SKMatrix(-1, 0, w, 0, -1, h, 0, 0, 1),
            4 => new SKMatrix(1, 0, 0, 0, -1, h, 0, 0, 1),
            5 => new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1),
            6 => new SKMatrix(0, -1, h, 1, 0, 0, 0, 0, 1),
            7 => new SKMatrix(0, -1, h, -1, 0, w, 0, 0, 1),
            8 => new SKMatrix(0, 1, 0, -1, 0, w, 0, 0, 1),
            _ => SKMatrix.Identity
        };
        canvas.Concat(in matrix);
        using var paint = new SKPaint { IsAntialias = true };
        canvas.DrawBitmap(decoded, 0, 0, paint);
        canvas.ResetMatrix();
        using var logo = SKBitmap.Decode(Path.Combine(environment.WebRootPath, "img", "logo.gif"))
            ?? throw new InvalidOperationException("Logo non disponibile.");
        var logoWidth = Math.Min(300f, Math.Min(output.Width * .16f, output.Height * .22f));
        var logoHeight = logoWidth * logo.Height / logo.Width;
        var padding = Math.Max(4, Math.Min(output.Width, output.Height) * .02f);
        var rect = SKRect.Create(output.Width - logoWidth - padding, output.Height - logoHeight - padding, logoWidth, logoHeight);
        using var backing = new SKPaint { Color = SKColors.White.WithAlpha(210), IsAntialias = true };
        canvas.DrawRoundRect(SKRect.Create(rect.Left - 3, rect.Top - 3, rect.Width + 6, rect.Height + 6), 6, 6, backing);
        canvas.DrawBitmap(logo, rect, paint);
        using var image = SKImage.FromBitmap(output);
        using var jpeg = image.Encode(SKEncodedImageFormat.Jpeg, 85);
        if (jpeg == null || jpeg.Size > 5 * 1024 * 1024)
            throw new InvalidDataException("Foto troppo complessa: riducine la risoluzione e riprova.");
        return jpeg.ToArray();
    }
}
