using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CryptoTracker.Migrations;

/// <summary>Single initial schema: Users, per-user Transactions and Holdings (replaces legacy multi-step migrations).</summary>
public partial class InitialPerUserSchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Users",
            columns: table => new
            {
                Id = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                Email = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                PasswordHash = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table => { table.PrimaryKey("PK_Users", x => x.Id); });

        migrationBuilder.CreateIndex(
            name: "IX_Users_Email",
            table: "Users",
            column: "Email",
            unique: true);

        migrationBuilder.CreateTable(
            name: "Transactions",
            columns: table => new
            {
                Id = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                UserId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                Symbol = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                Type = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                Quantity = table.Column<decimal>(type: "TEXT", nullable: false),
                PriceAtTransaction = table.Column<decimal>(type: "TEXT", nullable: false),
                Fee = table.Column<decimal>(type: "TEXT", nullable: false),
                Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                BaseCurrency = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                Notes = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Transactions", x => x.Id);
                table.ForeignKey(
                    name: "FK_Transactions_Users_UserId",
                    column: x => x.UserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Transactions_UserId",
            table: "Transactions",
            column: "UserId");

        migrationBuilder.CreateTable(
            name: "Holdings",
            columns: table => new
            {
                UserId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                Symbol = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                CurrentQuantity = table.Column<decimal>(type: "TEXT", nullable: false),
                Source = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                LastUpdated = table.Column<DateTime>(type: "TEXT", nullable: false),
                SellTargetUsd = table.Column<decimal>(type: "TEXT", nullable: true),
                BuyZoneUsd = table.Column<decimal>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Holdings", x => new { x.UserId, x.Symbol });
                table.ForeignKey(
                    name: "FK_Holdings_Users_UserId",
                    column: x => x.UserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "Holdings");
        migrationBuilder.DropTable(name: "Transactions");
        migrationBuilder.DropTable(name: "Users");
    }
}
