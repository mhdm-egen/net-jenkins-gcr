using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Deployment.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AspireRicherModelAndK8sEnv : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AppHostPath",
                table: "AspireApplicationRun");

            migrationBuilder.DropColumn(
                name: "AppHostPath",
                table: "AspireApplication");

            migrationBuilder.DropColumn(
                name: "KubeContext",
                table: "AspireApplication");

            migrationBuilder.DropColumn(
                name: "Namespace",
                table: "AspireApplication");

            migrationBuilder.AddColumn<string>(
                name: "KubernetesContext",
                table: "DeploymentEnvironment",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KubernetesNamespace",
                table: "DeploymentEnvironment",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EnvironmentId",
                table: "AspireApplicationRun",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "EnvironmentName",
                table: "AspireApplicationRun",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ManifestSource",
                table: "AspireApplicationRun",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Version",
                table: "AspireApplicationRun",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EnvironmentId",
                table: "AspireApplication",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "ManifestSource",
                table: "AspireApplication",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Version",
                table: "AspireApplication",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "KubernetesContext",
                table: "DeploymentEnvironment");

            migrationBuilder.DropColumn(
                name: "KubernetesNamespace",
                table: "DeploymentEnvironment");

            migrationBuilder.DropColumn(
                name: "EnvironmentId",
                table: "AspireApplicationRun");

            migrationBuilder.DropColumn(
                name: "EnvironmentName",
                table: "AspireApplicationRun");

            migrationBuilder.DropColumn(
                name: "ManifestSource",
                table: "AspireApplicationRun");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "AspireApplicationRun");

            migrationBuilder.DropColumn(
                name: "EnvironmentId",
                table: "AspireApplication");

            migrationBuilder.DropColumn(
                name: "ManifestSource",
                table: "AspireApplication");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "AspireApplication");

            migrationBuilder.AddColumn<string>(
                name: "AppHostPath",
                table: "AspireApplicationRun",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AppHostPath",
                table: "AspireApplication",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "KubeContext",
                table: "AspireApplication",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Namespace",
                table: "AspireApplication",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");
        }
    }
}
