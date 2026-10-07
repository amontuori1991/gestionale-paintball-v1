using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using Full_Metal_Paintball_Carmagnola.Authorization;
using Full_Metal_Paintball_Carmagnola.Data;
using Full_Metal_Paintball_Carmagnola.Models;
using Full_Metal_Paintball_Carmagnola.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using SkiaSharp;

void Check(bool ok, string message) { if(!ok) throw new Exception(message); }
var voucher = new GiftVoucher {IssuedOn=GiftVoucher.Today,ExpiresOn=GiftVoucher.Today.AddYears(1)};
Check(voucher.ChangeState("riscatta",false,"Staff",null)!=null,"Unpaid redemption");
Check(voucher.ChangeState("pagato",false,"Staff",null)!=null,"Staff payment");
Check(voucher.ChangeState("pagato",true,"Admin",null)==null,"Admin payment");
Check(voucher.ChangeState("riscatta",false,"Staff",null)==null,"Staff redemption");
Check(voucher.ChangeState("riscatta",false,"Staff",null)!=null,"Double redemption");
Check(voucher.ChangeState("ripristina",false,"Staff",null)!=null,"Undo without reason");
Check(voucher.ChangeState("ripristina",false,"Staff","Errore di selezione")==null,"Staff undo");
Check(voucher.History.Count==3,"Audit preserved");
voucher.ExpiresOn=GiftVoucher.Today.AddDays(-1);
Check(voucher.ChangeState("riscatta",false,"Staff",null)!=null,"Expired redemption");
voucher.ExpiresOn=GiftVoucher.Today; Check(voucher.Status=="Disponibile","Expiry inclusive");
Check(new DateOnly(2024,2,29).AddYears(1)==new DateOnly(2025,2,28),"Leap anniversary");
Console.WriteLine("PASS: lifecycle, Staff undo, required reason, payment permissions, expiration and leap year.");

const string adminCs="Host=127.0.0.1;Port=55439;Database=postgres;Username=bonus_tests;SSL Mode=Disable";
var database="voucher_checks_"+Guid.NewGuid().ToString("N");
await using var admin=new NpgsqlConnection(adminCs); await admin.OpenAsync();
await new NpgsqlCommand($"CREATE DATABASE {database}",admin).ExecuteNonQueryAsync();
var cs=new NpgsqlConnectionStringBuilder(adminCs){Database=database}.ConnectionString;
var builder=WebApplication.CreateBuilder(new WebApplicationOptions {ApplicationName=typeof(Partita).Assembly.GetName().Name,ContentRootPath=Path.GetFullPath("FullMetalPaintballCarmagnola"),WebRootPath="wwwroot",EnvironmentName="Development"});
builder.Logging.ClearProviders();
builder.Services.AddControllersWithViews().AddApplicationPart(typeof(Partita).Assembly);
builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
builder.Services.AddDbContext<TesseramentoDbContext>(o=>o.UseNpgsql(cs));
builder.Services.AddDbContext<ApplicationDbContext>(o=>o.UseNpgsql(cs));
builder.Services.AddIdentity<ApplicationUser,IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>();
builder.Services.AddAuthentication(o=>{o.DefaultAuthenticateScheme="Test";o.DefaultChallengeScheme="Test";o.DefaultForbidScheme="Test";}).AddScheme<AuthenticationSchemeOptions,TestAuth>("Test",_=>{});
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAuthorizationHandler,FeatureAuthorizationHandler>();
builder.Services.AddAuthorization(o=>o.AddPolicy("Buoni regalo",p=>p.Requirements.Add(new FeatureRequirement("Buoni regalo"))));
builder.Services.AddScoped<CompanyProfileService>(); builder.Services.AddSingleton<GiftVoucherRenderer>();
await using var app=builder.Build(); app.Urls.Add("http://127.0.0.1:55447");
app.UseStaticFiles(); app.UseRouting(); app.UseAuthentication(); app.UseAuthorization();
app.MapControllerRoute("default","{controller}/{action=Index}/{id?}");
try {
    using var scope=app.Services.CreateScope(); var db=scope.ServiceProvider.GetRequiredService<TesseramentoDbContext>();
    await db.Database.EnsureCreatedAsync();
    // Exercise the actual production DDL, not only EF-generated schema.
    await db.Database.ExecuteSqlRawAsync("DROP TABLE \"GiftVouchers\"");
    await GiftVoucherSchema.EnsureAsync(db); await GiftVoucherSchema.EnsureAsync(db);
    var identity=scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    foreach(var sql in identity.Database.GenerateCreateScript().Split(';')) if(sql.Contains("AspNet")) await identity.Database.ExecuteSqlRawAsync(sql);
    await scope.ServiceProvider.GetRequiredService<CompanyProfileService>().SaveAsync(new CompanyProfile {Name="A.S.D. FULL METAL PAINTBALL CARMAGNOLA",FieldAddress="Via Ceis 80 - Carmagnola (TO)",Phone="+39 346 874 1192",Email="paintballcarmagnola@gmail.com",Website="https://www.paintballcarmagnola.com"});
    using var signatureBitmap=new SKBitmap(600,100); using(var canvas=new SKCanvas(signatureBitmap)){canvas.Clear(SKColors.White);using var pen=new SKPaint{Color=SKColors.Black,StrokeWidth=2};canvas.DrawLine(10,60,580,40,pen);}
    using var signatureImage=SKImage.FromBitmap(signatureBitmap); using var signaturePng=signatureImage.Encode(SKEncodedImageFormat.Png,100);
    var signature=signaturePng.ToArray();
    if(args.Contains("--signature")) signature=await File.ReadAllBytesAsync(args[Array.IndexOf(args,"--signature")+1]);
    await app.StartAsync();
    HttpClient Client(string? role){var c=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}){BaseAddress=new Uri("http://127.0.0.1:55447")};if(role!=null)c.DefaultRequestHeaders.Add("X-Test-Role",role);return c;}
    using var adminClient=Client("Admin"); using var staff=Client("Staff"); using var anon=Client(null);
    Check((await anon.GetAsync("/BuoniRegalo")).StatusCode==HttpStatusCode.Unauthorized,"Anonymous access");
    Check((await staff.GetAsync("/BuoniRegalo/Crea")).StatusCode==HttpStatusCode.Forbidden,"Staff create");
    string Token(string html)=>WebUtility.HtmlDecode(Regex.Match(html,"name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
    var index=await adminClient.GetStringAsync("/BuoniRegalo");
    var token=Token(index); Check(token.Length>0,"Antiforgery token");
    using var upload=new MultipartFormDataContent(); upload.Add(new StringContent(token),"__RequestVerificationToken");upload.Add(new ByteArrayContent(signature),"file","signature.png");
    Check((await adminClient.PostAsync("/BuoniRegalo/Firma",upload)).StatusCode==HttpStatusCode.Redirect,"Signature upload");
    Check((await staff.PostAsync("/BuoniRegalo/Firma",new MultipartFormDataContent())).StatusCode==HttpStatusCode.Forbidden,"Staff signature access");
    var form=new Dictionary<string,string>{{"__RequestVerificationToken",token},{"Recipient","Alessandro Falconi"},{"Buyer","Acquirente Test"},{"Phone","+393330000000"},{"From","I tuoi amici"},{"Dedication","Buon compleanno! La prossima avventura ci aspetta sul campo."},{"Type","Kids"},{"People","8"},{"Duration","1.5"},{"Amount","240"},{"Theme","compleanno"},{"IssuedOn",GiftVoucher.Today.ToString("yyyy-MM-dd")}};
    var response=await adminClient.PostAsync("/BuoniRegalo/Crea",new FormUrlEncodedContent(form));
    var createResult=await response.Content.ReadAsStringAsync();
    Check(response.StatusCode==HttpStatusCode.Redirect,"Create failed: "+Regex.Match(createResult,"validation-summary-errors[\\s\\S]*?</div>").Value);
    db.ChangeTracker.Clear(); var row=await db.GiftVouchers.SingleAsync();
    Check(row.Details.Unlimited && row.ExpiresOn==row.IssuedOn.AddYears(1),"Kids/anniversary persistence");
    Check(await db.Partite.CountAsync()==0 && await db.AssenzeCalendario.CountAsync()==0,"Side effects on bookings/calendar");
    async Task<HttpResponseMessage> Change(HttpClient client,string op,int version,string? reason=null){var html=await client.GetStringAsync("/BuoniRegalo/Dettaglio/"+row.Id);return await client.PostAsync("/BuoniRegalo/Stato",new FormUrlEncodedContent(new Dictionary<string,string>{{"__RequestVerificationToken",Token(html)},{"id",row.Id.ToString()},{"version",version.ToString()},{"operation",op},{"reason",reason??""}}));}
    await Change(adminClient,"pagato",row.Version); db.ChangeTracker.Clear(); row=await db.GiftVouchers.SingleAsync(); Check(row.Paid,"Payment save");
    var preVersion=row.Version;
    await Change(staff,"riscatta",row.Version); await Change(staff,"riscatta",preVersion);
    db.ChangeTracker.Clear(); row=await db.GiftVouchers.SingleAsync(); Check(row.Redeemed && row.History.Count(x=>x.Action=="riscatta")==1,"Replay redemption");
    await Change(staff,"ripristina",row.Version,"Errore prova"); db.ChangeTracker.Clear(); row=await db.GiftVouchers.SingleAsync(); Check(!row.Redeemed && row.History.Count==4,"Staff undo persistence");
    await using(var first=new TesseramentoDbContext(new DbContextOptionsBuilder<TesseramentoDbContext>().UseNpgsql(cs).Options))
    await using(var second=new TesseramentoDbContext(new DbContextOptionsBuilder<TesseramentoDbContext>().UseNpgsql(cs).Options)){
        var a=await first.GiftVouchers.SingleAsync(); var b=await second.GiftVouchers.SingleAsync();
        a.ChangeState("riscatta",false,"A",null); b.ChangeState("riscatta",false,"B",null); await first.SaveChangesAsync();
        bool conflict=false;try{await second.SaveChangesAsync();}catch(DbUpdateConcurrencyException){conflict=true;}Check(conflict,"Concurrent redemption must conflict");
    }
    db.ChangeTracker.Clear(); row=await db.GiftVouchers.SingleAsync(); await Change(staff,"ripristina",row.Version,"Ripristino dopo test concorrenza");
    Check((await staff.PostAsync("/BuoniRegalo/Stato",new FormUrlEncodedContent(new Dictionary<string,string>{{"id",row.Id.ToString()},{"operation","riscatta"}}))).StatusCode==HttpStatusCode.BadRequest,"CSRF protection");
    var output=Path.GetFullPath(".codex-build/vouchers"); Directory.CreateDirectory(output);
    foreach(var format in new[]{"preview","pdf","jpg"}){
        var export=await adminClient.GetAsync($"/BuoniRegalo/Esporta/{row.Id}?format={format}");Check(export.IsSuccessStatusCode,"Export "+format);
        await File.WriteAllBytesAsync(Path.Combine(output,"voucher."+(format=="pdf"?"pdf":format+".jpg")),await export.Content.ReadAsByteArrayAsync());
    }
    var staffPage=await staff.GetStringAsync("/BuoniRegalo/Dettaglio/"+row.Id);Check(staffPage.Contains("Riscatta interamente")&&!staffPage.Contains("Conferma pagamento"),"Staff controls");
    var savedPayload=row.Payload;
    foreach(var theme in new[]{"classico","compleanno","natale","valentino","ricorrenza"}) {
        var themed=row.Details; themed.Theme=theme;
        row.Payload=JsonSerializer.Serialize(themed);
        var bytes=scope.ServiceProvider.GetRequiredService<GiftVoucherRenderer>().Render(row,signature,"preview");
        await File.WriteAllBytesAsync(Path.Combine(output,theme+".jpg"),bytes);
    }
    row.Payload=savedPayload;
    var staffPermission=await db.RolePermissions.SingleAsync(p=>p.RoleName=="Staff"&&p.FeatureName=="Buoni regalo");
    staffPermission.IsAllowed=false;await db.SaveChangesAsync();
    Check((await staff.GetAsync("/BuoniRegalo")).StatusCode==HttpStatusCode.Forbidden,"Revoked Staff permission");
    staffPermission.IsAllowed=true;await db.SaveChangesAsync();
    var dashboard=await adminClient.GetStringAsync("/BuoniRegalo/Crea");Check(dashboard.Contains("Altre ricorrenze"),"Templates view");
    Console.WriteLine("PASS: PostgreSQL schema twice, signature, real MVC views, permissions/revocation, CSRF, create/payment/redeem/undo, audit, concurrency and all five templates plus PDF/JPG export.");
    Console.WriteLine("Preview: http://127.0.0.1:55447/BuoniRegalo (test cookie VoucherTestRole=Admin). Output: "+output);
    if(args.Contains("--preview")) await Task.Delay(Timeout.Infinite);
} finally {await app.StopAsync();NpgsqlConnection.ClearAllPools();await new NpgsqlCommand($"DROP DATABASE {database} WITH (FORCE)",admin).ExecuteNonQueryAsync();}

sealed class TestAuth(IOptionsMonitor<AuthenticationSchemeOptions> options,ILoggerFactory logger,UrlEncoder encoder):AuthenticationHandler<AuthenticationSchemeOptions>(options,logger,encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync(){var role=Request.Headers["X-Test-Role"].ToString();if(string.IsNullOrEmpty(role))role=Request.Cookies["VoucherTestRole"];if(string.IsNullOrEmpty(role))return Task.FromResult(AuthenticateResult.NoResult());return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim(ClaimTypes.Role,role),new Claim(ClaimTypes.Name,"Test "+role)},"Test")),"Test")));}
    protected override Task HandleChallengeAsync(AuthenticationProperties properties){Response.StatusCode=401;return Task.CompletedTask;}
    protected override Task HandleForbiddenAsync(AuthenticationProperties properties){Response.StatusCode=403;return Task.CompletedTask;}
}
