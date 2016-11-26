using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AutoAllegro.Data.Migrations
{
    public partial class allegro_key : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AllegroHashedPass",
                table: "AspNetUsers",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AllegroKey",
                table: "AspNetUsers",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AllegroUserName",
                table: "AspNetUsers",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllegroHashedPass",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "AllegroKey",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "AllegroUserName",
                table: "AspNetUsers");
        }
    }
}
