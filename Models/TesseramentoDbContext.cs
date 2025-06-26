using Microsoft.EntityFrameworkCore; // ESSENZIALE per DbContext e DbSet

namespace Full_Metal_Paintball_Carmagnola.Models
{
    public class TesseramentoDbContext : DbContext
    {
        public TesseramentoDbContext(DbContextOptions<TesseramentoDbContext> options) : base(options)
        {
        }

        // Aggiungi una proprietà DbSet per il modello Tesseramento
        public DbSet<TesseramentoViewModel> Tesseramenti { get; set; }

        // Se vuoi configurare come le tabelle e colonne sono mappate nel DB
        // protected override void OnModelCreating(ModelBuilder modelBuilder)
        // {
        //     base.OnModelCreating(modelBuilder);
        //     // Esempio: configurare che Firma sia mappata come NVARCHAR(MAX)
        //     modelBuilder.Entity<TesseramentoViewModel>()
        //         .Property(t => t.Firma)
        //         .HasColumnType("nvarchar(max)");
        // }
    }
}