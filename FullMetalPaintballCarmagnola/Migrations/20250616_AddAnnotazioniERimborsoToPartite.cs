using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Full_Metal_Paintball_Carmagnola.Migrations
{
    public partial class AddAnnotazioniERimborsoToPartite : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Annotazioni",
                table: "Partite",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Rimborso",
                table: "Partite",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Annotazioni",
                table: "Partite");

            migrationBuilder.DropColumn(
                name: "Rimborso",
                table: "Partite");
        }
    }
}
