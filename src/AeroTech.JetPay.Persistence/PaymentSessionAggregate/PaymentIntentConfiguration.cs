using System.Text.Json;
using AeroTech.JetPay.Domain.PaymentSessionAggregate.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AeroTech.JetPay.Persistence.PaymentSessionAggregate
{
    public sealed class PaymentIntentConfiguration : IEntityTypeConfiguration<PaymentIntent>
    {
        private static readonly ValueConverter<IReadOnlyDictionary<string, string>?, string?> FormFieldsConverter = new(
            fields => fields == null ? null : JsonSerializer.Serialize(fields, (JsonSerializerOptions?)null),
            json => json == null ? null : JsonSerializer.Deserialize<Dictionary<string, string>>(json, (JsonSerializerOptions?)null));

        private static readonly ValueComparer<IReadOnlyDictionary<string, string>?> FormFieldsComparer = new(
            (left, right) => left == null ? right == null : right != null && left.Count == right.Count && !left.Except(right).Any(),
            fields => fields == null ? 0 : fields.Aggregate(0, (hash, pair) => HashCode.Combine(hash, pair.Key, pair.Value)),
            fields => fields == null ? null : new Dictionary<string, string>(fields));

        public void Configure(EntityTypeBuilder<PaymentIntent> builder)
        {
            builder.ToTable("PaymentIntents");
            builder.HasKey(intent => intent.Id);
            builder.Property(intent => intent.Id).HasMaxLength(64).ValueGeneratedNever();

            builder.Property(intent => intent.PaymentSessionId).HasMaxLength(64).IsRequired();
            builder.Property(intent => intent.PaymentMethodOptionId).HasMaxLength(64).IsRequired();
            builder.Property(intent => intent.FundingReference).HasMaxLength(128);
            builder.Property(intent => intent.FailureCode).HasMaxLength(64);
            builder.Property(intent => intent.FailureReason).HasMaxLength(512);

            builder.OwnsOne(intent => intent.NextAction, action =>
            {
                action.WithOwner().HasForeignKey("Id");
                action.Property<string>("Id").HasMaxLength(64);
                action.Property(customerAction => customerAction.Type).HasColumnName("NextActionType");
                action.Property(customerAction => customerAction.Url).HasColumnName("NextActionUrl").HasMaxLength(2048);
                action.Property(customerAction => customerAction.HttpMethod).HasColumnName("NextActionHttpMethod").HasMaxLength(16);
                action.Property(customerAction => customerAction.ExpiresAt).HasColumnName("NextActionExpiresAt");
                action.Property(customerAction => customerAction.FormFields)
                    .HasColumnName("NextActionFormFields")
                    .HasColumnType("nvarchar(max)")
                    .HasConversion(FormFieldsConverter, FormFieldsComparer);
            });

            builder.HasMany(intent => intent.ProviderAttempts)
                .WithOne()
                .HasForeignKey(attempt => attempt.PaymentIntentId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Navigation(intent => intent.ProviderAttempts)
                .HasField("_providerAttempts")
                .UsePropertyAccessMode(PropertyAccessMode.Field);

            builder.Ignore(intent => intent.IsOpen);
            builder.Ignore(intent => intent.HoldsFunding);
            builder.Ignore(intent => intent.HasUnresolvedProviderEffect);
            builder.Ignore(intent => intent.CurrentAttempt);
            builder.Ignore(intent => intent.AwaitsDispatch);

            builder.HasIndex(intent => intent.Status);
        }
    }
}
