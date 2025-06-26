using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Full_Metal_Paintball_Carmagnola.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTesseramentiTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Tesseramenti",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nome = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Cognome = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DataNascita = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Genere = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ComuneNascita = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ComuneResidenza = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    CodiceFiscale = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    Minorenne = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    NomeGenitore = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CognomeGenitore = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TerminiAccettati = table.Column<bool>(type: "bit", nullable: false),
                    Firma = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DataCreazione = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tesseramenti", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Tesseramenti");
        }
    }
}
