using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Full_Metal_Paintball_Carmagnola.Migrations
{
    public partial class PublicTesseramentoAnagrafica : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "NatoEstero",
                table: "Tesseramenti",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "CodiceCatastaleNascita",
                table: "Tesseramenti",
                type: "character varying(4)",
                maxLength: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NazioneNascita",
                table: "Tesseramenti",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CittaNascita",
                table: "Tesseramenti",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NazioneCittadinanza",
                table: "Tesseramenti",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NazioneResidenza",
                table: "Tesseramenti",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Cellulare",
                table: "Tesseramenti",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ComuniCatastali",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Provincia = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    CodiceCatastale = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    CodiceIstat = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Attivo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComuniCatastali", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StatiEsteriCatastali",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    CodiceCatastale = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    Attivo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatiEsteriCatastali", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ComuniCatastali_Nome_Provincia_CodiceCatastale",
                table: "ComuniCatastali",
                columns: new[] { "Nome", "Provincia", "CodiceCatastale" });

            migrationBuilder.CreateIndex(
                name: "IX_ComuniCatastali_CodiceCatastale",
                table: "ComuniCatastali",
                column: "CodiceCatastale",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StatiEsteriCatastali_Nome_CodiceCatastale",
                table: "StatiEsteriCatastali",
                columns: new[] { "Nome", "CodiceCatastale" });

            migrationBuilder.CreateIndex(
                name: "IX_StatiEsteriCatastali_CodiceCatastale",
                table: "StatiEsteriCatastali",
                column: "CodiceCatastale",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ComuniCatastali");
            migrationBuilder.DropTable(name: "StatiEsteriCatastali");

            migrationBuilder.DropColumn(name: "NatoEstero", table: "Tesseramenti");
            migrationBuilder.DropColumn(name: "CodiceCatastaleNascita", table: "Tesseramenti");
            migrationBuilder.DropColumn(name: "NazioneNascita", table: "Tesseramenti");
            migrationBuilder.DropColumn(name: "CittaNascita", table: "Tesseramenti");
            migrationBuilder.DropColumn(name: "NazioneCittadinanza", table: "Tesseramenti");
            migrationBuilder.DropColumn(name: "NazioneResidenza", table: "Tesseramenti");
            migrationBuilder.DropColumn(name: "Cellulare", table: "Tesseramenti");
        }
    }
}
