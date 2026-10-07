using System.Text.Json;
using Full_Metal_Paintball_Carmagnola.Models;
using QRCoder;
using SkiaSharp;

namespace Full_Metal_Paintball_Carmagnola.Services;

public sealed class GiftVoucherRenderer(IWebHostEnvironment env)
{
    public byte[] Render(GiftVoucher voucher, byte[] signature, string format)
    {
        using var output = new MemoryStream();
        if (format == "pdf")
        {
            using var pdf = SKDocument.CreatePdf(output);
            var canvas = pdf.BeginPage(595.28f, 396.85f);
            canvas.Scale(595.28f / 1200, 396.85f / 800);
            Draw(canvas, voucher, signature);
            pdf.EndPage(); pdf.Close();
        }
        else
        {
            int scale = format == "preview" ? 1 : 2;
            using var surface = SKSurface.Create(new SKImageInfo(1200 * scale, 800 * scale));
            surface.Canvas.Scale(scale);
            Draw(surface.Canvas, voucher, signature);
            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Jpeg, 94);
            data.SaveTo(output);
        }
        return output.ToArray();
    }

    private void Draw(SKCanvas c, GiftVoucher v, byte[] signature)
    {
        var m = v.Details;
        var company = JsonSerializer.Deserialize<CompanyProfile>(v.CompanyJson)!;
        using var bold = SKTypeface.FromFile(Path.Combine(env.WebRootPath, "fonts/flyers/BarlowCondensed-Bold.ttf"));
        using var regular = SKTypeface.FromFile(Path.Combine(env.WebRootPath, "fonts/flyers/Barlow-Regular.ttf"));
        using var logo = SKBitmap.Decode(Path.Combine(env.WebRootPath, "img/logo.gif"));
        using var hero = SKBitmap.Decode(Path.Combine(env.WebRootPath, "img/flyers/hero.jpg"));
        using var sign = SKBitmap.Decode(signature);
        var ink = SKColor.Parse("#122b24");
        var cream = SKColor.Parse("#f8f3e7");
        var accent = SKColor.Parse(m.Theme switch { "natale" => "#f4c76b", "valentino" => "#ff8e9d", "compleanno" => "#e0fc55", "ricorrenza" => "#8ee4dc", _ => "#c8f344" });
        c.Clear(ink);
        using var paint = new SKPaint { IsAntialias = true };
        void Rect(float x, float y, float w, float h, SKColor color) { paint.Color = color; c.DrawRect(x,y,w,h,paint); }
        void Text(string? s, float x, float y, float w, float h, float size, bool heavy = false, SKColor? color = null) => FlyerRenderer.Text(c, s, SKRect.Create(x,y,w,h), heavy ? bold : regular, color ?? cream, size, 10);
        if (hero != null)
        {
            c.Save(); c.ClipRect(new(750,0,1200,480));
            var ratio = Math.Max(450f / hero.Width, 480f / hero.Height);
            c.DrawBitmap(hero, SKRect.Create(750 + (450 - hero.Width * ratio) / 2, 0, hero.Width * ratio, hero.Height * ratio));
            c.Restore();
        }
        using (var shade = new SKPaint { Shader = SKShader.CreateLinearGradient(new(690,0),new(1200,0),[ink, ink.WithAlpha(0)],SKShaderTileMode.Clamp) }) c.DrawRect(690,0,510,480,shade);
        Rect(40, 40, 6, 42, accent);
        Text(company.Name, 60, 38, 810, 46, 25, true);
        paint.Color = SKColors.White; c.DrawRoundRect(new(1060,25,1175,140),12,12,paint);
        if (logo != null) Fit(c, logo, new(1070,35,1165,130));
        Text("BUONO REGALO / PAINTBALL EXPERIENCE", 44, 106, 720, 28, 18, false, accent);
        var headline = m.Theme switch { "compleanno" => "UN COMPLEANNO\nDA GIOCARE.", "natale" => "QUESTO NATALE,\nREGALA AZIONE.", "valentino" => "COMPLICI.\nANCHE SUL CAMPO.", "ricorrenza" => "UN'OCCASIONE\nDA RICORDARE.", _ => "REGALA UNA\nSCARICA DI ADRENALINA." };
        Text(headline, 40, 150, 755, 145, 68, true, accent);
        Text("PER " + m.Recipient, 44, 315, 760, 54, 36, true);
        Text(m.Dedication, 44, 379, 690, 62, 21);
        Rect(0,465,1200,335,cream);
        Rect(0,452,1200,13,accent);
        Text(m.Summary.ToUpperInvariant(),44,480,890,42,30,true,ink);
        Text(m.Mode == "amount" ? "Scegli la tua esperienza al campo. Nessun pacchetto prestabilito."
            : (m.Unlimited ? "Colpi illimitati" : "Colpi standard") + (string.IsNullOrWhiteSpace(m.PackageExtras) ? "" : " / " + m.PackageExtras),44,528,860,42,19,false,ink);
        Text($"EMESSO IL {v.IssuedOn:dd/MM/yyyy}  /  VALIDO FINO AL {v.ExpiresOn:dd/MM/yyyy}",44,577,850,30,22,true,ink);
        Text(m.Mode == "amount" ? "Unico utilizzo integrale, senza credito residuo. Prenotazione necessaria."
            : "Da utilizzare interamente in un'unica occasione. Prenotazione necessaria.",44,611,850,24,16,false,ink);
        Text(string.IsNullOrWhiteSpace(m.From) ? "Buon divertimento!" : "Un regalo da " + m.From,44,647,430,32,21,false,ink);
        if (m.ShowAmount && m.Mode != "amount") Text($"Valore: {m.Amount:0.00} euro",44,680,390,28,20,true,ink);
        if (sign != null) Fit(c, sign, new(560,647,885,717));
        using var generator = new QRCodeGenerator();
        using var qrData = generator.CreateQrCode(v.Code, QRCodeGenerator.ECCLevel.M);
        using var qr = new PngByteQRCode(qrData);
        using var qrBitmap = SKBitmap.Decode(qr.GetGraphic(8));
        c.DrawBitmap(qrBitmap, new SKRect(961,488,1161,688));
        Text(v.Code,925,697,268,26,18,true,ink);
        Rect(44,733,1112,1,ink.WithAlpha(45));
        var contacts = string.Join(" | ", new[] { company.FieldAddress, company.Phone, company.Email }.Where(x => !string.IsNullOrWhiteSpace(x)));
        var social = string.Join(" | ", new[] { company.Website, company.Instagram,
            company.FacebookPage == null ? null : "Facebook: " + company.FacebookPage,
            company.TaxCode == null ? null : "C.F. " + company.TaxCode }.Where(x => !string.IsNullOrWhiteSpace(x)));
        Text(contacts + "\n" + social,44,745,1112,43,16,false,ink);
        if (v.Status != "Disponibile")
        {
            Rect(0,0,1200,30,SKColor.Parse("#a12530"));
            Text("NON UTILIZZABILE - " + v.Status.ToUpperInvariant(),44,3,1000,24,19,true,SKColors.White);
        }
    }

    private static void Fit(SKCanvas c, SKBitmap bitmap, SKRect box)
    {
        var ratio = Math.Min(box.Width / bitmap.Width, box.Height / bitmap.Height);
        c.DrawBitmap(bitmap, SKRect.Create(box.MidX - bitmap.Width * ratio / 2, box.MidY - bitmap.Height * ratio / 2, bitmap.Width * ratio, bitmap.Height * ratio));
    }
}
