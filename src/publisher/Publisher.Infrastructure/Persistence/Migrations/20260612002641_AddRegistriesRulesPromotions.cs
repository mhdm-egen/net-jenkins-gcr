using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Publisher.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRegistriesRulesPromotions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AutomationRule",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    Trigger = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<int>(type: "int", nullable: false),
                    TargetRegistryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ContainerNamePattern = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    RequirePublishable = table.Column<bool>(type: "bit", nullable: false),
                    RequiredChannelName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutomationRule", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Promotion",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContainerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RegistryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TriggeredBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SourceRef = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    RemoteRef = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    RepositoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContainerName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Version = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RegistryName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    FailureReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RequestedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Promotion", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RemoteRegistry",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Provider = table.Column<int>(type: "int", nullable: false),
                    RegistryHost = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    RepositoryPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    AuthMethod = table.Column<int>(type: "int", nullable: false),
                    Username = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CredentialSecretRef = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RemoteRegistry", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AutomationRule_Name",
                table: "AutomationRule",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AutomationRule_TargetRegistryId",
                table: "AutomationRule",
                column: "TargetRegistryId");

            migrationBuilder.CreateIndex(
                name: "IX_AutomationRule_Trigger_Enabled",
                table: "AutomationRule",
                columns: new[] { "Trigger", "Enabled" });

            migrationBuilder.CreateIndex(
                name: "IX_Promotion_ContainerId_RegistryId",
                table: "Promotion",
                columns: new[] { "ContainerId", "RegistryId" });

            migrationBuilder.CreateIndex(
                name: "IX_Promotion_Status",
                table: "Promotion",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_RemoteRegistry_Name",
                table: "RemoteRegistry",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AutomationRule");

            migrationBuilder.DropTable(
                name: "Promotion");

            migrationBuilder.DropTable(
                name: "RemoteRegistry");
        }
    }
}
