using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiftingCastTracker.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class AddCompetitionSynchronization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "competition_documents",
                columns: table => new
                {
                    competition_id = table.Column<string>(type: "text", nullable: false),
                    document_id = table.Column<string>(type: "text", nullable: false),
                    revision = table.Column<string>(type: "text", nullable: false),
                    document_data = table.Column<string>(type: "jsonb", nullable: false),
                    last_synced_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_competition_documents", x => new { x.competition_id, x.document_id });
                    table.ForeignKey(
                        name: "FK_competition_documents_competitions_competition_id",
                        column: x => x.competition_id,
                        principalTable: "competitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "competition_sync_state",
                columns: table => new
                {
                    competition_id = table.Column<string>(type: "text", nullable: false),
                    last_sequence = table.Column<string>(type: "text", nullable: false),
                    last_synced_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_competition_sync_state", x => x.competition_id);
                    table.ForeignKey(
                        name: "FK_competition_sync_state_competitions_competition_id",
                        column: x => x.competition_id,
                        principalTable: "competitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "competition_documents");

            migrationBuilder.DropTable(
                name: "competition_sync_state");
        }
    }
}
