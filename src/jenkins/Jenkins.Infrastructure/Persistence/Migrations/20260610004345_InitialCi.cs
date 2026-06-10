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
                name: "SourceRepository",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    GitUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Provider = table.Column<int>(type: "INTEGER", nullable: false),
                    DefaultBranch = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CiJobName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    BaseVersion = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceRepository", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeployableComponent",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ContainerName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    DeployableUnitId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeployableUnitName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    AutoPublish = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
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

            migrationBuilder.CreateIndex(
                name: "IX_DeployableComponent_RepositoryId_ContainerName",
                table: "DeployableComponent",
                columns: new[] { "RepositoryId", "ContainerName" },
                unique: true);

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
                name: "DeployableComponent");

            migrationBuilder.DropTable(
                name: "SourceRepository");
        }
    }
}
