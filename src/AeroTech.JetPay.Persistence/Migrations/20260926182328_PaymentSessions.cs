using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AeroTech.JetPay.Persistence.Migrations
{
    public partial class PaymentSessions : Migration
    {
        private const string Schema = "Payment";

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            Archive(migrationBuilder, "PaymentIntents", "PaymentIntentsLegacyArchive");
            Archive(migrationBuilder, "IdempotencyRecords", "IdempotencyRecordsLegacyArchive");

            migrationBuilder.CreateTable(
                name: "PaymentSessions",
                schema: Schema,
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
                    InitiatorSalesChannel = table.Column<int>(type: "int", nullable: false),
                    InitiatorActorType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    InitiatorActorId = table.Column<long>(type: "bigint", nullable: false),
                    InitiatorOfficeId = table.Column<long>(type: "bigint", nullable: true),
                    RequiredAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    CurrencyId = table.Column<int>(type: "int", nullable: false),
                    AssuranceRequirement = table.Column<int>(type: "int", nullable: false),
                    InteractionMode = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    GuaranteedAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    CapturedAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    RefundedAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    EarliestGuaranteeExpiry = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FailureCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PaymentIntentIds = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdatedBy = table.Column<long>(type: "bigint", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PaymentIntents",
                schema: Schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PaymentSessionId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    PaymentMethodOptionId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TenderType = table.Column<int>(type: "int", nullable: false),
                    RequestedAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    CurrencyId = table.Column<int>(type: "int", nullable: false),
                    CaptureMode = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AuthorizedAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    GuaranteedAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    CapturedAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    RefundedAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    GuaranteeExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    NextActionType = table.Column<int>(type: "int", nullable: true),
                    NextActionUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    NextActionHttpMethod = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    NextActionFormFields = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NextActionExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FailureCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    ProviderReference = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
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

            migrationBuilder.CreateTable(
                name: "IdempotencyRecords",
                schema: Schema,
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    Operation = table.Column<int>(type: "int", nullable: false),
                    Scope = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RequestFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PaymentSessionId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PaymentIntentIds = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdatedBy = table.Column<long>(type: "bigint", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdempotencyRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentSessions_OrderId_CommercialVersion",
                schema: Schema,
                table: "PaymentSessions",
                columns: new[] { "OrderId", "CommercialVersion" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentSessions_PayableInstructionId",
                schema: Schema,
                table: "PaymentSessions",
                column: "PayableInstructionId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentSessions_Status_ExpiresAt",
                schema: Schema,
                table: "PaymentSessions",
                columns: new[] { "Status", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentIntents_PaymentSessionId_Sequence",
                schema: Schema,
                table: "PaymentIntents",
                columns: new[] { "PaymentSessionId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentIntents_Status",
                schema: Schema,
                table: "PaymentIntents",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyRecords_Operation_Scope_IdempotencyKey",
                schema: Schema,
                table: "IdempotencyRecords",
                columns: new[] { "Operation", "Scope", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyRecords_PaymentSessionId",
                schema: Schema,
                table: "IdempotencyRecords",
                column: "PaymentSessionId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "IdempotencyRecords", schema: Schema);
            migrationBuilder.DropTable(name: "PaymentIntents", schema: Schema);
            migrationBuilder.DropTable(name: "PaymentSessions", schema: Schema);

            Restore(migrationBuilder, "PaymentIntentsLegacyArchive", "PaymentIntents");
            Restore(migrationBuilder, "IdempotencyRecordsLegacyArchive", "IdempotencyRecords");
        }

        private static void Archive(MigrationBuilder migrationBuilder, string table, string archive)
        {
            migrationBuilder.RenameTable(name: table, schema: Schema, newName: archive, newSchema: Schema);
            migrationBuilder.Sql($"EXEC sp_rename N'[{Schema}].[PK_{table}]', N'PK_{archive}', N'OBJECT';");
        }

        private static void Restore(MigrationBuilder migrationBuilder, string archive, string table)
        {
            migrationBuilder.RenameTable(name: archive, schema: Schema, newName: table, newSchema: Schema);
            migrationBuilder.Sql($"EXEC sp_rename N'[{Schema}].[PK_{archive}]', N'PK_{table}', N'OBJECT';");
        }
    }
}
