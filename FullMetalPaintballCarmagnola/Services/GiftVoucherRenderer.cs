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
        var ink = SKColor.Parse(m.Theme switch { "compleanno" => "#172c51", "valentino" => "#621e36", "ricorrenza" => "#252a36", _ => "#122b24" });
        var cream = SKColor.Parse(m.Theme == "valentino" ? "#fff1ea" : "#f8f3e7");
        var accent = SKColor.Parse(m.Theme switch { "natale" => "#f4c76b", "valentino" => "#ffb5ba", "compleanno" => "#ffdc69", "ricorrenza" => "#e3c68b", _ => "#c8f344" });
        c.Clear(ink);
        using var paint = new SKPaint { IsAntialias = true };
        void Rect(float x, float y, float w, float h, SKColor color) { paint.Color = color; c.DrawRect(x,y,w,h,paint); }
        void Text(string? s, float x, float y, float w, float h, float size, bool heavy = false, SKColor? color = null) => FlyerRenderer.Text(c, s, SKRect.Create(x,y,w,h), heavy ? bold : regular, color ?? cream, size, 10);
        DrawTheme(c, m.Theme, hero, ink, accent);
        Rect(40, 40, 6, 42, accent);
        Text(company.Name, 60, 38, 810, 46, 25, true);
        paint.Color = SKColors.White; c.DrawRoundRect(new(1060,25,1175,140),12,12,paint);
        if (logo != null) Fit(c, logo, new(1070,35,1165,130));
        Text("BUONO REGALO / PAINTBALL EXPERIENCE", 44, 106, 720, 28, 18, false, accent);
        var headline = m.Theme switch { "compleanno" => "OGGI SI FESTEGGIA.\nSUL CAMPO.", "natale" => "SOTTO L'ALBERO,\nUN'AVVENTURA.", "valentino" => "Un'avventura\nda vivere insieme.", "ricorrenza" => "I MOMENTI SPECIALI\nSI VIVONO.", _ => "REGALA UNA\nSCARICA DI ADRENALINA." };
        Text(headline, 40, 150, 735, 145, m.Theme == "valentino" ? 54 : 68, m.Theme != "valentino", accent);
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

    // Vector decorations stay sharp in PDF and use deterministic positions in every export.
    private static void DrawTheme(SKCanvas c, string theme, SKBitmap? hero, SKColor ink, SKColor accent)
    {
        using var p = new SKPaint { IsAntialias = true };
        void Line(float x, float y, float xx, float yy, SKColor color, float width = 2)
        { p.Color=color; p.StrokeWidth=width; c.DrawLine(x,y,xx,yy,p); }
        void Circle(float x,float y,float r,SKColor color){p.Color=color;c.DrawCircle(x,y,r,p);}
        void Polygon(SKColor color, params SKPoint[] points)
        { using var path=new SKPath();path.AddPoly(points);p.Color=color;c.DrawPath(path,p); }
        void Photo(SKPath clip, SKRect box)
        {
            if(hero==null)return;
            c.Save();c.ClipPath(clip,SKClipOperation.Intersect,true);
            var ratio=Math.Max(box.Width/hero.Width,box.Height/hero.Height);
            c.DrawBitmap(hero,SKRect.Create(box.MidX-hero.Width*ratio/2,box.MidY-hero.Height*ratio/2,hero.Width*ratio,hero.Height*ratio));c.Restore();
        }
        void Heart(float x,float y,float size,SKColor color)
        {
            using var path=new SKPath();path.MoveTo(x,y+size);
            path.CubicTo(x-size*1.4f,y,x-size*.7f,y-size*.8f,x,y-size*.25f);
            path.CubicTo(x+size*.7f,y-size*.8f,x+size*1.4f,y,x,y+size);path.Close();p.Color=color;c.DrawPath(path,p);
        }
        void Gift(float x,float y,float size,SKColor color)
        {
            p.Color=color;c.DrawRoundRect(SKRect.Create(x,y,size,size*.7f),5,5,p);
            p.Color=accent;c.DrawRect(x+size*.43f,y,size*.14f,size*.7f,p);c.DrawRect(x-5,y-9,size+10,14,p);
            p.Style=SKPaintStyle.Stroke;p.StrokeWidth=5;
            c.DrawOval(SKRect.Create(x+size*.12f,y-size*.29f,size*.38f,size*.24f),p);
            c.DrawOval(SKRect.Create(x+size*.5f,y-size*.29f,size*.38f,size*.24f),p);p.Style=SKPaintStyle.Fill;
        }
        if(theme=="compleanno")
        {
            Circle(1050,290,245,SKColor.Parse("#ea6381"));
            using var frame=new SKPath();frame.AddCircle(1000,290,153);Photo(frame,new(847,137,1153,443));
            var random=new Random(42);
            SKColor[] colors=[accent,SKColor.Parse("#7bded1"),SKColor.Parse("#ff87ad")];
            for(int i=0;i<60;i++){
                float x=random.Next(795,1200),y=random.Next(145,449);
                if(Math.Pow(x-1000,2)+Math.Pow(y-290,2)<165*165)continue;
                c.Save();c.RotateDegrees(random.Next(180),x,y);p.Color=colors[i%3];c.DrawRect(x,y,6,16,p);c.Restore();
            }
            for(int i=0;i<3;i++){
                float x=815+i*45,y=165+i%2*35;
                Line(x,y+25,x+12,340,accent.WithAlpha(140));p.Color=colors[i];c.DrawOval(SKRect.Create(x-22,y-30,44,58),p);
            }
        }
        else if(theme=="natale")
        {
            for(int i=0;i<3;i++){
                float x=875+i*135;
                Polygon(SKColor.Parse(i%2==0?"#23513c":"#306148"),new(x,115),new(x-115,435),new(x+115,435));
                for(int j=0;j<4;j++)Circle(x+(j%2==0?-1:1)*(20+j*10),225+j*48,5,accent);
            }
            Line(981,35,981,134,accent);
            Circle(981,252,122,accent);
            using var frame=new SKPath();frame.AddCircle(981,252,115);Photo(frame,new(866,137,1096,367));
            p.Color=accent;c.DrawRoundRect(SKRect.Create(965,124,32,15),3,3,p);
            Gift(823,373,75,SKColor.Parse("#b63b43"));Gift(1065,370,95,SKColor.Parse("#973841"));
            for(int i=0;i<27;i++)Circle(800+(i*71)%380,65+(i*47)%380,2,SKColors.White.WithAlpha(170));
        }
        else if(theme=="valentino")
        {
            Polygon(SKColor.Parse("#81314c"),new(870,0),new(1200,0),new(1200,452),new(770,452));
            Heart(1000,254,145,accent);
            using var frame=new SKPath();frame.MoveTo(1000,388);
            frame.CubicTo(818,254,909,150,1000,220);
            frame.CubicTo(1091,150,1182,254,1000,388);frame.Close();Photo(frame,new(860,168,1140,388));
            Heart(840,147,23,SKColor.Parse("#ee7288"));Heart(1150,365,27,accent);Heart(838,378,16,accent);
            Line(790,422,1160,422,accent.WithAlpha(120));
        }
        else if(theme=="ricorrenza")
        {
            Polygon(SKColor.Parse("#383d49"),new(850,0),new(1200,0),new(1200,452),new(760,452));
            for(int i=0;i<4;i++){
                p.Color=accent.WithAlpha((byte)(60+i*30));p.Style=SKPaintStyle.Stroke;p.StrokeWidth=1;
                c.DrawRect(823+i*12,158+i*12,340-i*24,276-i*24,p);p.Style=SKPaintStyle.Fill;
            }
            using var frame=new SKPath();frame.AddRect(new(873,192,1113,379));Photo(frame,new(873,192,1113,379));
            Gift(790,353,92,SKColor.Parse("#535968"));
            for(int i=0;i<7;i++){float x=810+i*57,y=95+i%2*30;Line(x-5,y,x+5,y,accent);Line(x,y-5,x,y+5,accent);}
        }
        else
        {
            using var frame=new SKPath();frame.AddRect(new(750,0,1200,452));Photo(frame,new(750,0,1200,452));
            using(var shade=new SKPaint{Shader=SKShader.CreateLinearGradient(new(690,0),new(1100,0),[ink,ink.WithAlpha(0)],SKShaderTileMode.Clamp)})c.DrawRect(690,0,510,452,shade);
            var random=new Random(7);
            for(int i=0;i<40;i++){float x=random.Next(830,1200),y=random.Next(155,452);Circle(x,y,random.Next(2,9),accent.WithAlpha(180));}
            Polygon(accent,new(1110,452),new(1160,310),new(1176,310),new(1132,452));
        }
    }

    private static void Fit(SKCanvas c, SKBitmap bitmap, SKRect box)
    {
        var ratio = Math.Min(box.Width / bitmap.Width, box.Height / bitmap.Height);
        c.DrawBitmap(bitmap, SKRect.Create(box.MidX - bitmap.Width * ratio / 2, box.MidY - bitmap.Height * ratio / 2, bitmap.Width * ratio, bitmap.Height * ratio));
    }
}
