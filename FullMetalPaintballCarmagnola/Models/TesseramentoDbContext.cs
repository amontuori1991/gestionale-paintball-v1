using Microsoft.EntityFrameworkCore;
using Full_Metal_Paintball_Carmagnola.Models;

namespace Full_Metal_Paintball_Carmagnola.Models
{
    public class TesseramentoDbContext : DbContext
    {
        public TesseramentoDbContext(DbContextOptions<TesseramentoDbContext> options)
            : base(options)
        {
        }

        public DbSet<Tesseramento> Tesseramenti { get; set; }
        public DbSet<Partita> Partite { get; set; }
        public DbSet<PresenzaStaff> PresenzaStaff { get; set; }
        public DbSet<ReperibilitaStaff> ReperibilitaStaff { get; set; }
        public DbSet<AssenzaCalendario> AssenzeCalendario { get; set; }
        public DbSet<RangeTessereAcsi> RangeTessereAcsi { get; set; }
        public DbSet<RolePermission> RolePermissions { get; set; }

        public DbSet<Documento> Documenti { get; set; }
        public DbSet<DocumentoFornitore> Fornitori { get; set; }
        public DbSet<DocumentoAsd> DocumentiAsd { get; set; }

        public DbSet<Topic> Topics { get; set; }
        public DbSet<ToDoItem> ToDoItems { get; set; }
        public DbSet<MovimentoPartita> MovimentiPartita { get; set; }

        public DbSet<MovimentoExtra> MovimentiExtra { get; set; }

        public DbSet<Spesa> Spese { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

             modelBuilder.Entity<Topic>()
                .HasMany(t => t.ToDoItems)
                .WithOne(ti => ti.Topic)
                .HasForeignKey(ti => ti.TopicId)
                .OnDelete(DeleteBehavior.Cascade);
        }

        public DbSet<CodicePromozionale> codicipromozionali { get; set; }

    }
}
