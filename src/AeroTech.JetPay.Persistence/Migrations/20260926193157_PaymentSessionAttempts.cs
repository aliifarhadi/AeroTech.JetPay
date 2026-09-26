using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AeroTech.JetPay.Persistence.Migrations
{
    public partial class PaymentSessionAttempts : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PaymentIntents_PaymentSessionId_Sequence",
                schema: "Payment",
                table: "PaymentIntents");

            migrationBuilder.DropColumn(
                name: "EarliestGuaranteeExpiry",
                schema: "Payment",
                table: "PaymentSessions");

            migrationBuilder.DropColumn(
                name: "PaymentIntentIds",
                schema: "Payment",
                table: "PaymentSessions");

            migrationBuilder.DropColumn(
                name: "CaptureMode",
                schema: "Payment",
                table: "PaymentIntents");

            migrationBuilder.DropColumn(
                name: "ProviderReference",
                schema: "Payment",
                table: "PaymentIntents");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                schema: "Payment",
                table: "PaymentIntents");

            migrationBuilder.DropColumn(
                name: "Sequence",
                schema: "Payment",
                table: "PaymentIntents");

            migrationBuilder.DropColumn(
                name: "RefundedAmount",
                schema: "Payment",
                table: "PaymentSessions");

            migrationBuilder.AddColumn<decimal>(
                name: "OutstandingAmount",
                schema: "Payment",
                table: "PaymentSessions",
                type: "decimal(19,4)",
                precision: 19,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<long>(
                name: "IssuerLegalEntityId",
                schema: "Payment",
                table: "PaymentSessions",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "SelectionMode",
                schema: "Payment",
                table: "PaymentSessions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "FundingReference",
                schema: "Payment",
                table: "PaymentIntents",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PaidUnappliedAt",
                schema: "Payment",
                table: "PaymentIntents",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProviderPaymentAttempts",
                schema: "Payment",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    PaymentIntentId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    AttemptNumber = table.Column<int>(type: "int", nullable: false),
                    ProviderProfileId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ProviderProfileVersion = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ProviderTransactionRef = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CallbackReceivedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    VerifyDeadline = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    VerificationStartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    VerifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SettledAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReversalExpectedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReversedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UnknownSince = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FailureCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderPaymentAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProviderPaymentAttempts_PaymentIntents_PaymentIntentId",
                        column: x => x.PaymentIntentId,
                        principalSchema: "Payment",
                        principalTable: "PaymentIntents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentIntents_PaymentSessionId",
                schema: "Payment",
                table: "PaymentIntents",
                column: "PaymentSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProviderPaymentAttempts_IdempotencyKey",
                schema: "Payment",
                table: "ProviderPaymentAttempts",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProviderPaymentAttempts_PaymentIntentId_AttemptNumber",
                schema: "Payment",
                table: "ProviderPaymentAttempts",
                columns: new[] { "PaymentIntentId", "AttemptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProviderPaymentAttempts_Status_ReversalExpectedAt",
                schema: "Payment",
                table: "ProviderPaymentAttempts",
                columns: new[] { "Status", "ReversalExpectedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentIntents_PaymentSessions_PaymentSessionId",
                schema: "Payment",
                table: "PaymentIntents",
                column: "PaymentSessionId",
                principalSchema: "Payment",
                principalTable: "PaymentSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(@"
UPDATE [Payment].[PaymentSessions]
SET [OutstandingAmount] = CASE WHEN [RequiredAmount] > [GuaranteedAmount] THEN [RequiredAmount] - [GuaranteedAmount] ELSE 0 END,
    [SelectionMode] = CASE [InteractionMode] WHEN 1 THEN 1 WHEN 2 THEN 2 ELSE 3 END,
    [Status] = CASE [Status] WHEN 3 THEN 3 WHEN 4 THEN 3 WHEN 5 THEN 4 WHEN 6 THEN 5 WHEN 7 THEN 6 ELSE [Status] END;

UPDATE [Payment].[PaymentIntents]
SET [TenderType] = CASE [TenderType]
    WHEN 3 THEN 9
    WHEN 4 THEN 3
    WHEN 5 THEN 4
    WHEN 6 THEN 5
    WHEN 7 THEN 6
    WHEN 8 THEN 7
    WHEN 9 THEN 8
    WHEN 11 THEN 9
    WHEN 12 THEN 4
    WHEN 13 THEN 4
    ELSE [TenderType] END;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE [Payment].[PaymentSessions]
SET [Status] = CASE [Status] WHEN 3 THEN 4 WHEN 4 THEN 5 WHEN 5 THEN 6 WHEN 6 THEN 7 WHEN 7 THEN 8 ELSE [Status] END;

UPDATE [Payment].[PaymentIntents]
SET [TenderType] = CASE [TenderType]
    WHEN 3 THEN 4
    WHEN 4 THEN 5
    WHEN 5 THEN 6
    WHEN 6 THEN 7
    WHEN 7 THEN 8
    WHEN 8 THEN 9
    WHEN 9 THEN 11
    WHEN 11 THEN 10
    ELSE [TenderType] END;");

            migrationBuilder.DropForeignKey(
                name: "FK_PaymentIntents_PaymentSessions_PaymentSessionId",
                schema: "Payment",
                table: "PaymentIntents");

            migrationBuilder.DropTable(
                name: "ProviderPaymentAttempts",
                schema: "Payment");

            migrationBuilder.DropIndex(
                name: "IX_PaymentIntents_PaymentSessionId",
                schema: "Payment",
                table: "PaymentIntents");

            migrationBuilder.DropColumn(
                name: "IssuerLegalEntityId",
                schema: "Payment",
                table: "PaymentSessions");

            migrationBuilder.DropColumn(
                name: "SelectionMode",
                schema: "Payment",
                table: "PaymentSessions");

            migrationBuilder.DropColumn(
                name: "FundingReference",
                schema: "Payment",
                table: "PaymentIntents");

            migrationBuilder.DropColumn(
                name: "PaidUnappliedAt",
                schema: "Payment",
                table: "PaymentIntents");

            migrationBuilder.DropColumn(
                name: "OutstandingAmount",
                schema: "Payment",
                table: "PaymentSessions");

            migrationBuilder.AddColumn<decimal>(
                name: "RefundedAmount",
                schema: "Payment",
                table: "PaymentSessions",
                type: "decimal(19,4)",
                precision: 19,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EarliestGuaranteeExpiry",
                schema: "Payment",
                table: "PaymentSessions",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentIntentIds",
                schema: "Payment",
                table: "PaymentSessions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "CaptureMode",
                schema: "Payment",
                table: "PaymentIntents",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ProviderReference",
                schema: "Payment",
                table: "PaymentIntents",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                schema: "Payment",
                table: "PaymentIntents",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<int>(
                name: "Sequence",
                schema: "Payment",
                table: "PaymentIntents",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentIntents_PaymentSessionId_Sequence",
                schema: "Payment",
                table: "PaymentIntents",
                columns: new[] { "PaymentSessionId", "Sequence" },
                unique: true);
        }
    }
}
