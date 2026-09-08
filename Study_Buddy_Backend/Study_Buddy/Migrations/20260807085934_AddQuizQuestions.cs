using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Study_Buddy.Migrations
{
    /// <inheritdoc />
    public partial class AddQuizQuestions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "QuestionsJson",
                table: "QuizResults",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "QuestionsJson",
                table: "QuizResults");
        }
    }
}
