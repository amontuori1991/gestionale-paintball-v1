using Microsoft.EntityFrameworkCore.Migrations;
using System;

#nullable disable

namespace Full_Metal_Paintball_Carmagnola.Migrations
{
    public partial class AggiungiStaffPresenze : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PresenzaStaff",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Data = table.Column<DateTime>(nullable: false),
                    Giorno = table.Column<string>(nullable: false),
                    NomeStaff = table.Column<string>(nullable: false),
                    Presente = table.Column<bool>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PresenzaStaff", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReperibilitaStaff",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Data = table.Column<DateTime>(nullable: false),
                    Giorno = table.Column<string>(nullable: false),
                    Reperibile = table.Column<string>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReperibilitaStaff", x => x.Id);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PresenzaStaff");

            migrationBuilder.DropTable(
                name: "ReperibilitaStaff");
        }
    }
}
