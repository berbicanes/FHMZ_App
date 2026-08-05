using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vodostaji.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPublicationLag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "PublicationLagSeconds",
                table: "stations",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PublicationLagSeconds",
                table: "stations");
        }
    }
}
