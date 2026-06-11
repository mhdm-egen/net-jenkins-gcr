using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jenkins.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Build",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CiJobName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CiBuildNumber = table.Column<int>(type: "int", nullable: false),
                    CiRunUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CiRunId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CommitSha = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CommitShort = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Branch = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CommitAuthor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CommitMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CommittedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PackageVersion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FileVersion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AssemblyVersion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    InformationalVersion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    BaseVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    SbomUri = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    VulnerabilityReportUri = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DurationMs = table.Column<long>(type: "bigint", nullable: true),
                    TriggeredBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Build", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ContainerReleaseHandoff",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BuildId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BuildArtifactId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeployableComponentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeployableUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeploymentReleaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SemanticVersion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ArtifactUri = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RequestedByPrincipal = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SettledAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContainerReleaseHandoff", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Pipeline",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pipeline", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SourceRepository",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    GitUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Provider = table.Column<int>(type: "int", nullable: false),
                    DefaultBranch = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CiJobName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BaseVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceRepository", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BuildArtifact",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BuildId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Version = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Digest = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    ProducedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BuildArtifact", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BuildArtifact_Build_BuildId",
                        column: x => x.BuildId,
                        principalTable: "Build",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PipelineStage",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PipelineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    JobName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UpstreamJobName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Parameters = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PipelineStage", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PipelineStage_Pipeline_PipelineId",
                        column: x => x.PipelineId,
                        principalTable: "Pipeline",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DeployableComponent",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContainerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DeployableUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeployableUnitName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AutoPublish = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeployableComponent", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeployableComponent_SourceRepository_RepositoryId",
                        column: x => x.RepositoryId,
                        principalTable: "SourceRepository",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ArtifactPublication",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BuildArtifactId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Registry = table.Column<int>(type: "int", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PublishedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Tags = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArtifactPublication", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArtifactPublication_BuildArtifact_BuildArtifactId",
                        column: x => x.BuildArtifactId,
                        principalTable: "BuildArtifact",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArtifactPublication_BuildArtifactId",
                table: "ArtifactPublication",
                column: "BuildArtifactId");

            migrationBuilder.CreateIndex(
                name: "IX_Build_CiJobName_CiBuildNumber",
                table: "Build",
                columns: new[] { "CiJobName", "CiBuildNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Build_RepositoryId_StartedAtUtc",
                table: "Build",
                columns: new[] { "RepositoryId", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_BuildArtifact_BuildId",
                table: "BuildArtifact",
                column: "BuildId");

            migrationBuilder.CreateIndex(
                name: "IX_ContainerReleaseHandoff_BuildArtifactId",
                table: "ContainerReleaseHandoff",
                column: "BuildArtifactId");

            migrationBuilder.CreateIndex(
                name: "IX_ContainerReleaseHandoff_BuildId_Status",
                table: "ContainerReleaseHandoff",
                columns: new[] { "BuildId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ContainerReleaseHandoff_DeploymentReleaseId",
                table: "ContainerReleaseHandoff",
                column: "DeploymentReleaseId");

            migrationBuilder.CreateIndex(
                name: "IX_DeployableComponent_RepositoryId_ContainerName",
                table: "DeployableComponent",
                columns: new[] { "RepositoryId", "ContainerName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pipeline_Name",
                table: "Pipeline",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PipelineStage_PipelineId_Order",
                table: "PipelineStage",
                columns: new[] { "PipelineId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_SourceRepository_Name",
                table: "SourceRepository",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArtifactPublication");

            migrationBuilder.DropTable(
                name: "ContainerReleaseHandoff");

            migrationBuilder.DropTable(
                name: "DeployableComponent");

            migrationBuilder.DropTable(
                name: "PipelineStage");

            migrationBuilder.DropTable(
                name: "BuildArtifact");

            migrationBuilder.DropTable(
                name: "SourceRepository");

            migrationBuilder.DropTable(
                name: "Pipeline");

            migrationBuilder.DropTable(
                name: "Build");
        }
    }
}
