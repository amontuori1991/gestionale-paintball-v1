using Full_Metal_Paintball_Carmagnola.Models; // Assicurati che questo sia corretto e includa la tua classe Tesseramento
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

// Assicurati che il namespace qui sia corretto per il tuo DbContext (es. Full_Metal_Paintball_Carmagnola.Data)
// Se il tuo DbContext è nella radice del progetto o in una cartella specifica, adatta il namespace.
namespace Full_Metal_Paintball_Carmagnola.Data // <-- Esempio: aggiungi il namespace del tuo progetto/cartella Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // AGGIUNGI QUESTO DbSet PER LA TUA NUOVA TABELLA DEI TESSERAMENTI
        // Questo indicherà a Entity Framework Core che la classe Tesseramento
        // deve essere mappata a una tabella nel database, di solito chiamata "Tesseramenti" per convenzione.
        public DbSet<Tesseramento> Tesseramenti { get; set; }

        // Puoi aggiungere qui i tuoi DbSet personalizzati in futuro
        // Se hai bisogno di configurazioni avanzate per il modello (es. chiavi composte, relazioni complesse),
        // puoi sovrascrivere il metodo OnModelCreating:
        /*
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder); // È FONDAMENTALE chiamare il base.OnModelCreating per far funzionare Identity

            // Esempio di configurazione aggiuntiva, se mai necessaria
            // builder.Entity<Tesseramento>()
            //    .Property(t => t.Firma)
            //    .HasColumnType("nvarchar(max)"); // Utile se la firma è una stringa base64 molto lunga
        }
        */
    }
}