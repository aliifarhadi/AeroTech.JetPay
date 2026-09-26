using AeroTech.JetPay.Domain.PaymentSessionAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroTech.JetPay.Persistence.PaymentSessionAggregate
{
    public sealed class PaymentSessionConfiguration : IEntityTypeConfiguration<PaymentSession>
    {
        public void Configure(EntityTypeBuilder<PaymentSession> builder)
        {
            builder.ToTable("PaymentSessions");
            builder.HasKey(session => session.Id);
            builder.Property(session => session.Id).HasMaxLength(64).ValueGeneratedNever();

            builder.Property(session => session.PayableInstructionId).HasMaxLength(100).IsRequired();
            builder.Property(session => session.OrderReference).HasMaxLength(64).IsRequired();
            builder.Property(session => session.FailureCode).HasMaxLength(64);

            builder.OwnsOne(session => session.InitiatorContext, initiator =>
            {
                initiator.WithOwner().HasForeignKey("Id");
                initiator.Property<string>("Id").HasMaxLength(64);
                initiator.Property(context => context.SalesChannel).HasColumnName("InitiatorSalesChannel");
                initiator.Property(context => context.ActorType).HasColumnName("InitiatorActorType").HasMaxLength(64).IsRequired();
                initiator.Property(context => context.ActorId).HasColumnName("InitiatorActorId");
                initiator.Property(context => context.OfficeId).HasColumnName("InitiatorOfficeId");
            });

            builder.Navigation(session => session.InitiatorContext).IsRequired();

            builder.PrimitiveCollection(session => session.PaymentIntentIds)
                .HasField("_paymentIntentIds")
                .UsePropertyAccessMode(PropertyAccessMode.Field)
                .IsRequired();

            builder.Ignore(session => session.IsTerminal);
            builder.Ignore(session => session.OutstandingGuaranteeAmount);

            builder.HasIndex(session => session.PayableInstructionId);
            builder.HasIndex(session => new { session.OrderId, session.CommercialVersion });
            builder.HasIndex(session => new { session.Status, session.ExpiresAt });
        }
    }
}
