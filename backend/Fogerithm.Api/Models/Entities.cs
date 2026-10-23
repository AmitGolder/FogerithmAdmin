
namespace Fogerithm.Api.Models;

public class Part
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public string Specification { get; set; } = "";
    public string Unit { get; set; } = "pc";
    public decimal UnitCost { get; set; }
    public decimal OpeningStock { get; set; }
    public decimal ReorderLevel { get; set; } = 5;
    public string Supplier { get; set; } = "";
    public string Remarks { get; set; } = "";
}

public class Product
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal SellingPrice { get; set; }
    public List<BomLine> BomLines { get; set; } = [];
}

public class BomLine
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int PartId { get; set; }
    public Part? Part { get; set; }
    public decimal QuantityPerProduct { get; set; }
}

public class StockMovement
{
    public int Id { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public string Reference { get; set; } = "";
    public string Type { get; set; } = "";
    public int PartId { get; set; }
    public Part? Part { get; set; }
    public decimal QuantityIn { get; set; }
    public decimal QuantityOut { get; set; }
    public decimal UnitCost { get; set; }
    public string Notes { get; set; } = "";
}

public enum OrderStatus
{
    Accepted,
    InProduction,
    Completed,
    Cancelled
}

public enum ReservationStatus
{
    Active,
    Consumed,
    Released
}

public class CustomerOrder
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = "";
    public string CustomerName { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public OrderStatus Status { get; set; } = OrderStatus.Accepted;
    public List<OrderLine> Lines { get; set; } = [];
}

public class OrderLine
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public CustomerOrder? Order { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public decimal Quantity { get; set; }
}

public class ReservationLine
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public CustomerOrder? Reservation { get; set; }
    public int PartId { get; set; }
    public Part? Part { get; set; }
    public decimal Quantity { get; set; }
    public ReservationStatus Status { get; set; }
        = ReservationStatus.Active;
}

// PRODUCTION

public enum ProductionStatus
{
    InProgress,
    Completed,
    Cancelled
}

public class ProductionRun
{
    public int Id { get; set; }

    public string ProductionNumber { get; set; } = "";

    public int OrderId { get; set; }
    public CustomerOrder? Order { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public decimal PlannedQuantity { get; set; }
    public decimal CompletedQuantity { get; set; }

    public ProductionStatus Status { get; set; }
        = ProductionStatus.InProgress;

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    public List<ProductionConsumption> Consumptions { get; set; } = [];
}

public class ProductionConsumption
{
    public int Id { get; set; }

    public int ProductionRunId { get; set; }
    public ProductionRun? ProductionRun { get; set; }

    // Original component required by the product BOM.
    public int PartId { get; set; }
    public Part? Part { get; set; }

    // Component actually used during production; null means the original PartId for legacy rows.
    public int? ActualPartId { get; set; }
    public Part? ActualPart { get; set; }

    public decimal PlannedQuantity { get; set; }
    public decimal ActualQuantity { get; set; }
}
public class FinishedGoodsMovement
{
    public int Id { get; set; }

    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    public string Reference { get; set; } = "";

    // Production, Sale, Adjustment, Return
    public string Type { get; set; } = "";

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public decimal QuantityIn { get; set; }
    public decimal QuantityOut { get; set; }

    public string Notes { get; set; } = "";
}