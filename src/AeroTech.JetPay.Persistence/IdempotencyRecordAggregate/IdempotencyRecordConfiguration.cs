using AeroTech.JetPay.Domain.IdempotencyRecordAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroTech.JetPay.Persistence.IdempotencyRecordAggregate
{
    public sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
    {
        public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
        {
            builder.ToTable("IdempotencyRecords");
            builder.HasKey(record => record.Id);
            builder.Property(record => record.Id).ValueGeneratedNever();

            builder.Property(record => record.Scope).HasMaxLength(64).IsRequired();
            builder.Property(record => record.IdempotencyKey).HasMaxLength(100).IsRequired();
            builder.Property(record => record.RequestFingerprint).HasMaxLength(64).IsRequired();
            builder.Property(record => record.PaymentIntentId).HasMaxLength(64).IsRequired();

            builder.HasIndex(record => new { record.Operation, record.Scope, record.IdempotencyKey }).IsUnique();
            builder.HasIndex(record => record.PaymentIntentId);
        }
    }
}
