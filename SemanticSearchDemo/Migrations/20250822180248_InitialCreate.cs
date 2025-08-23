using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SemanticSearchDemo.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NewsItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Link = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Headline = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ShortDescription = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Authors = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    EmbeddingData = table.Column<string>(type: "vector(384)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NewsItems", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NewsItems");
        }
    }
}
