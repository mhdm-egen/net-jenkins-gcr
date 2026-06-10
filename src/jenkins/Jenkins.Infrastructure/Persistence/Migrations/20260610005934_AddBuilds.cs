using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jenkins.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBuilds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Build",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CiJobName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CiBuildNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    CiRunUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    CiRunId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CommitSha = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CommitShort = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Branch = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CommitAuthor = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    CommitMessage = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    CommittedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    PackageVersion = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    FileVersion = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    AssemblyVersion = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    InformationalVersion = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    BaseVersion = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    SbomUri = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    VulnerabilityReportUri = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    DurationMs = table.Column<long>(type: "INTEGER", nullable: true),
                    TriggeredBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Build", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BuildArtifact",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    BuildId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Version = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Digest = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    SizeBytes = table.Column<long>(type: "INTEGER", nullable: true),
                    ProducedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
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
                name: "ArtifactPublication",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    BuildArtifactId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Registry = table.Column<int>(type: "INTEGER", nullable: false),
                    Reference = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    PublishedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Tags = table.Column<string>(type: "TEXT", nullable: false)
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArtifactPublication");

            migrationBuilder.DropTable(
                name: "BuildArtifact");

            migrationBuilder.DropTable(
                name: "Build");
        }
    }
}
