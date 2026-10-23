
using Fogerithm.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Fogerithm.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : DbContext(options)
{
    public DbSet<Part> Parts => Set<Part>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<BomLine> BomLines => Set<BomLine>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();

    public DbSet<CustomerOrder> Orders => Set<CustomerOrder>();
    public DbSet<OrderLine> OrderLines => Set<OrderLine>();
    public DbSet<ReservationLine> ReservationLines => Set<ReservationLine>();

    public DbSet<ProductionRun> ProductionRuns => Set<ProductionRun>();
    public DbSet<ProductionConsumption> ProductionConsumptions
        => Set<ProductionConsumption>();

    public DbSet<FinishedGoodsMovement> FinishedGoodsMovements
        => Set<FinishedGoodsMovement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Unique codes
        modelBuilder.Entity<Part>()
            .HasIndex(x => x.Code)
            .IsUnique();

        modelBuilder.Entity<Product>()
            .HasIndex(x => x.Code)
            .IsUnique();

        modelBuilder.Entity<ProductionRun>()
            .HasIndex(x => x.ProductionNumber)
            .IsUnique();

        // Decimal precision: parts and products
        modelBuilder.Entity<Part>()
            .Property(x => x.UnitCost)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Part>()
            .Property(x => x.OpeningStock)
            .HasPrecision(18, 4);

        modelBuilder.Entity<Part>()
            .Property(x => x.ReorderLevel)
            .HasPrecision(18, 4);

        modelBuilder.Entity<Product>()
            .Property(x => x.SellingPrice)
            .HasPrecision(18, 2);

        // Bill of Materials
        modelBuilder.Entity<BomLine>()
            .Property(x => x.QuantityPerProduct)
            .HasPrecision(18, 4);

        modelBuilder.Entity<BomLine>()
            .HasOne(x => x.Product)
            .WithMany(x => x.BomLines)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<BomLine>()
            .HasOne(x => x.Part)
            .WithMany()
            .HasForeignKey(x => x.PartId)
            .OnDelete(DeleteBehavior.Restrict);

        // Raw-material stock ledger
        modelBuilder.Entity<StockMovement>()
            .Property(x => x.QuantityIn)
            .HasPrecision(18, 4);

        modelBuilder.Entity<StockMovement>()
            .Property(x => x.QuantityOut)
            .HasPrecision(18, 4);

        modelBuilder.Entity<StockMovement>()
            .Property(x => x.UnitCost)
            .HasPrecision(18, 2);

        modelBuilder.Entity<StockMovement>()
            .HasOne(x => x.Part)
            .WithMany()
            .HasForeignKey(x => x.PartId)
            .OnDelete(DeleteBehavior.Restrict);

        // Customer orders
        modelBuilder.Entity<OrderLine>()
            .Property(x => x.Quantity)
            .HasPrecision(18, 4);

        modelBuilder.Entity<OrderLine>()
            .HasOne(x => x.Order)
            .WithMany(x => x.Lines)
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<OrderLine>()
            .HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        // Material reservations
        modelBuilder.Entity<ReservationLine>()
            .Property(x => x.Quantity)
            .HasPrecision(18, 4);

        modelBuilder.Entity<ReservationLine>()
            .HasOne(x => x.Reservation)
            .WithMany()
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ReservationLine>()
            .HasOne(x => x.Part)
            .WithMany()
            .HasForeignKey(x => x.PartId)
            .OnDelete(DeleteBehavior.Restrict);

        // Production runs
        modelBuilder.Entity<ProductionRun>()
            .Property(x => x.PlannedQuantity)
            .HasPrecision(18, 4);

        modelBuilder.Entity<ProductionRun>()
            .Property(x => x.CompletedQuantity)
            .HasPrecision(18, 4);

        modelBuilder.Entity<ProductionRun>()
            .HasOne(x => x.Order)
            .WithMany()
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ProductionRun>()
            .HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        // Actual and planned material consumption
        modelBuilder.Entity<ProductionConsumption>()
            .Property(x => x.PlannedQuantity)
            .HasPrecision(18, 4);

        modelBuilder.Entity<ProductionConsumption>()
            .Property(x => x.ActualQuantity)
            .HasPrecision(18, 4);

        modelBuilder.Entity<ProductionConsumption>()
            .HasOne(x => x.ProductionRun)
            .WithMany(x => x.Consumptions)
            .HasForeignKey(x => x.ProductionRunId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ProductionConsumption>()
            .HasOne(x => x.Part)
            .WithMany()
            .HasForeignKey(x => x.PartId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ProductionConsumption>()
            .HasOne(x => x.ActualPart)
            .WithMany()
            .HasForeignKey(x => x.ActualPartId)
            .OnDelete(DeleteBehavior.Restrict);

        // Finished-goods stock ledger
        modelBuilder.Entity<FinishedGoodsMovement>()
            .Property(x => x.QuantityIn)
            .HasPrecision(18, 4);

        modelBuilder.Entity<FinishedGoodsMovement>()
            .Property(x => x.QuantityOut)
            .HasPrecision(18, 4);

        modelBuilder.Entity<FinishedGoodsMovement>()
            .HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
