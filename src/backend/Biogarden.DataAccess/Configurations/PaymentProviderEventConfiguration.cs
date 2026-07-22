using Biogarden.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Biogarden.DataAccess.Configurations;

public class PaymentProviderEventConfiguration : IEntityTypeConfiguration<PaymentProviderEvent>
{
    public void Configure(EntityTypeBuilder<PaymentProviderEvent> eventBuilder)
    {
        eventBuilder.ToTable("PaymentProviderEvents");

        eventBuilder.HasKey(paymentEvent => paymentEvent.Id);

        eventBuilder.Property(paymentEvent => paymentEvent.Provider)
            .HasMaxLength(50)
            .IsRequired();

        eventBuilder.Property(paymentEvent => paymentEvent.EventKey)
            .HasMaxLength(300)
            .IsRequired();

        eventBuilder.Property(paymentEvent => paymentEvent.CheckoutId)
            .HasMaxLength(200)
            .IsRequired();

        eventBuilder.Property(paymentEvent => paymentEvent.EventType)
            .HasMaxLength(100)
            .IsRequired();

        eventBuilder.Property(paymentEvent => paymentEvent.Status)
            .HasMaxLength(50);

        eventBuilder.Property(paymentEvent => paymentEvent.ReceivedAtUtc)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        eventBuilder.Property(paymentEvent => paymentEvent.ProcessedAtUtc)
            .HasColumnType("datetime2");

        eventBuilder.HasIndex(paymentEvent => new { paymentEvent.Provider, paymentEvent.EventKey })
            .IsUnique();

        eventBuilder.HasIndex(paymentEvent => paymentEvent.CheckoutId);
        eventBuilder.HasIndex(paymentEvent => paymentEvent.ReceivedAtUtc);
    }
}
