using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Study_Buddy.Migrations
{
    /// <inheritdoc />
    public partial class AddPassageField : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Passage",
                table: "QuizQuestions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Passage",
                table: "PaperQuestions",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Passage",
                table: "QuizQuestions");

            migrationBuilder.DropColumn(
                name: "Passage",
                table: "PaperQuestions");
        }
    }
}
