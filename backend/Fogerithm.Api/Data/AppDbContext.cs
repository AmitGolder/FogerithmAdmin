using Fogerithm.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Fogerithm.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Part> Parts => Set<Part>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<BomLine> BomLines => Set<BomLine>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<CustomerOrder> Orders => Set<CustomerOrder>();
    public DbSet<OrderLine> OrderLines => Set<OrderLine>();
    public DbSet<ReservationLine> ReservationLines => Set<ReservationLine>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Part>().HasIndex(x => x.Code).IsUnique();
        modelBuilder.Entity<Product>().HasIndex(x => x.Code).IsUnique();
        modelBuilder.Entity<Part>().Property(x => x.UnitCost).HasPrecision(18, 2);
        modelBuilder.Entity<BomLine>().Property(x => x.QuantityPerProduct).HasPrecision(18, 4);
        modelBuilder.Entity<StockMovement>().Property(x => x.QuantityIn).HasPrecision(18, 4);
        modelBuilder.Entity<StockMovement>().Property(x => x.QuantityOut).HasPrecision(18, 4);
        modelBuilder.Entity<OrderLine>().Property(x => x.Quantity).HasPrecision(18, 4);
        modelBuilder.Entity<ReservationLine>().Property(x => x.Quantity).HasPrecision(18, 4);
        modelBuilder.Entity<ReservationLine>()
            .HasOne(x => x.Reservation).WithMany()
            .HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<BomLine>()
            .HasOne(x => x.Product).WithMany(x => x.BomLines)
            .HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
    }
}
