using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ashveil.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMonsterSpawns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "monster_spawns",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    zone = table.Column<string>(type: "text", nullable: false),
                    monster = table.Column<string>(type: "text", nullable: false),
                    x = table.Column<float>(type: "real", nullable: false),
                    z = table.Column<float>(type: "real", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_monster_spawns", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "monster_spawns");
        }
    }
}
