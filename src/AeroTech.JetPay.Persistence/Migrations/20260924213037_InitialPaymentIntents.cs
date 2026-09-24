using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AeroTech.JetPay.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialPaymentIntents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "Payment");

            migrationBuilder.EnsureSchema(
                name: "dbo");

            migrationBuilder.CreateTable(
                name: "IdempotencyRecords",
                schema: "Payment",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    Operation = table.Column<int>(type: "int", nullable: false),
                    Scope = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RequestFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PaymentIntentId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdatedBy = table.Column<long>(type: "bigint", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdempotencyRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InboxMessages",
                schema: "dbo",
                columns: table => new
                {
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Consumer = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    MessageType = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ReceivedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboxMessages", x => new { x.MessageId, x.Consumer });
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MessageType = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", maxLength: 256, nullable: false),
                    OccurredOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ProcessedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PaymentIntents",
                schema: "Payment",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PayableInstructionId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OrderId = table.Column<long>(type: "bigint", nullable: false),
                    OrderReference = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CommercialVersion = table.Column<int>(type: "int", nullable: false),
                    Purpose = table.Column<int>(type: "int", nullable: false),
                    PayerType = table.Column<int>(type: "int", nullable: false),
                    PayerId = table.Column<long>(type: "bigint", nullable: false),
                    RequestedAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    CurrencyId = table.Column<int>(type: "int", nullable: false),
                    RequiredGuarantee = table.Column<int>(type: "int", nullable: false),
                    CaptureMode = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AuthorizedAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    GuaranteedAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    CapturedAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    RefundedAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    GuaranteeExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IntentExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SelectedTenderType = table.Column<int>(type: "int", nullable: true),
                    SelectedPaymentMethodOptionId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    NextActionType = table.Column<int>(type: "int", nullable: true),
                    NextActionUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    NextActionHttpMethod = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    NextActionFormFields = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NextActionExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FailureCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdatedBy = table.Column<long>(type: "bigint", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentIntents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyRecords_Operation_Scope_IdempotencyKey",
                schema: "Payment",
                table: "IdempotencyRecords",
                columns: new[] { "Operation", "Scope", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyRecords_PaymentIntentId",
                schema: "Payment",
                table: "IdempotencyRecords",
                column: "PaymentIntentId");

            migrationBuilder.CreateIndex(
                name: "IX_InboxMessages_ReceivedOn",
                schema: "dbo",
                table: "InboxMessages",
                column: "ReceivedOn");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_ProcessedOn",
                schema: "dbo",
                table: "OutboxMessages",
                column: "ProcessedOn");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentIntents_OrderId_CommercialVersion",
                schema: "Payment",
                table: "PaymentIntents",
                columns: new[] { "OrderId", "CommercialVersion" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentIntents_PayableInstructionId",
                schema: "Payment",
                table: "PaymentIntents",
                column: "PayableInstructionId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentIntents_Status",
                schema: "Payment",
                table: "PaymentIntents",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IdempotencyRecords",
                schema: "Payment");

            migrationBuilder.DropTable(
                name: "InboxMessages",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "OutboxMessages",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PaymentIntents",
                schema: "Payment");
        }
    }
}
