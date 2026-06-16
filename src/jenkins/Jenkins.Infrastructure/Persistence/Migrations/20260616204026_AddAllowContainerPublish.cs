using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jenkins.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAllowContainerPublish : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowContainerPublish",
                table: "SourceRepository",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowContainerPublish",
                table: "SourceRepository");
        }
    }
}
