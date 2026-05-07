using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Full_Metal_Paintball_Carmagnola.Migrations
{
    public partial class AddForeignDocumentToTesseramenti : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TipoDocumentoEstero",
                table: "Tesseramenti",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NumeroDocumentoEstero",
                table: "Tesseramenti",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TipoDocumentoEstero",
                table: "Tesseramenti");

            migrationBuilder.DropColumn(
                name: "NumeroDocumentoEstero",
                table: "Tesseramenti");
        }
    }
}
