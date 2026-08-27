using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KlyonHubPlatform.Infra.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSnapshotBillingCycleToPayment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SnapshotBillingCycle",
                table: "Payments",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.Sql(@"
                UPDATE Payments p
                INNER JOIN Subscriptions s ON p.SubscriptionId = s.Id
                SET p.SnapshotBillingCycle = s.BillingCycle
                WHERE p.SnapshotBillingCycle IS NULL
                  AND p.SubscriptionId IS NOT NULL
                  AND s.BillingCycle IS NOT NULL
                  AND TRIM(s.BillingCycle) <> '';
            ");

            migrationBuilder.Sql(@"
                UPDATE Payments p
                INNER JOIN Subscriptions s ON p.SubscriptionId = s.Id
                INNER JOIN Contracts c ON s.ContractId = c.Id
                SET p.SnapshotBillingCycle = CASE
                    WHEN c.CardPlanType IN (2, 3, 5) THEN 'Monthly'
                    WHEN c.CardPlanType IN (1, 4) THEN 'Yearly'
                    ELSE NULL
                END
                WHERE p.SnapshotBillingCycle IS NULL
                  AND p.SubscriptionId IS NOT NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SnapshotBillingCycle",
                table: "Payments");
        }
    }
}
