using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Full_Metal_Paintball_Carmagnola.Migrations
{
    public partial class AddNoTesseramentoToTesseramenti : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "NoTesseramento",
                table: "Tesseramenti",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NoTesseramento",
                table: "Tesseramenti");
        }
    }
}
