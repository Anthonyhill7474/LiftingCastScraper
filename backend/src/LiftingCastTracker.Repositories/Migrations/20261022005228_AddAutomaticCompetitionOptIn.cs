using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiftingCastTracker.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class AddAutomaticCompetitionOptIn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "automatic_sync_enabled",
                table: "competitions",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "automatic_sync_enabled",
                table: "competitions");
        }
    }
}
