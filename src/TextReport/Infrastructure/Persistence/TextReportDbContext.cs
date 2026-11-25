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
            order.HasOne(x => x.Payment).WithOne().HasForeignKey<Payment>(x => x.OrderId);
        });

        modelBuilder.Entity<Payment>(payment =>
        {
            payment.Property(x => x.Amount).HasPrecision(12, 2);
            payment.Property(x => x.Currency).HasMaxLength(3);
            payment.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        });
    }
}
