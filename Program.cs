using Full_Metal_Paintball_Carmagnola.Data;
using Full_Metal_Paintball_Carmagnola.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Aggiungi i servizi al contenitore
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// Configura il TesseramentoDbContext con la stringa di connessione
builder.Services.AddDbContext<TesseramentoDbContext>(options =>
    options.UseSqlServer(connectionString)); // Configura per usare SQL Server

// Configura l'autenticazione e l'autorizzazione
builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>();

// Aggiungi il supporto per i controller con viste (MVC)
builder.Services.AddControllersWithViews();

// Aggiungi il supporto per le Razor Pages (necessario per Identity UI e _ValidationScriptsPartial)
builder.Services.AddRazorPages();

var app = builder.Build();

// Configura il pipeline delle richieste HTTP
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // Il valore predefinito HSTS è di 30 giorni. Potresti volerlo cambiare per scenari di produzione.
    app.UseHsts();
}

app.UseHttpsRedirection(); // Reindirizza le richieste HTTP a HTTPS
app.UseStaticFiles();     // Permette l'accesso a file statici (es. CSS, JS, immagini in wwwroot)

app.UseRouting();         // Abilita il routing per le richieste

app.UseAuthorization();   // Abilita l'autorizzazione (controllo degli accessi)
app.UseAuthentication();  // Abilita l'autenticazione (chi è l'utente)

// Configura la mappatura delle rotte per i controller MVC
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Mappa le Razor Pages (necessario per Identity UI)
app.MapRazorPages();

app.Run();
