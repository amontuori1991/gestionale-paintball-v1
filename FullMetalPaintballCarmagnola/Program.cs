using DocumentFormat.OpenXml.InkML; // Questo using sembra non essere utilizzato, potresti rimuoverlo.
using Full_Metal_Paintball_Carmagnola.Authorization;
using Full_Metal_Paintball_Carmagnola.Data; // Per TesseramentoDbContext
using Full_Metal_Paintball_Carmagnola.Models; // Per Topic, ApplicationUser
using Full_Metal_Paintball_Carmagnola.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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

// Configura l'autenticazione e l'autorizzazione con conferma account abilitata
builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = true; // OBBLIGA conferma email
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<ApplicationDbContext>();

// Aggiungi il supporto per i controller con viste (MVC)
builder.Services.AddControllersWithViews();

// Aggiungi il supporto per le Razor Pages (necessario per Identity UI e _ValidationScriptsPartial)
builder.Services.AddRazorPages();

// Registra IEmailService come transient
builder.Services.AddTransient<IEmailService, EmailSender>();

// REGISTRAZIONE DEL SERVIZIO PER IL FEATURE AUTHORIZATION HANDLER
builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<IAuthorizationHandler, FeatureAuthorizationHandler>();

// Definizione delle policy basate sulle feature
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Tesserati", policy =>
        policy.Requirements.Add(new FeatureRequirement("Tesserati")));
    options.AddPolicy("Prenotazioni", policy =>
        policy.Requirements.Add(new FeatureRequirement("Prenotazioni")));
    options.AddPolicy("Caparre", policy =>
        policy.Requirements.Add(new FeatureRequirement("Caparre")));
    options.AddPolicy("Presenze Staff", policy =>
        policy.Requirements.Add(new FeatureRequirement("Presenze Staff")));
    options.AddPolicy("Gestione Utenti", policy =>
        policy.Requirements.Add(new FeatureRequirement("Gestione Utenti")));
    options.AddPolicy("Statistiche", policy =>
        policy.Requirements.Add(new FeatureRequirement("Statistiche")));
    options.AddPolicy("Prezzi", policy =>
        policy.Requirements.Add(new FeatureRequirement("Prezzi")));
    options.AddPolicy("Calendario Assenze", policy =>
        policy.Requirements.Add(new FeatureRequirement("Calendario Assenze")));
    options.AddPolicy("ACSI", policy =>
        policy.Requirements.Add(new FeatureRequirement("ACSI")));
    options.AddPolicy("ToDoList", policy => // Policy per la ToDoList
        policy.Requirements.Add(new FeatureRequirement("ToDoList")));
});

// Configura cookie
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";
    options.AccessDeniedPath = "/AccessDenied/Custom";
    options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
    options.SlidingExpiration = true;
    options.Cookie.HttpOnly = true;
});

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

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

// Creazione automatica dei ruoli e assegnazione Admin iniziale
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var dbContext = scope.ServiceProvider.GetRequiredService<TesseramentoDbContext>(); // <<< SPOSTATO QUI, PRIMA DELL'USO

    string[] roles = { "Admin", "Staff" };

    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
            await roleManager.CreateAsync(new IdentityRole(role));
    }

    // (Opzionale) assegna il ruolo Admin a un utente specifico
    var adminEmail = "alexmontuori1991@gmail.com";
    var adminUser = await userManager.FindByEmailAsync(adminEmail);
    if (adminUser != null && !await userManager.IsInRoleAsync(adminUser, "Admin"))
    {
        await userManager.AddToRoleAsync(adminUser, "Admin");
    }

    // Logica di Seed per i Topic della ToDoList
    if (!await dbContext.Topics.AnyAsync()) // Controlla se esistono già dei topic
    {
        var topics = new List<Topic>
        {
            new Topic { Name = "Container" },
            new Topic { Name = "Ostacoli" },
            new Topic { Name = "Fucili" }
        };
        await dbContext.Topics.AddRangeAsync(topics);
        await dbContext.SaveChangesAsync();
    }
}

app.Run();