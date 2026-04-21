using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Full_Metal_Paintball_Carmagnola.Migrations
{
    public partial class AddDataValiditaToRangeTessereAcsi : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DataValidita",
                table: "RangeTessereAcsi",
                type: "timestamp with time zone",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DataValidita",
                table: "RangeTessereAcsi");
        }
    }
}
