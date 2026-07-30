using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Deployment.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCommitProvenanceForLeadTime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CommitSha",
                table: "KnownContainer",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CommittedAtUtc",
                table: "KnownContainer",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CommitSha",
                table: "DeploymentRun",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CommittedAtUtc",
                table: "DeploymentRun",
                type: "datetimeoffset",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CommitSha",
                table: "KnownContainer");

            migrationBuilder.DropColumn(
                name: "CommittedAtUtc",
                table: "KnownContainer");

            migrationBuilder.DropColumn(
                name: "CommitSha",
                table: "DeploymentRun");

            migrationBuilder.DropColumn(
                name: "CommittedAtUtc",
                table: "DeploymentRun");
        }
    }
}
