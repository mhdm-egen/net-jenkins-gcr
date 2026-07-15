using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Deployment.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAspireAppBlueGreen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RolloutActiveSlot",
                table: "AspireApplicationRun",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RolloutGreenSlot",
                table: "AspireApplicationRun",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ActiveSlot",
                table: "AspireApplication",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PromotionMode",
                table: "AspireApplication",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Strategy",
                table: "AspireApplication",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RolloutActiveSlot",
                table: "AspireApplicationRun");

            migrationBuilder.DropColumn(
                name: "RolloutGreenSlot",
                table: "AspireApplicationRun");

            migrationBuilder.DropColumn(
                name: "ActiveSlot",
                table: "AspireApplication");

            migrationBuilder.DropColumn(
                name: "PromotionMode",
                table: "AspireApplication");

            migrationBuilder.DropColumn(
                name: "Strategy",
                table: "AspireApplication");
        }
    }
}
