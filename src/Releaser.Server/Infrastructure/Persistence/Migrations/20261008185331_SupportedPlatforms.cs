using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Releaser.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SupportedPlatforms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "supported_platforms",
                table: "applications",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            // Backfill (issue #10): the platforms each application's releases already use; all platforms
            // (Windows, MacOS, LinuxX64, LinuxArm64, LinuxArmv7l = 1..5) when it has no releases yet.
            migrationBuilder.Sql("""
                UPDATE applications AS a
                SET supported_platforms = COALESCE(
                    (SELECT jsonb_agg(DISTINCT p.value ORDER BY p.value)
                     FROM releases AS r, jsonb_array_elements(r.platforms) AS p
                     WHERE r.app_id = a.id),
                    '[1,2,3,4,5]'::jsonb);
                ALTER TABLE applications ALTER COLUMN supported_platforms DROP DEFAULT;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "supported_platforms",
                table: "applications");
        }
    }
}
