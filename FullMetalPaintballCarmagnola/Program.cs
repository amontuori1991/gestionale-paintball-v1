using Full_Metal_Paintball_Carmagnola.Authorization;
using Full_Metal_Paintball_Carmagnola.Data;
using Full_Metal_Paintball_Carmagnola.Models;
using Full_Metal_Paintball_Carmagnola.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Stringa di connessione dal file appsettings.json
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

// CONFIGURAZIONE POSTGRESQL
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDbContext<TesseramentoDbContext>(options =>
    options.UseNpgsql(connectionString));

// Configura Identity
builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = true;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
builder.Services.AddTransient<IEmailService, EmailSender>();
builder.Services.AddTransient<IEmailSender, EmailSender>(); // Fondamentale per Identity


// Autorizzazioni basate su Feature
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAuthorizationHandler, FeatureAuthorizationHandler>();

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
    options.AddPolicy("ToDoList", policy =>
        policy.Requirements.Add(new FeatureRequirement("ToDoList")));
});

// Cookie di autenticazione
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";
    options.AccessDeniedPath = "/AccessDenied/Custom";
    options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
    options.SlidingExpiration = true;
    options.Cookie.HttpOnly = true;
});

var app = builder.Build();

// Pipeline
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
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

// SEED iniziale ruoli e dati
// SEED iniziale ruoli e dati
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var dbContext = scope.ServiceProvider.GetRequiredService<TesseramentoDbContext>();

    string[] roles = { "Admin", "Staff" };

    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
            await roleManager.CreateAsync(new IdentityRole(role));
    }

    var adminEmail = "alexmontuori1991@gmail.com";
    var adminUser = await userManager.FindByEmailAsync(adminEmail);
    if (adminUser != null && !await userManager.IsInRoleAsync(adminUser, "Admin"))
    {
        await userManager.AddToRoleAsync(adminUser, "Admin");
    }

    if (!await dbContext.Topics.AnyAsync())
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

// Imposta lingua italiana globale
var cultureInfo = new System.Globalization.CultureInfo("it-IT");
System.Globalization.CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;

app.Run();

