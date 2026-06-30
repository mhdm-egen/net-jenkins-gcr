using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Deployment.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class KubernetesTarget : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "CloudRunServiceName",
                table: "DeploymentRun",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(300)",
                oldMaxLength: 300);

            migrationBuilder.AddColumn<string>(
                name: "KubernetesContext",
                table: "DeploymentRun",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KubernetesNamespace",
                table: "DeploymentRun",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KubernetesResource",
                table: "DeploymentRun",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KubernetesSpec",
                table: "DeploymentRun",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CloudRunServiceName",
                table: "DeploymentMapping",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(300)",
                oldMaxLength: 300);

            migrationBuilder.AddColumn<string>(
                name: "Kubernetes",
                table: "DeploymentMapping",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "KubernetesContext",
                table: "DeploymentRun");

            migrationBuilder.DropColumn(
                name: "KubernetesNamespace",
                table: "DeploymentRun");

            migrationBuilder.DropColumn(
                name: "KubernetesResource",
                table: "DeploymentRun");

            migrationBuilder.DropColumn(
                name: "KubernetesSpec",
                table: "DeploymentRun");

            migrationBuilder.DropColumn(
                name: "Kubernetes",
                table: "DeploymentMapping");

            migrationBuilder.AlterColumn<string>(
                name: "CloudRunServiceName",
                table: "DeploymentRun",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(300)",
                oldMaxLength: 300,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CloudRunServiceName",
                table: "DeploymentMapping",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(300)",
                oldMaxLength: 300,
                oldNullable: true);
        }
    }
}
