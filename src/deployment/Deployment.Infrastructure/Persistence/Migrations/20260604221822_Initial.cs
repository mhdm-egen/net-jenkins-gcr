using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Deployment.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConfigurationSetting",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeployableUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EnvironmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Key = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    IsSecret = table.Column<bool>(type: "bit", nullable: false),
                    SecretReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ValueType = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfigurationSetting", x => x.Id);
                    table.CheckConstraint("CK_ConfigurationSetting_SecretShape", "([IsSecret] = 1 AND [Value] IS NULL AND [SecretReference] IS NOT NULL) OR ([IsSecret] = 0 AND [SecretReference] IS NULL AND [Value] IS NOT NULL)");
                });

            migrationBuilder.CreateTable(
                name: "ConfigurationSettingHistory",
                columns: table => new
                {
                    HistoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConfigurationSettingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChangeKind = table.Column<int>(type: "int", nullable: false),
                    OldValue = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    OldSecretReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    OldIsSecret = table.Column<bool>(type: "bit", nullable: true),
                    OldValueType = table.Column<int>(type: "int", nullable: true),
                    NewValue = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    NewSecretReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    NewIsSecret = table.Column<bool>(type: "bit", nullable: true),
                    NewValueType = table.Column<int>(type: "int", nullable: true),
                    ChangedByPrincipal = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ChangedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfigurationSettingHistory", x => x.HistoryId);
                });

            migrationBuilder.CreateTable(
                name: "DeployableUnit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UnitType = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeployableUnit", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Deployment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReleaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EnvironmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ParentDeploymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Strategy = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Trigger = table.Column<int>(type: "int", nullable: false),
                    TriggeredByPrincipal = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SkipPromotionPathReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    OverrideFreezeReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RolledBackByDeploymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Deployment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Deployment_Deployment_ParentDeploymentId",
                        column: x => x.ParentDeploymentId,
                        principalTable: "Deployment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Environment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PromotionRank = table.Column<int>(type: "int", nullable: false),
                    RequiresApproval = table.Column<bool>(type: "bit", nullable: false),
                    IsProduction = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Environment", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Release",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeployableUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SemanticVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    BuildNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CommitSha = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ArtifactType = table.Column<int>(type: "int", nullable: false),
                    ArtifactUri = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ArtifactSha256 = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    SbomUri = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    VulnerabilityReportUri = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CiRunUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CiRunId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PublishedByPrincipal = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Release", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReleaseStatusChange",
                columns: table => new
                {
                    ChangeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReleaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromStatus = table.Column<int>(type: "int", nullable: false),
                    ToStatus = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ChangedByPrincipal = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ChangedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReleaseStatusChange", x => x.ChangeId);
                });

            migrationBuilder.CreateTable(
                name: "Application",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Application", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Application_DeployableUnit_Id",
                        column: x => x.Id,
                        principalTable: "DeployableUnit",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Service",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    RepositoryUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    TargetFramework = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Service", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Service_DeployableUnit_Id",
                        column: x => x.Id,
                        principalTable: "DeployableUnit",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Approval",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeploymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApproverPrincipal = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DecidedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Approval", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Approval_Deployment_DeploymentId",
                        column: x => x.DeploymentId,
                        principalTable: "Deployment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DeploymentEvent",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeploymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Detail = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeploymentEvent", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeploymentEvent_Deployment_DeploymentId",
                        column: x => x.DeploymentId,
                        principalTable: "Deployment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DeploymentSecretBinding",
                columns: table => new
                {
                    DeploymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConfigurationSettingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResolvedSecretUri = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ResolvedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeploymentSecretBinding", x => new { x.DeploymentId, x.ConfigurationSettingId });
                    table.ForeignKey(
                        name: "FK_DeploymentSecretBinding_Deployment_DeploymentId",
                        column: x => x.DeploymentId,
                        principalTable: "Deployment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DeploymentTarget",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EnvironmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetKind = table.Column<int>(type: "int", nullable: false),
                    ResourceId = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Region = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Slot = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeploymentTarget", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeploymentTarget_Environment_EnvironmentId",
                        column: x => x.EnvironmentId,
                        principalTable: "Environment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EnvironmentFreezeWindow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EnvironmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedByPrincipal = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnvironmentFreezeWindow", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EnvironmentFreezeWindow_Environment_EnvironmentId",
                        column: x => x.EnvironmentId,
                        principalTable: "Environment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReleaseComposition",
                columns: table => new
                {
                    ApplicationReleaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ServiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PinMode = table.Column<int>(type: "int", nullable: false),
                    ServiceReleaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReleaseComposition", x => new { x.ApplicationReleaseId, x.ServiceId });
                    table.CheckConstraint("CK_ReleaseComposition_PinMode", "([PinMode] = 0 AND [ServiceReleaseId] IS NOT NULL) OR ([PinMode] IN (1,2) AND [ServiceReleaseId] IS NULL)");
                    table.ForeignKey(
                        name: "FK_ReleaseComposition_Release_ApplicationReleaseId",
                        column: x => x.ApplicationReleaseId,
                        principalTable: "Release",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ApplicationService",
                columns: table => new
                {
                    ApplicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ServiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsOptional = table.Column<bool>(type: "bit", nullable: false),
                    DeploymentOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationService", x => new { x.ApplicationId, x.ServiceId });
                    table.ForeignKey(
                        name: "FK_ApplicationService_Application_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "Application",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationService_ServiceId",
                table: "ApplicationService",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Approval_DeploymentId",
                table: "Approval",
                column: "DeploymentId");

            migrationBuilder.CreateIndex(
                name: "IX_ConfigurationSetting_DeployableUnitId_EnvironmentId_Key",
                table: "ConfigurationSetting",
                columns: new[] { "DeployableUnitId", "EnvironmentId", "Key" },
                unique: true,
                filter: "[EnvironmentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ConfigurationSettingHistory_ConfigurationSettingId_ChangedAtUtc",
                table: "ConfigurationSettingHistory",
                columns: new[] { "ConfigurationSettingId", "ChangedAtUtc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_DeployableUnit_Name",
                table: "DeployableUnit",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Deployment_EnvironmentId_Status_CompletedAtUtc",
                table: "Deployment",
                columns: new[] { "EnvironmentId", "Status", "CompletedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Deployment_ParentDeploymentId",
                table: "Deployment",
                column: "ParentDeploymentId");

            migrationBuilder.CreateIndex(
                name: "IX_Deployment_ReleaseId",
                table: "Deployment",
                column: "ReleaseId");

            migrationBuilder.CreateIndex(
                name: "IX_Deployment_TargetId_Status_CompletedAtUtc",
                table: "Deployment",
                columns: new[] { "TargetId", "Status", "CompletedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentEvent_DeploymentId_Timestamp",
                table: "DeploymentEvent",
                columns: new[] { "DeploymentId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentSecretBinding_ConfigurationSettingId",
                table: "DeploymentSecretBinding",
                column: "ConfigurationSettingId");

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentTarget_EnvironmentId_Region",
                table: "DeploymentTarget",
                columns: new[] { "EnvironmentId", "Region" });

            migrationBuilder.CreateIndex(
                name: "IX_Environment_Name",
                table: "Environment",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Environment_PromotionRank",
                table: "Environment",
                column: "PromotionRank");

            migrationBuilder.CreateIndex(
                name: "IX_EnvironmentFreezeWindow_EnvironmentId_StartUtc_EndUtc",
                table: "EnvironmentFreezeWindow",
                columns: new[] { "EnvironmentId", "StartUtc", "EndUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Release_DeployableUnitId_SemanticVersion",
                table: "Release",
                columns: new[] { "DeployableUnitId", "SemanticVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Release_DeployableUnitId_Status",
                table: "Release",
                columns: new[] { "DeployableUnitId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseComposition_ServiceId",
                table: "ReleaseComposition",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseComposition_ServiceReleaseId",
                table: "ReleaseComposition",
                column: "ServiceReleaseId");

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseStatusChange_ReleaseId_ChangedAtUtc",
                table: "ReleaseStatusChange",
                columns: new[] { "ReleaseId", "ChangedAtUtc" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApplicationService");

            migrationBuilder.DropTable(
                name: "Approval");

            migrationBuilder.DropTable(
                name: "ConfigurationSetting");

            migrationBuilder.DropTable(
                name: "ConfigurationSettingHistory");

            migrationBuilder.DropTable(
                name: "DeploymentEvent");

            migrationBuilder.DropTable(
                name: "DeploymentSecretBinding");

            migrationBuilder.DropTable(
                name: "DeploymentTarget");

            migrationBuilder.DropTable(
                name: "EnvironmentFreezeWindow");

            migrationBuilder.DropTable(
                name: "ReleaseComposition");

            migrationBuilder.DropTable(
                name: "ReleaseStatusChange");

            migrationBuilder.DropTable(
                name: "Service");

            migrationBuilder.DropTable(
                name: "Application");

            migrationBuilder.DropTable(
                name: "Deployment");

            migrationBuilder.DropTable(
                name: "Environment");

            migrationBuilder.DropTable(
                name: "Release");

            migrationBuilder.DropTable(
                name: "DeployableUnit");
        }
    }
}
