using AeroTech.JetPay.Domain.PaymentSessionAggregate.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroTech.JetPay.Persistence.PaymentSessionAggregate
{
    public sealed class ProviderPaymentAttemptConfiguration : IEntityTypeConfiguration<ProviderPaymentAttempt>
    {
        public void Configure(EntityTypeBuilder<ProviderPaymentAttempt> builder)
        {
            builder.ToTable("ProviderPaymentAttempts");
            builder.HasKey(attempt => attempt.Id);
            builder.Property(attempt => attempt.Id).ValueGeneratedNever();

            builder.Property(attempt => attempt.PaymentIntentId).HasMaxLength(64).IsRequired();
            builder.Property(attempt => attempt.ProviderProfileId).HasMaxLength(64).IsRequired();
            builder.Property(attempt => attempt.ProviderProfileVersion).HasMaxLength(32).IsRequired();
            builder.Property(attempt => attempt.IdempotencyKey).HasMaxLength(100).IsRequired();
            builder.Property(attempt => attempt.ProviderTransactionRef).HasMaxLength(128);
            builder.Property(attempt => attempt.FailureCode).HasMaxLength(64);
            builder.Property(attempt => attempt.FailureReason).HasMaxLength(512);

            builder.Ignore(attempt => attempt.IsMoneyVerified);
            builder.Ignore(attempt => attempt.IsUnresolved);

            builder.HasIndex(attempt => new { attempt.PaymentIntentId, attempt.AttemptNumber }).IsUnique();
            builder.HasIndex(attempt => attempt.IdempotencyKey).IsUnique();
            builder.HasIndex(attempt => new { attempt.Status, attempt.ReversalExpectedAt });
        }
    }
}
