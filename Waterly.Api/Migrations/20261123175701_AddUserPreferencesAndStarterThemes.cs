using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Waterly.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddUserPreferencesAndStarterThemes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "LastActiveDate",
                table: "UserProgress",
                type: "date",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "UserPreferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ColorScheme = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ActiveBottleThemeId = table.Column<int>(type: "integer", nullable: true),
                    ActiveProfileFrameId = table.Column<int>(type: "integer", nullable: true),
                    ActiveWaterAnimationId = table.Column<int>(type: "integer", nullable: true),
                    ActiveBadgeEffectId = table.Column<int>(type: "integer", nullable: true),
                    ActiveMascotAccessoryId = table.Column<int>(type: "integer", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPreferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserPreferences_Rewards_ActiveBadgeEffectId",
                        column: x => x.ActiveBadgeEffectId,
                        principalTable: "Rewards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserPreferences_Rewards_ActiveBottleThemeId",
                        column: x => x.ActiveBottleThemeId,
                        principalTable: "Rewards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserPreferences_Rewards_ActiveMascotAccessoryId",
                        column: x => x.ActiveMascotAccessoryId,
                        principalTable: "Rewards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserPreferences_Rewards_ActiveProfileFrameId",
                        column: x => x.ActiveProfileFrameId,
                        principalTable: "Rewards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserPreferences_Rewards_ActiveWaterAnimationId",
                        column: x => x.ActiveWaterAnimationId,
                        principalTable: "Rewards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserPreferences_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Rewards",
                columns: new[] { "Id", "AssetKey", "Code", "Description", "Name", "RequiredAchievementCode", "RequiredLevel", "RewardType" },
                values: new object[,]
                {
                    { 13, "bottle_classic", "BOTTLE_CLASSIC", "Waterly'nin klasik şişe teması.", "Klasik Şişe", null, 1, "BottleTheme" },
                    { 14, "bottle_ocean", "BOTTLE_OCEAN", "Derin mavi okyanus teması.", "Okyanus Şişe", null, 1, "BottleTheme" },
                    { 15, "bottle_mint", "BOTTLE_MINT", "Ferah nane yeşili tema.", "Nane Şişe", null, 1, "BottleTheme" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserPreferences_ActiveBadgeEffectId",
                table: "UserPreferences",
                column: "ActiveBadgeEffectId");

            migrationBuilder.CreateIndex(
                name: "IX_UserPreferences_ActiveBottleThemeId",
                table: "UserPreferences",
                column: "ActiveBottleThemeId");

            migrationBuilder.CreateIndex(
                name: "IX_UserPreferences_ActiveMascotAccessoryId",
                table: "UserPreferences",
                column: "ActiveMascotAccessoryId");

            migrationBuilder.CreateIndex(
                name: "IX_UserPreferences_ActiveProfileFrameId",
                table: "UserPreferences",
                column: "ActiveProfileFrameId");

            migrationBuilder.CreateIndex(
                name: "IX_UserPreferences_ActiveWaterAnimationId",
                table: "UserPreferences",
                column: "ActiveWaterAnimationId");

            migrationBuilder.CreateIndex(
                name: "IX_UserPreferences_UserId",
                table: "UserPreferences",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserPreferences");

            migrationBuilder.DeleteData(
                table: "Rewards",
                keyColumn: "Id",
                keyValue: 13);

            migrationBuilder.DeleteData(
                table: "Rewards",
                keyColumn: "Id",
                keyValue: 14);

            migrationBuilder.DeleteData(
                table: "Rewards",
                keyColumn: "Id",
                keyValue: 15);

            migrationBuilder.DropColumn(
                name: "LastActiveDate",
                table: "UserProgress");
        }
    }
}
