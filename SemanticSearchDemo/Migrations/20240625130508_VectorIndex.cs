using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SemanticSearchDemo.Migrations
{
    /// <inheritdoc />
    public partial class VectorIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_NewsItems_EmbeddingVector",
                table: "NewsItems",
                column: "EmbeddingVector")
                .Annotation("Npgsql:IndexMethod", "hnsw")
                .Annotation("Npgsql:IndexOperators", new[] { "vector_cosine_ops" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NewsItems_EmbeddingVector",
                table: "NewsItems");
        }
    }
}
