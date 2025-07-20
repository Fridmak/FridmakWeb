using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TestingAppWeb.Migrations
{
    /// <inheritdoc />
    public partial class UpdateFriendModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Approved",
                table: "Friends",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Approved",
                table: "Friends");
        }
    }
}
