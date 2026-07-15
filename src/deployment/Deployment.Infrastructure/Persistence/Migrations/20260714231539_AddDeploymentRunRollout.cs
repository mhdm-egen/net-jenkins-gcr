using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Deployment.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDeploymentRunRollout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DecisionBy",
                table: "DeploymentRun",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RolloutActiveSlot",
                table: "DeploymentRun",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RolloutGreenSlot",
                table: "DeploymentRun",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DecisionBy",
                table: "DeploymentRun");

            migrationBuilder.DropColumn(
                name: "RolloutActiveSlot",
                table: "DeploymentRun");

            migrationBuilder.DropColumn(
                name: "RolloutGreenSlot",
                table: "DeploymentRun");
        }
    }
}
