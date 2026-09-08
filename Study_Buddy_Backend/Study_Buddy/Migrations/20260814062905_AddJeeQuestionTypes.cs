using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Study_Buddy.Migrations
{
    /// <inheritdoc />
    public partial class AddJeeQuestionTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Assertion",
                table: "QuizQuestions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CorrectAnswersJson",
                table: "QuizQuestions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "QuizQuestions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RightOptionsJson",
                table: "QuizQuestions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "Assertion",
                table: "PaperQuestions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CorrectAnswersJson",
                table: "PaperQuestions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "PaperQuestions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RightOptionsJson",
                table: "PaperQuestions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "PaperQuestions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Assertion",
                table: "QuizQuestions");

            migrationBuilder.DropColumn(
                name: "CorrectAnswersJson",
                table: "QuizQuestions");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "QuizQuestions");

            migrationBuilder.DropColumn(
                name: "RightOptionsJson",
                table: "QuizQuestions");

            migrationBuilder.DropColumn(
                name: "Assertion",
                table: "PaperQuestions");

            migrationBuilder.DropColumn(
                name: "CorrectAnswersJson",
                table: "PaperQuestions");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "PaperQuestions");

            migrationBuilder.DropColumn(
                name: "RightOptionsJson",
                table: "PaperQuestions");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "PaperQuestions");
        }
    }
}
