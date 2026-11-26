using Microsoft.EntityFrameworkCore;
using TextReport.Features.Orders;
using TextReport.Features.Payments;

namespace TextReport.Infrastructure.Persistence;

internal sealed class TextReportDbContext(DbContextOptions<TextReportDbContext> options)
    : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Payment> Payments => Set<Payment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(order =>
        {
            order.Property(x => x.Email).HasMaxLength(254);
            order.Property(x => x.InputText).HasMaxLength(15_000);
            order.HasOne(x => x.Payment).WithOne(x => x.Order).HasForeignKey<Payment>(x => x.OrderId);
            order.OwnsOne(x => x.ProcessingAttempts);
            order.OwnsOne(x => x.EmailAttempts);
            order.Navigation(x => x.ProcessingAttempts).IsRequired();
            order.Navigation(x => x.EmailAttempts).IsRequired();
            order.HasIndex(x => new { x.ProcessedAt, x.CreatedAt });
            order.HasIndex(x => new { x.EmailSentAt, x.ProcessedAt });
        });

        modelBuilder.Entity<Payment>(payment =>
        {
            payment.Property(x => x.Amount).HasPrecision(12, 2);
            payment.Property(x => x.Currency).HasMaxLength(3);
            payment.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            payment.OwnsOne(x => x.Attempts);
            payment.Navigation(x => x.Attempts).IsRequired();
            payment.HasIndex(x => new { x.Status, x.CreatedAt });
        });
    }
}
