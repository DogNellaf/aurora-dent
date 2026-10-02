using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DentalClinic.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProfileIsBanned : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsBanned",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("UPDATE [AspNetUsers] SET [IsBanned] = 1 WHERE [EmailConfirmed] = 0");
            migrationBuilder.Sql("UPDATE [AspNetUsers] SET [EmailConfirmed] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsBanned",
                table: "AspNetUsers");
        }
    }
}
