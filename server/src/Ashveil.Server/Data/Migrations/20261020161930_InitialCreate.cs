using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Ashveil.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "accounts",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    username = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_accounts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "alignment_histories",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    entity_kind = table.Column<string>(type: "text", nullable: false),
                    entity_id = table.Column<long>(type: "bigint", nullable: false),
                    delta = table.Column<int>(type: "integer", nullable: false),
                    after = table.Column<int>(type: "integer", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_alignment_histories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "bots",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "text", nullable: false),
                    class_type = table.Column<string>(type: "text", nullable: false),
                    gender = table.Column<string>(type: "text", nullable: false),
                    level = table.Column<int>(type: "integer", nullable: false),
                    zone = table.Column<string>(type: "text", nullable: false),
                    temperament = table.Column<string>(type: "text", nullable: false),
                    alignment = table.Column<int>(type: "integer", nullable: false),
                    purple_until = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_dead = table.Column<bool>(type: "boolean", nullable: false),
                    respawn_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    kills = table.Column<int>(type: "integer", nullable: false),
                    deaths = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bots", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "enhance_histories",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    character_id = table.Column<long>(type: "bigint", nullable: false),
                    item_id = table.Column<long>(type: "bigint", nullable: false),
                    template_id = table.Column<string>(type: "text", nullable: false),
                    from_level = table.Column<int>(type: "integer", nullable: false),
                    to_level = table.Column<int>(type: "integer", nullable: false),
                    result = table.Column<string>(type: "text", nullable: false),
                    gold_cost = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_enhance_histories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "items",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    template_id = table.Column<string>(type: "text", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    enhance_level = table.Column<int>(type: "integer", nullable: false),
                    owner_kind = table.Column<string>(type: "text", nullable: false),
                    owner_id = table.Column<long>(type: "bigint", nullable: false),
                    equipped_slot = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_items", x => x.id);
                    table.CheckConstraint("ck_items_quantity_positive", "quantity > 0");
                });

            migrationBuilder.CreateTable(
                name: "pk_records",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    killer_kind = table.Column<string>(type: "text", nullable: false),
                    killer_id = table.Column<long>(type: "bigint", nullable: false),
                    killer_name = table.Column<string>(type: "text", nullable: false),
                    victim_kind = table.Column<string>(type: "text", nullable: false),
                    victim_id = table.Column<long>(type: "bigint", nullable: false),
                    victim_name = table.Column<string>(type: "text", nullable: false),
                    victim_was_innocent = table.Column<bool>(type: "boolean", nullable: false),
                    alignment_delta = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pk_records", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "spawn_cooldowns",
                columns: table => new
                {
                    character_id = table.Column<long>(type: "bigint", nullable: false),
                    spawn_id = table.Column<string>(type: "text", nullable: false),
                    last_killed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_spawn_cooldowns", x => new { x.character_id, x.spawn_id });
                });

            migrationBuilder.CreateTable(
                name: "characters",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    account_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    name_key = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    class_type = table.Column<string>(type: "text", nullable: false),
                    gender = table.Column<string>(type: "text", nullable: false),
                    level = table.Column<int>(type: "integer", nullable: false),
                    exp = table.Column<long>(type: "bigint", nullable: false),
                    gold = table.Column<long>(type: "bigint", nullable: false),
                    pos_x = table.Column<float>(type: "real", nullable: false),
                    pos_y = table.Column<float>(type: "real", nullable: false),
                    pos_z = table.Column<float>(type: "real", nullable: false),
                    alignment = table.Column<int>(type: "integer", nullable: false),
                    purple_until = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    pk_kills = table.Column<int>(type: "integer", nullable: false),
                    pk_deaths = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_characters", x => x.id);
                    table.CheckConstraint("ck_characters_gold_non_negative", "gold >= 0");
                    table.ForeignKey(
                        name: "fk_characters_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    account_id = table.Column<long>(type: "bigint", nullable: false),
                    token_hash = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_refresh_tokens", x => x.id);
                    table.ForeignKey(
                        name: "fk_refresh_tokens_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ground_items",
                columns: table => new
                {
                    item_id = table.Column<long>(type: "bigint", nullable: false),
                    zone = table.Column<string>(type: "text", nullable: false),
                    x = table.Column<float>(type: "real", nullable: false),
                    z = table.Column<float>(type: "real", nullable: false),
                    dropped_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    dropped_by_kind = table.Column<string>(type: "text", nullable: false),
                    dropped_by_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ground_items", x => x.item_id);
                    table.ForeignKey(
                        name: "fk_ground_items_items_item_id",
                        column: x => x.item_id,
                        principalTable: "items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ledger_entries",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    character_id = table.Column<long>(type: "bigint", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    delta = table.Column<long>(type: "bigint", nullable: false),
                    balance_after = table.Column<long>(type: "bigint", nullable: false),
                    related_type = table.Column<string>(type: "text", nullable: true),
                    related_id = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ledger_entries", x => x.id);
                    table.ForeignKey(
                        name: "fk_ledger_entries_characters_character_id",
                        column: x => x.character_id,
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_accounts_username",
                table: "accounts",
                column: "username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_alignment_histories_entity_kind_entity_id",
                table: "alignment_histories",
                columns: new[] { "entity_kind", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "ix_bots_name",
                table: "bots",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_bots_zone",
                table: "bots",
                column: "zone");

            migrationBuilder.CreateIndex(
                name: "ix_characters_account_id",
                table: "characters",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_characters_name_key",
                table: "characters",
                column: "name_key",
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_enhance_histories_character_id",
                table: "enhance_histories",
                column: "character_id");

            migrationBuilder.CreateIndex(
                name: "ix_ground_items_zone_expires_at",
                table: "ground_items",
                columns: new[] { "zone", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "ix_items_owner_kind_owner_id",
                table: "items",
                columns: new[] { "owner_kind", "owner_id" });

            migrationBuilder.CreateIndex(
                name: "ix_ledger_entries_character_id_id",
                table: "ledger_entries",
                columns: new[] { "character_id", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_pk_records_killer_kind_killer_id",
                table: "pk_records",
                columns: new[] { "killer_kind", "killer_id" });

            migrationBuilder.CreateIndex(
                name: "ix_pk_records_victim_kind_victim_id",
                table: "pk_records",
                columns: new[] { "victim_kind", "victim_id" });

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_account_id",
                table: "refresh_tokens",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_token_hash",
                table: "refresh_tokens",
                column: "token_hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "alignment_histories");

            migrationBuilder.DropTable(
                name: "bots");

            migrationBuilder.DropTable(
                name: "enhance_histories");

            migrationBuilder.DropTable(
                name: "ground_items");

            migrationBuilder.DropTable(
                name: "ledger_entries");

            migrationBuilder.DropTable(
                name: "pk_records");

            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropTable(
                name: "spawn_cooldowns");

            migrationBuilder.DropTable(
                name: "items");

            migrationBuilder.DropTable(
                name: "characters");

            migrationBuilder.DropTable(
                name: "accounts");
        }
    }
}
