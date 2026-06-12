using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Publisher.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PublishableContainer",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BuildId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContainerName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Version = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CommitSha = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ArtifactUri = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ImageDigest = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FirstSeenAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastSeenAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublishableContainer", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PublishChannel",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CurrentContainerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublishChannel", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChannelBinding",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChannelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    ContainerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BoundBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BoundAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChannelBinding", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChannelBinding_PublishChannel_ChannelId",
                        column: x => x.ChannelId,
                        principalTable: "PublishChannel",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChannelBinding_ChannelId_Sequence",
                table: "ChannelBinding",
                columns: new[] { "ChannelId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PublishableContainer_RepositoryId_ContainerName_Version",
                table: "PublishableContainer",
                columns: new[] { "RepositoryId", "ContainerName", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PublishChannel_Name",
                table: "PublishChannel",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChannelBinding");

            migrationBuilder.DropTable(
                name: "PublishableContainer");

            migrationBuilder.DropTable(
                name: "PublishChannel");
        }
    }
}
