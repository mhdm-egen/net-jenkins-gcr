using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Deployment.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddContainerImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContainerImage",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Registry = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Repository = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    DefaultTag = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContainerImage", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContainerImage_Registry_Repository_Name",
                table: "ContainerImage",
                columns: new[] { "Registry", "Repository", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContainerImage");
        }
    }
}
