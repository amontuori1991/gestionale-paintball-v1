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

            // Surveys unique slug
            modelBuilder.Entity<Survey>()
                .HasIndex(s => s.PublicSlug)
                .IsUnique();

            // Survey -> Questions (cascade)
            modelBuilder.Entity<SurveyQuestion>()
                .HasOne(q => q.Survey)
                .WithMany(s => s.Questions)
                .HasForeignKey(q => q.SurveyId)
                .OnDelete(DeleteBehavior.Cascade);

            // Question -> Options (cascade)
            modelBuilder.Entity<SurveyOption>()
                .HasOne(o => o.Question)
                .WithMany(q => q.Options)
                .HasForeignKey(o => o.QuestionId)
                .OnDelete(DeleteBehavior.Cascade);

            // Survey -> Responses (cascade)
            modelBuilder.Entity<SurveyResponse>()
                .HasOne(r => r.Survey)
                .WithMany() // risposte non dentro Survey per ridurre carichi
                .HasForeignKey(r => r.SurveyId)
                .OnDelete(DeleteBehavior.Cascade);

            // Response -> Answers (cascade)
            modelBuilder.Entity<SurveyAnswer>()
                .HasOne(a => a.Response)
                .WithMany(r => r.Answers)
                .HasForeignKey(a => a.ResponseId)
                .OnDelete(DeleteBehavior.Cascade);

            // Answer -> Question (restrict: non cancellare question se ci sono risposte)
            modelBuilder.Entity<SurveyAnswer>()
                .HasOne(a => a.Question)
                .WithMany()
                .HasForeignKey(a => a.QuestionId)
                .OnDelete(DeleteBehavior.Restrict);

            // Indici utili
            modelBuilder.Entity<SurveyQuestion>().HasIndex(q => new { q.SurveyId, q.Order });
            modelBuilder.Entity<SurveyOption>().HasIndex(o => new { o.QuestionId, o.Order });
            modelBuilder.Entity<SurveyResponse>().HasIndex(r => new { r.SurveyId, r.SubmittedAt });

            modelBuilder.Entity<Topic>()
                .HasMany(t => t.ToDoItems)
                .WithOne(ti => ti.Topic)
                .HasForeignKey(ti => ti.TopicId)
                .OnDelete(DeleteBehavior.Cascade);
        }

        public DbSet<CodicePromozionale> codicipromozionali { get; set; }
        public DbSet<Promozione> Promozioni { get; set; }

        public DbSet<Survey> Surveys { get; set; }
        public DbSet<SurveyQuestion> SurveyQuestions { get; set; }
        public DbSet<SurveyOption> SurveyOptions { get; set; }
        public DbSet<SurveyResponse> SurveyResponses { get; set; }
        public DbSet<SurveyAnswer> SurveyAnswers { get; set; }

    }
}
