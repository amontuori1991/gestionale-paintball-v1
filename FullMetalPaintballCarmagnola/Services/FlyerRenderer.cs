using Full_Metal_Paintball_Carmagnola.Models;
using QRCoder;
using SkiaSharp;

namespace Full_Metal_Paintball_Carmagnola.Services;

public sealed class FlyerRenderer(IWebHostEnvironment env)
{
    private const float W = 794, H = 1123;
    public byte[] Render(FlyerRequest model, CompanyProfile company)
    {
        using var bold = SKTypeface.FromFile(Path.Combine(env.WebRootPath, "fonts/flyers/BarlowCondensed-Bold.ttf"));
        using var regular = SKTypeface.FromFile(Path.Combine(env.WebRootPath, "fonts/flyers/Barlow-Regular.ttf"));
        using var logo = SKBitmap.Decode(Path.Combine(env.WebRootPath, "img/logo.gif")) ?? throw new InvalidOperationException("Logo globale non disponibile.");
        using var output = new MemoryStream();
        if (model.Format == "pdf")
        {
            using var pdf = SKDocument.CreatePdf(output);
            var canvas = pdf.BeginPage(595.276f, 841.89f);
            canvas.Scale(595.276f / W, 841.89f / H);
            Draw(canvas, model, company, bold, regular, logo);
            pdf.EndPage(); pdf.Close();
        }
        else
        {
            var width = model.Format == "preview" ? 794 : 2480;
            var height = model.Format == "preview" ? 1123 : 3508;
            using var surface = SKSurface.Create(new SKImageInfo(width, height));
            surface.Canvas.Scale(width / W, height / H);
            Draw(surface.Canvas, model, company, bold, regular, logo);
            using var image = surface.Snapshot();
            using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, model.Format == "preview" ? 80 : 95);
            encoded.SaveTo(output);
        }
        return output.ToArray();
    }

    private static void Draw(SKCanvas c, FlyerRequest m, CompanyProfile p, SKTypeface bold, SKTypeface regular, SKBitmap logo)
    {
        var light = m.Theme == "sun";
        var bg = SKColor.Parse(light ? "#fff4db" : "#111d19");
        var ink = SKColor.Parse(light ? "#17251c" : "#fff8ec");
        var accent = SKColor.Parse(m.Theme == "ice" ? "#65e8ef" : light ? "#ff5731" : "#d6fa43");
        var punch = SKColor.Parse(m.Theme == "ice" ? "#ff714e" : "#f12a7a");
        c.Clear(bg);
        using var paint = new SKPaint { IsAntialias = true };
        void Rect(float x, float y, float w, float h, SKColor color) { paint.Color = color; paint.Style = SKPaintStyle.Fill; c.DrawRect(x,y,w,h,paint); }
        // Deterministic paint marks and target rings keep exported files identical to the preview.
        paint.Color = accent.WithAlpha(22); paint.Style = SKPaintStyle.Stroke; paint.StrokeWidth = 1.5f;
        for (var r = 80; r < 500; r += 48) c.DrawCircle(690, 300, r, paint);
        var random = new Random(17);
        paint.Style = SKPaintStyle.Fill;
        for (var i = 0; i < 55; i++)
        {
            paint.Color = (i % 2 == 0 ? accent : punch).WithAlpha(100);
            c.DrawCircle(random.Next(530, 795), random.Next(80, 630), random.Next(2, 12), paint);
        }
        Rect(38, 37, 7, 55, accent);
        Text(c, p.Name, new(58, 38, 575, 104), bold, ink, 24, 12);
        paint.Color = SKColors.White;
        c.DrawRoundRect(new SKRect(637, 29, 753, 145), 18, 18, paint);
        var scale = Math.Min(100f / logo.Width, 100f / logo.Height);
        c.DrawBitmap(logo, SKRect.Create(695 - logo.Width * scale / 2, 87 - logo.Height * scale / 2, logo.Width * scale, logo.Height * scale));
        Text(c, m.Badge, new(46, 150, 730, 181), regular, accent, 17, 13);
        Text(c, m.Title.ToUpperInvariant(), new(42, 200, 730, 435), bold, ink, 112, 45);
        Text(c, m.Subtitle, new(46, 448, 686, 516), regular, ink, 28, 19);
        c.Save(); c.RotateDegrees(-3, 397, 571);
        Rect(-15, 534, 830, 77, accent);
        Text(c, m.Offer, new(46, 545, 745, 597), bold, bg, 36, 18);
        c.Restore();
        Text(c, m.Event, new(46, 646, 744, 690), bold, accent, 27, 17);
        Text(c, m.Details, new(46, 704, 740, 812), regular, ink, 24, 16);
        Rect(0, 846, W, H - 846, light ? SKColor.Parse("#17251c") : SKColor.Parse("#f6f3e8"));
        var footerInk = light ? SKColors.White : SKColor.Parse("#17251c");
        Text(c, m.CallToAction, new(46, 865, 588, 908), bold, footerInk, 32, 17);
        var contacts = new[] { p.Phone, p.Email, p.FieldAddress, p.Website, p.Instagram, p.FacebookPage == null ? null : "Facebook: " + p.FacebookPage, p.TaxCode == null ? null : "C.F. " + p.TaxCode };
        Text(c, string.Join("\n", contacts.Where(s => !string.IsNullOrWhiteSpace(s))), new(46, 922, 586, 1082), regular, footerInk, 18, 10);
        if (Uri.TryCreate(p.Website, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
        {
            using var generator = new QRCodeGenerator();
            using var data = generator.CreateQrCode(p.Website, QRCodeGenerator.ECCLevel.M);
            using var qr = new PngByteQRCode(data);
            using var bitmap = SKBitmap.Decode(qr.GetGraphic(8));
            c.DrawBitmap(bitmap, new SKRect(611, 919, 749, 1057));
            Text(c, "SCOPRI DI PIU", new(611, 1060, 749, 1085), bold, footerInk, 16, 12);
        }
    }

    private static void Text(SKCanvas canvas, string? text, SKRect box, SKTypeface typeface, SKColor color, float max, float min)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        using var paint = new SKPaint { Color = color, IsAntialias = true };
        using var font = new SKFont(typeface, max);
        List<string> lines = new();
        for (var size = max; size >= min; size--)
        {
            font.Size = size; lines.Clear();
            foreach (var paragraph in text.Replace("\r", "").Split('\n'))
            {
                var line = "";
                foreach (var word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    var next = line.Length == 0 ? word : line + " " + word;
                    if (font.MeasureText(next) <= box.Width) { line = next; continue; }
                    if (line.Length > 0) { lines.Add(line); line = ""; }
                    foreach (var ch in word)
                    {
                        if (font.MeasureText(line + ch) > box.Width) { lines.Add(line); line = ""; }
                        line += ch;
                    }
                }
                lines.Add(line);
            }
            if (lines.Count * size * 1.15f <= box.Height) break;
            if (size == min) throw new InvalidDataException("Troppo testo per il volantino. Accorcia i contenuti o i dati di contatto.");
        }
        var y = box.Top - font.Metrics.Ascent;
        foreach (var line in lines) { canvas.DrawText(line, box.Left, y, font, paint); y += font.Size * 1.15f; }
    }
}
