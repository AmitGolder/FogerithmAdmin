
using System.Text.Json.Serialization;
using Fogerithm.Api.Data;
using Fogerithm.Api.Models;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ======================================================
// CONFIGURATION
// ======================================================

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("Fogerithm")));

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.ReferenceHandler =
        ReferenceHandler.IgnoreCycles;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AngularFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseCors("AngularFrontend");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ======================================================
// PARTS
// ======================================================

app.MapGet("/api/parts", async (AppDbContext db) =>
{
    var parts = await db.Parts
        .AsNoTracking()
        .OrderBy(p => p.Code)
        .ToListAsync();

    return Results.Ok(parts);
});

app.MapPost("/api/parts", async (
    PartRequest request,
    AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(request.Code) ||
        string.IsNullOrWhiteSpace(request.Name))
    {
        return Results.BadRequest(new
        {
            message = "Part code and name are required."
        });
    }

    var code = request.Code.Trim();

    if (await db.Parts.AnyAsync(p => p.Code == code))
    {
        return Results.Conflict(new
        {
            message = "Part code already exists."
        });
    }

    if (request.UnitCost < 0 || request.ReorderLevel < 0)
    {
        return Results.BadRequest(new
        {
            message = "Cost and reorder level cannot be negative."
        });
    }

    var part = new Part
    {
        Code = code,
        Name = request.Name.Trim(),
        Category = request.Category ?? "",
        Specification = request.Specification ?? "",
        Unit = string.IsNullOrWhiteSpace(request.Unit)
            ? "pc"
            : request.Unit.Trim(),
        UnitCost = request.UnitCost,
        OpeningStock = 0,
        ReorderLevel = request.ReorderLevel,
        Supplier = request.Supplier ?? "",
        Remarks = request.Remarks ?? ""
    };

    db.Parts.Add(part);
    await db.SaveChangesAsync();

    return Results.Created($"/api/parts/{part.Id}", part);
});

app.MapPut("/api/parts/{id:int}", async (
    int id,
    UpdatePartRequest request,
    AppDbContext db) =>
{
    var part = await db.Parts.FindAsync(id);

    if (part is null)
        return Results.NotFound();

    if (string.IsNullOrWhiteSpace(request.Code) ||
        string.IsNullOrWhiteSpace(request.Name))
    {
        return Results.BadRequest(new
        {
            message = "Part code and name are required."
        });
    }

    if (request.UnitCost < 0 || request.ReorderLevel < 0)
    {
        return Results.BadRequest(new
        {
            message = "Cost and reorder level cannot be negative."
        });
    }

    var code = request.Code.Trim();

    if (await db.Parts.AnyAsync(p => p.Code == code && p.Id != id))
    {
        return Results.Conflict(new
        {
            message = "Part code already exists."
        });
    }

    part.Code = code;
    part.Name = request.Name.Trim();
    part.Category = request.Category ?? "";
    part.Specification = request.Specification ?? "";
    part.Unit = string.IsNullOrWhiteSpace(request.Unit)
        ? "pc"
        : request.Unit.Trim();
    part.UnitCost = request.UnitCost;
    part.ReorderLevel = request.ReorderLevel;
    part.Supplier = request.Supplier ?? "";
    part.Remarks = request.Remarks ?? "";

    await db.SaveChangesAsync();

    return Results.Ok(part);
});

app.MapDelete("/api/parts/{id:int}", async (
    int id,
    AppDbContext db) =>
{
    var part = await db.Parts.FindAsync(id);

    if (part is null)
        return Results.NotFound();

    var inUse =
        await db.StockMovements.AnyAsync(m => m.PartId == id) ||
        await db.BomLines.AnyAsync(b => b.PartId == id) ||
        await db.ReservationLines.AnyAsync(r => r.PartId == id) ||
        await db.ProductionConsumptions.AnyAsync(c => c.PartId == id);

    if (inUse)
    {
        return Results.Conflict(new
        {
            message = "This part is in use and cannot be deleted."
        });
    }

    db.Parts.Remove(part);
    await db.SaveChangesAsync();

    return Results.NoContent();
});

// ======================================================
// PRODUCTS
// ======================================================

app.MapGet("/api/products", async (AppDbContext db) =>
{
    var products = await db.Products
        .AsNoTracking()
        .OrderBy(p => p.Name)
        .Select(p => new
        {
            p.Id,
            p.Code,
            p.Name,
            p.Description,
            p.SellingPrice,
            BomLines = p.BomLines.Select(b => new
            {
                b.Id,
                b.ProductId,
                b.PartId,
                b.QuantityPerProduct,
                Part = b.Part == null
                    ? null
                    : new
                    {
                        b.Part.Id,
                        b.Part.Code,
                        b.Part.Name,
                        b.Part.Category,
                        b.Part.Unit,
                        b.Part.UnitCost
                    }
            }).ToList()
        })
        .ToListAsync();

    return Results.Ok(products);
});

app.MapPost("/api/products", async (
    ProductRequest request,
    AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(request.Code) ||
        string.IsNullOrWhiteSpace(request.Name))
    {
        return Results.BadRequest(new
        {
            message = "Product code and name are required."
        });
    }

    if (request.SellingPrice < 0)
    {
        return Results.BadRequest(new
        {
            message = "Selling price cannot be negative."
        });
    }

    var code = request.Code.Trim();

    if (await db.Products.AnyAsync(p => p.Code == code))
    {
        return Results.Conflict(new
        {
            message = "Product code already exists."
        });
    }

    var product = new Product
    {
        Code = code,
        Name = request.Name.Trim(),
        Description = request.Description ?? "",
        SellingPrice = request.SellingPrice
    };

    db.Products.Add(product);
    await db.SaveChangesAsync();

    return Results.Created($"/api/products/{product.Id}", new
    {
        product.Id,
        product.Code,
        product.Name,
        product.Description,
        product.SellingPrice
    });
});

app.MapPost("/api/products/{productId:int}/bom-lines", async (
    int productId,
    BomLineRequest request,
    AppDbContext db) =>
{
    if (request.QuantityPerProduct <= 0)
    {
        return Results.BadRequest(new
        {
            message = "Quantity must be greater than zero."
        });
    }

    var productExists =
        await db.Products.AnyAsync(p => p.Id == productId);

    if (!productExists)
        return Results.NotFound(new { message = "Product not found." });

    var partExists =
        await db.Parts.AnyAsync(p => p.Id == request.PartId);

    if (!partExists)
        return Results.NotFound(new { message = "Part not found." });

    var line = new BomLine
    {
        ProductId = productId,
        PartId = request.PartId,
        QuantityPerProduct = request.QuantityPerProduct
    };

    db.BomLines.Add(line);
    await db.SaveChangesAsync();

    return Results.Created($"/api/bom-lines/{line.Id}", new
    {
        line.Id,
        line.ProductId,
        line.PartId,
        line.QuantityPerProduct
    });
});

app.MapDelete("/api/bom-lines/{lineId:int}", async (
    int lineId,
    AppDbContext db) =>
{
    var line = await db.BomLines.FindAsync(lineId);

    if (line is null)
        return Results.NotFound();

    var usedByProduction = await db.ProductionRuns
        .AnyAsync(r =>
            r.ProductId == line.ProductId &&
            r.Status == ProductionStatus.InProgress);

    if (usedByProduction)
    {
        return Results.Conflict(new
        {
            message = "Cannot change this BOM while production is in progress."
        });
    }

    db.BomLines.Remove(line);
    await db.SaveChangesAsync();

    return Results.NoContent();
});

// ======================================================
// RAW MATERIAL INVENTORY
// ======================================================

app.MapGet("/api/inventory", async (AppDbContext db) =>
{
    var parts = await db.Parts
        .AsNoTracking()
        .OrderBy(p => p.Code)
        .ToListAsync();

    var movements = await db.StockMovements
        .AsNoTracking()
        .ToListAsync();

    var reservations = await db.ReservationLines
        .AsNoTracking()
        .Where(r => r.Status == ReservationStatus.Active)
        .ToListAsync();

    var inventory = parts.Select(part =>
    {
        var physical = part.OpeningStock +
            movements
                .Where(m => m.PartId == part.Id)
                .Sum(m => m.QuantityIn - m.QuantityOut);

        var reserved = reservations
            .Where(r => r.PartId == part.Id)
            .Sum(r => r.Quantity);

        return new
        {
            part.Id,
            part.Code,
            part.Name,
            part.Category,
            part.Unit,
            part.UnitCost,
            PhysicalStock = physical,
            ReservedStock = reserved,
            AvailableStock = physical - reserved,
            StockStatus = physical - reserved <= 0
                ? "OUT OF STOCK"
                : physical - reserved <= part.ReorderLevel
                    ? "LOW STOCK"
                    : "OK",
            part.ReorderLevel
        };
    }).ToList();

    return Results.Ok(inventory);
});

app.MapPost("/api/stock/receive", async (
    ReceiveStockRequest request,
    AppDbContext db) =>
{
    if (request.Quantity <= 0 || request.UnitCost < 0)
    {
        return Results.BadRequest(new
        {
            message = "Quantity must be positive and unit cost cannot be negative."
        });
    }

    var part = await db.Parts.FindAsync(request.PartId);

    if (part is null)
        return Results.NotFound(new { message = "Part not found." });

    var movement = new StockMovement
    {
        PartId = part.Id,
        QuantityIn = request.Quantity,
        QuantityOut = 0,
        UnitCost = request.UnitCost,
        Reference = request.Reference ?? "",
        Notes = string.IsNullOrWhiteSpace(request.Notes)
            ? request.Supplier ?? ""
            : request.Notes,
        Type = "Receive",
        OccurredAt = DateTime.UtcNow
    };

    db.StockMovements.Add(movement);
    await db.SaveChangesAsync();

    return Results.Ok(new
    {
        message = "Stock received successfully."
    });
});

app.MapPost("/api/stock/adjust", async (
    AdjustStockRequest request,
    AppDbContext db) =>
{
    var part = await db.Parts.FindAsync(request.PartId);

    if (part is null)
        return Results.NotFound(new { message = "Part not found." });

    if (request.QuantityChange == 0)
    {
        return Results.BadRequest(new
        {
            message = "Adjustment quantity cannot be zero."
        });
    }

    var movements = await db.StockMovements
        .Where(m => m.PartId == part.Id)
        .ToListAsync();

    var currentPhysical = part.OpeningStock +
        movements.Sum(m => m.QuantityIn - m.QuantityOut);

    if (currentPhysical + request.QuantityChange < 0)
    {
        return Results.BadRequest(new
        {
            message = "Physical stock cannot become negative."
        });
    }

    if (request.QuantityChange < 0)
    {
        var activeReserved = await db.ReservationLines
            .Where(r => r.PartId == part.Id &&
                        r.Status == ReservationStatus.Active)
            .SumAsync(r => (decimal?)r.Quantity) ?? 0;

        if (currentPhysical + request.QuantityChange < activeReserved)
        {
            return Results.Conflict(new
            {
                message = "Adjustment would reduce stock below the reserved quantity."
            });
        }
    }

    db.StockMovements.Add(new StockMovement
    {
        PartId = part.Id,
        QuantityIn = request.QuantityChange > 0
            ? request.QuantityChange
            : 0,
        QuantityOut = request.QuantityChange < 0
            ? -request.QuantityChange
            : 0,
        UnitCost = part.UnitCost,
        Reference = "ADJUSTMENT",
        Type = "Adjustment",
        Notes = request.Reason,
        OccurredAt = DateTime.UtcNow
    });

    await db.SaveChangesAsync();

    return Results.Ok(new
    {
        message = "Stock adjusted successfully."
    });
});

app.MapGet("/api/stock/movements", async (
    int? partId,
    AppDbContext db) =>
{
    var query = db.StockMovements
        .AsNoTracking()
        .AsQueryable();

    if (partId.HasValue)
        query = query.Where(m => m.PartId == partId.Value);

    var movements = await query
        .OrderByDescending(m => m.OccurredAt)
        .Select(m => new
        {
            m.Id,
            m.PartId,
            PartName = m.Part == null ? "" : m.Part.Name,
            m.OccurredAt,
            m.Reference,
            m.Type,
            m.QuantityIn,
            m.QuantityOut,
            m.UnitCost,
            m.Notes
        })
        .ToListAsync();

    return Results.Ok(movements);
});

// ======================================================
// CUSTOMER ORDERS
// ======================================================

app.MapGet("/api/orders", async (AppDbContext db) =>
{
    var orders = await db.Orders
        .AsNoTracking()
        .OrderByDescending(o => o.CreatedAt)
        .Select(o => new
        {
            o.Id,
            o.OrderNumber,
            o.CustomerName,
            o.CreatedAt,
            Status = o.Status.ToString(),
            Lines = o.Lines.Select(line => new
            {
                line.Id,
                line.OrderId,
                line.ProductId,
                ProductCode = line.Product == null ? "" : line.Product.Code,
                ProductName = line.Product == null ? "" : line.Product.Name,
                line.Quantity
            }).ToList()
        })
        .ToListAsync();

    return Results.Ok(orders);
});

app.MapPost("/api/orders", async (
    OrderRequest request,
    AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(request.CustomerName))
    {
        return Results.BadRequest(new
        {
            message = "Customer name is required."
        });
    }

    if (request.Lines is null || request.Lines.Count == 0 ||
        request.Lines.Any(x => x.ProductId <= 0 || x.Quantity <= 0))
    {
        return Results.BadRequest(new
        {
            message = "Add valid order lines with positive quantities."
        });
    }

    var requestedLines = request.Lines
        .GroupBy(x => x.ProductId)
        .Select(g => new
        {
            ProductId = g.Key,
            Quantity = g.Sum(x => x.Quantity)
        })
        .ToList();

    var productIds = requestedLines.Select(x => x.ProductId).ToList();

    var products = await db.Products
        .Include(p => p.BomLines)
        .Where(p => productIds.Contains(p.Id))
        .ToListAsync();

    if (products.Count != productIds.Count)
    {
        return Results.BadRequest(new
        {
            message = "One or more products were not found."
        });
    }

    var requiredByPart = new Dictionary<int, decimal>();

    foreach (var requested in requestedLines)
    {
        var product = products.First(p => p.Id == requested.ProductId);

        foreach (var bom in product.BomLines)
        {
            requiredByPart[bom.PartId] =
                requiredByPart.GetValueOrDefault(bom.PartId) +
                bom.QuantityPerProduct * requested.Quantity;
        }
    }

    var partIds = requiredByPart.Keys.ToList();

    var parts = await db.Parts
        .Where(p => partIds.Contains(p.Id))
        .ToListAsync();

    var movements = await db.StockMovements
        .Where(m => partIds.Contains(m.PartId))
        .ToListAsync();

    var reservations = await db.ReservationLines
        .Where(r => partIds.Contains(r.PartId) &&
                    r.Status == ReservationStatus.Active)
        .ToListAsync();

    var shortages = new List<string>();

    foreach (var requirement in requiredByPart)
    {
        var part = parts.FirstOrDefault(p => p.Id == requirement.Key);

        if (part is null)
        {
            shortages.Add($"Part ID {requirement.Key} was not found.");
            continue;
        }

        var physical = part.OpeningStock +
            movements
                .Where(m => m.PartId == part.Id)
                .Sum(m => m.QuantityIn - m.QuantityOut);

        var reserved = reservations
            .Where(r => r.PartId == part.Id)
            .Sum(r => r.Quantity);

        var available = physical - reserved;

        if (requirement.Value > available)
        {
            shortages.Add(
                $"{part.Code}: required {requirement.Value}, available {available}");
        }
    }

    if (shortages.Count > 0)
    {
        return Results.Conflict(new
        {
            message = "Insufficient available stock. Order was not created.",
            shortages
        });
    }

    await using var transaction = await db.Database.BeginTransactionAsync();

    try
    {
        var order = new CustomerOrder
        {
            OrderNumber =
                $"ORD-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
            CustomerName = request.CustomerName.Trim(),
            CreatedAt = DateTime.UtcNow,
            Status = OrderStatus.Accepted,
            Lines = requestedLines.Select(x => new OrderLine
            {
                ProductId = x.ProductId,
                Quantity = x.Quantity
            }).ToList()
        };

        db.Orders.Add(order);
        await db.SaveChangesAsync();

        foreach (var requirement in requiredByPart)
        {
            db.ReservationLines.Add(new ReservationLine
            {
                OrderId = order.Id,
                PartId = requirement.Key,
                Quantity = requirement.Value,
                Status = ReservationStatus.Active
            });
        }

        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        return Results.Created($"/api/orders/{order.Id}", new
        {
            order.Id,
            order.OrderNumber,
            order.CustomerName,
            order.CreatedAt,
            Status = order.Status.ToString()
        });
    }
    catch
    {
        await transaction.RollbackAsync();
        throw;
    }
});

// ======================================================
// PRODUCTION - LIST
// ======================================================

app.MapGet("/api/production", async (AppDbContext db) =>
{
    var runs = await db.ProductionRuns
        .AsNoTracking()
        .OrderByDescending(x => x.StartedAt)
        .Select(x => new
        {
            x.Id,
            x.ProductionNumber,
            x.OrderId,
            OrderNumber = x.Order == null ? "" : x.Order.OrderNumber,
            x.ProductId,
            ProductName = x.Product == null ? "" : x.Product.Name,
            x.PlannedQuantity,
            x.CompletedQuantity,
            Status = x.Status.ToString(),
            x.StartedAt,
            x.CompletedAt
        })
        .ToListAsync();

    return Results.Ok(runs);
});

// ======================================================
// PRODUCTION - DETAILS
// ======================================================

app.MapGet("/api/production/{id:int}", async (
    int id,
    AppDbContext db) =>
{
    var run = await db.ProductionRuns
        .AsNoTracking()
        .Include(x => x.Order)
        .Include(x => x.Product)
        .Include(x => x.Consumptions)
            .ThenInclude(x => x.Part)
        .Include(x => x.Consumptions)
            .ThenInclude(x => x.ActualPart)
        .FirstOrDefaultAsync(x => x.Id == id);

    if (run is null)
        return Results.NotFound(new { message = "Production run not found." });

    return Results.Ok(new
    {
        run.Id,
        run.ProductionNumber,
        run.OrderId,
        OrderNumber = run.Order?.OrderNumber,
        run.ProductId,
        ProductName = run.Product?.Name,
        run.PlannedQuantity,
        run.CompletedQuantity,
        Status = run.Status.ToString(),
        run.StartedAt,
        run.CompletedAt,
        Consumptions = run.Consumptions.Select(c => new
        {
            c.Id,
            c.PartId,
            PartName = c.Part?.Name,
            c.ActualPartId,
            ActualPartName = c.ActualPart == null ? null : c.ActualPart.Name,
            c.PlannedQuantity,
            c.ActualQuantity
        })
    });
});

// ======================================================
// PRODUCTION - START
// ======================================================

app.MapPost("/api/production", async (
    StartProductionRequest request,
    AppDbContext db) =>
{
    if (request.PlannedQuantity <= 0)
    {
        return Results.BadRequest(new
        {
            message = "Planned quantity must be greater than zero."
        });
    }

    var order = await db.Orders
        .Include(o => o.Lines)
        .FirstOrDefaultAsync(o => o.Id == request.OrderId);

    if (order is null)
        return Results.NotFound(new { message = "Order not found." });

    if (order.Status != OrderStatus.Accepted &&
        order.Status != OrderStatus.InProduction)
    {
        return Results.BadRequest(new
        {
            message = "This order cannot enter production."
        });
    }

    var product = await db.Products
        .Include(p => p.BomLines)
        .FirstOrDefaultAsync(p => p.Id == request.ProductId);

    if (product is null)
        return Results.NotFound(new { message = "Product not found." });

    var orderQuantity = order.Lines
        .Where(x => x.ProductId == request.ProductId)
        .Sum(x => x.Quantity);

    if (orderQuantity <= 0)
    {
        return Results.BadRequest(new
        {
            message = "The product is not included in this order."
        });
    }

    if (product.BomLines.Count == 0)
    {
        return Results.BadRequest(new
        {
            message = "Add BOM lines before starting production."
        });
    }

    var existingRuns = await db.ProductionRuns
        .Where(x => x.OrderId == request.OrderId &&
                    x.ProductId == request.ProductId &&
                    x.Status != ProductionStatus.Cancelled)
        .ToListAsync();

    var allocatedQuantity = existingRuns.Sum(x =>
        x.Status == ProductionStatus.Completed
            ? x.CompletedQuantity
            : x.PlannedQuantity);

    if (allocatedQuantity + request.PlannedQuantity > orderQuantity)
    {
        return Results.Conflict(new
        {
            message = "Production quantity exceeds the remaining order quantity.",
            OrderQuantity = orderQuantity,
            AlreadyAllocated = allocatedQuantity,
            Remaining = orderQuantity - allocatedQuantity
        });
    }

    var plannedByPart = product.BomLines
        .GroupBy(x => x.PartId)
        .ToDictionary(
            g => g.Key,
            g => g.Sum(x => x.QuantityPerProduct) * request.PlannedQuantity);

    var partIds = plannedByPart.Keys.ToList();

    var reservations = await db.ReservationLines
        .Where(x => x.OrderId == order.Id &&
                    x.Status == ReservationStatus.Active &&
                    partIds.Contains(x.PartId))
        .ToListAsync();

    var activeRuns = await db.ProductionRuns
        .Where(x => x.OrderId == order.Id &&
                    x.ProductId == product.Id &&
                    x.Status == ProductionStatus.InProgress)
        .Include(x => x.Consumptions)
        .ToListAsync();

    var parts = await db.Parts
        .Where(x => partIds.Contains(x.Id))
        .ToDictionaryAsync(x => x.Id);

    foreach (var requirement in plannedByPart)
    {
        var partId = requirement.Key;

        var reserved = reservations
            .Where(x => x.PartId == partId)
            .Sum(x => x.Quantity);

        var alreadyAllocated = activeRuns
            .SelectMany(x => x.Consumptions)
            .Where(x => x.PartId == partId)
            .Sum(x => x.PlannedQuantity);

        if (!parts.ContainsKey(partId) ||
            requirement.Value + alreadyAllocated > reserved)
        {
            var partName = parts.TryGetValue(partId, out var p)
                ? p.Name
                : $"Part ID {partId}";

            return Results.Conflict(new
            {
                message = $"Insufficient unallocated reservation for {partName}.",
                Required = requirement.Value,
                Reserved = reserved,
                AlreadyAllocated = alreadyAllocated
            });
        }
    }

    var run = new ProductionRun
    {
        ProductionNumber =
            $"PR-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
        OrderId = order.Id,
        ProductId = product.Id,
        PlannedQuantity = request.PlannedQuantity,
        CompletedQuantity = 0,
        Status = ProductionStatus.InProgress,
        StartedAt = DateTime.UtcNow,
        Consumptions = plannedByPart.Select(x => new ProductionConsumption
        {
            PartId = x.Key,
            PlannedQuantity = x.Value,
            ActualQuantity = 0
        }).ToList()
    };

    await using var transaction = await db.Database.BeginTransactionAsync();

    try
    {
        db.ProductionRuns.Add(run);
        order.Status = OrderStatus.InProduction;

        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        return Results.Created($"/api/production/{run.Id}", new
        {
            run.Id,
            run.ProductionNumber,
            run.OrderId,
            run.ProductId,
            run.PlannedQuantity,
            Status = run.Status.ToString(),
            Consumptions = run.Consumptions.Select(x => new
            {
                x.PartId,
                x.PlannedQuantity
            })
        });
    }
    catch
    {
        await transaction.RollbackAsync();
        throw;
    }
});

// ======================================================
// PRODUCTION - COMPLETE
// ======================================================

app.MapPost("/api/production/{id:int}/complete", async (
    int id,
    CompleteProductionRequest request,
    AppDbContext db) =>
{
    if (request.CompletedQuantity <= 0)
        return Results.BadRequest(new { message = "Completed quantity must be greater than zero." });

    var run = await db.ProductionRuns
        .Include(x => x.Consumptions)
        .Include(x => x.Order).ThenInclude(x => x!.Lines)
        .Include(x => x.Product)
        .FirstOrDefaultAsync(x => x.Id == id);

    if (run is null) return Results.NotFound(new { message = "Production run not found." });
    if (run.Status != ProductionStatus.InProgress)
        return Results.Conflict(new { message = "Only an in-progress production run can be completed." });
    if (request.CompletedQuantity > run.PlannedQuantity)
        return Results.BadRequest(new { message = "Completed quantity cannot exceed the planned quantity." });
    if (request.Consumptions is null)
        return Results.BadRequest(new { message = "Actual material consumption is required." });

    // PartId identifies the original planned BOM component. ActualPartId is the component actually fitted.
    var plannedByPart = run.Consumptions.GroupBy(x => x.PartId)
        .ToDictionary(g => g.Key, g => g.Sum(x => x.PlannedQuantity));
    var requestsByOriginal = new Dictionary<int, ActualConsumptionRequest>();
    foreach (var item in request.Consumptions)
    {
        if (item.PartId <= 0 || item.ActualQuantity < 0 || (item.ActualPartId.HasValue && item.ActualPartId.Value <= 0))
            return Results.BadRequest(new { message = "Select valid parts and enter non-negative actual quantities." });
        if (!requestsByOriginal.TryAdd(item.PartId, item))
            return Results.BadRequest(new { message = $"Duplicate consumption entry for planned part ID {item.PartId}." });
    }
    if (plannedByPart.Keys.Any(x => !requestsByOriginal.ContainsKey(x)) || requestsByOriginal.Keys.Any(x => !plannedByPart.ContainsKey(x)))
        return Results.BadRequest(new { message = "Provide exactly one actual consumption entry for every planned BOM component." });
    foreach (var req in requestsByOriginal)
        if (req.Value.ActualQuantity > plannedByPart[req.Key])
            return Results.BadRequest(new { message = $"Actual quantity exceeds planned allocation for part ID {req.Key}.", Planned = plannedByPart[req.Key], Actual = req.Value.ActualQuantity });

    var actualByPart = requestsByOriginal.Values
        .Where(x => x.ActualQuantity > 0)
        .GroupBy(x => x.ActualPartId.GetValueOrDefault(x.PartId))
        .ToDictionary(g => g.Key, g => g.Sum(x => x.ActualQuantity));
    var allPartIds = plannedByPart.Keys.Concat(actualByPart.Keys).Distinct().ToList();
    var parts = await db.Parts.Where(p => allPartIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id);
    foreach (var actualId in actualByPart.Keys)
        if (!parts.ContainsKey(actualId)) return Results.BadRequest(new { message = $"Actual part ID {actualId} was not found." });

    await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
    try
    {
        var movements = await db.StockMovements.Where(m => allPartIds.Contains(m.PartId)).ToListAsync();
        var activeReservations = await db.ReservationLines
            .Where(r => r.Status == ReservationStatus.Active && allPartIds.Contains(r.PartId))
            .OrderBy(r => r.Id).ToListAsync();

        // The current run's planned allocation is released first; calculate stock available after that release.
        var releaseByPart = plannedByPart;
        foreach (var partId in allPartIds)
        {
            var part = parts[partId];
            var physical = part.OpeningStock + movements.Where(m => m.PartId == partId).Sum(m => m.QuantityIn - m.QuantityOut);
            var reservedNow = activeReservations.Where(r => r.PartId == partId).Sum(r => r.Quantity);
            var releasedForThisRun = releaseByPart.GetValueOrDefault(partId);
            var availableAfterRelease = physical - Math.Max(0, reservedNow - releasedForThisRun);
            var actualRequired = actualByPart.GetValueOrDefault(partId);
            if (actualRequired > availableAfterRelease)
                return Results.Conflict(new { message = $"Insufficient available stock for actual component {part.Code} ({part.Name}).", PhysicalStock = physical, CurrentlyReserved = reservedNow, AvailableAfterReleasingThisRun = availableAfterRelease, ActualConsumption = actualRequired });
        }

        // Release each original reservation allocation in full, preserving any reservation belonging to other runs/orders.
        foreach (var requirement in plannedByPart)
        {
            var remaining = requirement.Value;
            foreach (var reservation in activeReservations.Where(r => r.PartId == requirement.Key))
            {
                if (remaining <= 0) break;
                var take = Math.Min(reservation.Quantity, remaining);
                reservation.Quantity -= take;
                remaining -= take;
                if (reservation.Quantity == 0) reservation.Status = ReservationStatus.Released;
            }
            if (remaining > 0) throw new InvalidOperationException($"Active reservation allocation missing for planned part ID {requirement.Key}.");
        }

        // Persist actual component against each original BOM line; inventory is consumed against the actual component.
        foreach (var consumption in run.Consumptions)
        {
            var item = requestsByOriginal[consumption.PartId];
            var actualPartId = item.ActualPartId.GetValueOrDefault(item.PartId);
            consumption.ActualPartId = actualPartId;
            consumption.ActualQuantity = item.ActualQuantity;
            var planned = consumption.PlannedQuantity;
            var actual = item.ActualQuantity;
            var unused = planned - actual;
            if (unused > 0)
                db.ReservationLines.Add(new ReservationLine { OrderId = run.OrderId, PartId = consumption.PartId, Quantity = unused, Status = ReservationStatus.Released });
        }
        foreach (var actual in actualByPart)
        {
            if (actual.Value <= 0) continue;
            db.ReservationLines.Add(new ReservationLine { OrderId = run.OrderId, PartId = actual.Key, Quantity = actual.Value, Status = ReservationStatus.Consumed });
            db.StockMovements.Add(new StockMovement
            {
                PartId = actual.Key, QuantityIn = 0, QuantityOut = actual.Value, UnitCost = parts[actual.Key].UnitCost,
                Reference = run.ProductionNumber, Type = "Production Consumption",
                Notes = $"Actual material consumption for {run.ProductionNumber}", OccurredAt = DateTime.UtcNow
            });
        }

        run.CompletedQuantity = request.CompletedQuantity;
        run.Status = ProductionStatus.Completed;
        run.CompletedAt = DateTime.UtcNow;
        db.FinishedGoodsMovements.Add(new FinishedGoodsMovement { ProductId = run.ProductId, QuantityIn = request.CompletedQuantity, QuantityOut = 0, Reference = run.ProductionNumber, Type = "Production", Notes = $"Finished goods received from {run.ProductionNumber}", OccurredAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var order = run.Order!;
        var allRuns = await db.ProductionRuns.Where(x => x.OrderId == order.Id).ToListAsync();
        var orderFulfilled = order.Lines.All(line =>
        {
            var completed = allRuns.Where(x => x.ProductId == line.ProductId && x.Status == ProductionStatus.Completed).Sum(x => x.CompletedQuantity);
            var inProgress = allRuns.Any(x => x.ProductId == line.ProductId && x.Status == ProductionStatus.InProgress);
            return completed >= line.Quantity && !inProgress;
        });
        if (orderFulfilled)
        {
            order.Status = OrderStatus.Completed;
            var remainingReservations = await db.ReservationLines.Where(r => r.OrderId == order.Id && r.Status == ReservationStatus.Active).ToListAsync();
            foreach (var reservation in remainingReservations) reservation.Status = ReservationStatus.Released;
        }
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return Results.Ok(new { message = "Production completed successfully.", run.Id, run.ProductionNumber, run.CompletedQuantity, Status = run.Status.ToString(), OrderStatus = order.Status.ToString() });
    }
    catch
    {
        await transaction.RollbackAsync();
        throw;
    }
});

// ======================================================
// PRODUCTION - CANCEL
// ======================================================

app.MapPost("/api/production/{id:int}/cancel", async (
    int id,
    AppDbContext db) =>
{
    var run = await db.ProductionRuns
        .Include(x => x.Consumptions)
        .Include(x => x.Order)
        .FirstOrDefaultAsync(x => x.Id == id);

    if (run is null)
        return Results.NotFound(new { message = "Production run not found." });

    if (run.Status != ProductionStatus.InProgress)
    {
        return Results.Conflict(new
        {
            message = "Only an in-progress production run can be cancelled."
        });
    }

    await using var transaction = await db.Database.BeginTransactionAsync();

    try
    {
        var partIds = run.Consumptions
            .Select(x => x.PartId)
            .Distinct()
            .ToList();

        var reservations = await db.ReservationLines
            .Where(r => r.OrderId == run.OrderId &&
                        r.Status == ReservationStatus.Active &&
                        partIds.Contains(r.PartId))
            .OrderBy(r => r.Id)
            .ToListAsync();

        foreach (var consumption in run.Consumptions)
        {
            var remaining = consumption.PlannedQuantity;

            foreach (var reservation in reservations
                         .Where(r => r.PartId == consumption.PartId))
            {
                if (remaining <= 0)
                    break;

                var amount = Math.Min(reservation.Quantity, remaining);

                reservation.Quantity -= amount;
                remaining -= amount;

                if (reservation.Quantity == 0)
                    reservation.Status = ReservationStatus.Released;
            }

            if (remaining > 0)
            {
                throw new InvalidOperationException(
                    $"Active reservation allocation missing for part ID {consumption.PartId}.");
            }

            db.ReservationLines.Add(new ReservationLine
            {
                OrderId = run.OrderId,
                PartId = consumption.PartId,
                Quantity = consumption.PlannedQuantity,
                Status = ReservationStatus.Released
            });
        }

        run.Status = ProductionStatus.Cancelled;

        var order = run.Order!;

        var otherRuns = await db.ProductionRuns
            .Where(x => x.OrderId == order.Id && x.Id != run.Id)
            .ToListAsync();

        var anyCompleted = otherRuns.Any(x =>
            x.Status == ProductionStatus.Completed);

        var anyInProgress = otherRuns.Any(x =>
            x.Status == ProductionStatus.InProgress);

        if (!anyCompleted && !anyInProgress)
        {
            order.Status = OrderStatus.Accepted;
        }
        else if (!anyInProgress && order.Lines.All(line =>
        {
            var completed = otherRuns
                .Where(x => x.ProductId == line.ProductId &&
                            x.Status == ProductionStatus.Completed)
                .Sum(x => x.CompletedQuantity);

            return completed >= line.Quantity;
        }))
        {
            order.Status = OrderStatus.Completed;
        }

        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        return Results.Ok(new
        {
            message = "Production cancelled and its material allocation released.",
            run.Id,
            run.ProductionNumber,
            Status = run.Status.ToString(),
            OrderStatus = order.Status.ToString()
        });
    }
    catch
    {
        await transaction.RollbackAsync();
        throw;
    }
});

// ======================================================
// FINISHED GOODS STOCK
// ======================================================

app.MapGet("/api/finished-goods", async (AppDbContext db) =>
{
    var products = await db.Products
        .AsNoTracking()
        .OrderBy(p => p.Name)
        .Select(p => new
        {
            p.Id,
            p.Code,
            p.Name,
            p.SellingPrice,
            PhysicalStock = db.FinishedGoodsMovements
                .Where(m => m.ProductId == p.Id)
                .Sum(m => (decimal?)(m.QuantityIn - m.QuantityOut)) ?? 0m
        })
        .ToListAsync();

    return Results.Ok(products);
});

app.MapPost("/api/finished-goods/deliver", async (
    DeliverFinishedGoodsRequest request,
    AppDbContext db) =>
{
    if (request.ProductId <= 0 || request.Quantity <= 0)
    {
        return Results.BadRequest(new
        {
            message = "Select a product and enter a delivered quantity greater than zero."
        });
    }

    var product = await db.Products
        .AsNoTracking()
        .FirstOrDefaultAsync(p => p.Id == request.ProductId);

    if (product is null)
        return Results.NotFound(new { message = "Product not found." });

    await using var transaction = await db.Database.BeginTransactionAsync();

    try
    {
        // Re-read the ledger inside the transaction so we never intentionally
        // deliver more than the current physical finished-goods balance.
        var physicalStock = await db.FinishedGoodsMovements
            .Where(m => m.ProductId == request.ProductId)
            .SumAsync(m => (decimal?)(m.QuantityIn - m.QuantityOut)) ?? 0m;

        if (request.Quantity > physicalStock)
        {
            await transaction.RollbackAsync();
            return Results.BadRequest(new
            {
                message = $"Insufficient finished-goods stock. Available: {physicalStock:0.####}."
            });
        }

        var reference = string.IsNullOrWhiteSpace(request.Reference)
            ? $"DEL-{DateTime.UtcNow:yyyyMMddHHmmss}"
            : request.Reference.Trim();

        var movement = new FinishedGoodsMovement
        {
            ProductId = request.ProductId,
            OccurredAt = DateTime.UtcNow,
            Reference = reference,
            Type = "Delivery",
            QuantityIn = 0,
            QuantityOut = request.Quantity,
            Notes = string.IsNullOrWhiteSpace(request.Notes)
                ? "Finished goods handed over / delivered."
                : request.Notes.Trim()
        };

        db.FinishedGoodsMovements.Add(movement);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        return Results.Ok(new
        {
            message = "Delivery recorded and finished-goods stock reduced.",
            movement.Id,
            movement.ProductId,
            ProductName = product.Name,
            movement.OccurredAt,
            movement.Reference,
            movement.Type,
            movement.QuantityOut,
            movement.Notes,
            PhysicalStock = physicalStock - request.Quantity
        });
    }
    catch
    {
        await transaction.RollbackAsync();
        throw;
    }
});

app.MapGet("/api/finished-goods/movements", async (
    int? productId,
    AppDbContext db) =>
{
    var query = db.FinishedGoodsMovements
        .AsNoTracking()
        .AsQueryable();

    if (productId.HasValue)
        query = query.Where(m => m.ProductId == productId.Value);

    var movements = await query
        .OrderByDescending(m => m.OccurredAt)
        .Select(m => new
        {
            m.Id,
            m.ProductId,
            ProductName = m.Product == null ? "" : m.Product.Name,
            m.OccurredAt,
            m.Reference,
            m.Type,
            m.QuantityIn,
            m.QuantityOut,
            m.Notes
        })
        .ToListAsync();

    return Results.Ok(movements);
});

// ======================================================
// SHORTAGES
// ======================================================

app.MapGet("/api/shortages", async (AppDbContext db) =>
{
    var parts = await db.Parts
        .AsNoTracking()
        .ToListAsync();

    var movements = await db.StockMovements
        .AsNoTracking()
        .ToListAsync();

    var reservations = await db.ReservationLines
        .AsNoTracking()
        .Where(r => r.Status == ReservationStatus.Active)
        .ToListAsync();

    var shortages = parts.Select(part =>
    {
        var physical = part.OpeningStock +
            movements
                .Where(m => m.PartId == part.Id)
                .Sum(m => m.QuantityIn - m.QuantityOut);

        var reserved = reservations
            .Where(r => r.PartId == part.Id)
            .Sum(r => r.Quantity);

        var available = physical - reserved;

        return new
        {
            part.Id,
            part.Code,
            part.Name,
            PhysicalStock = physical,
            ReservedStock = reserved,
            AvailableStock = available,
            part.ReorderLevel,
            Shortage = Math.Max(0, part.ReorderLevel - available)
        };
    })
    .Where(x => x.AvailableStock <= x.ReorderLevel)
    .OrderBy(x => x.AvailableStock)
    .ToList();

    return Results.Ok(shortages);
});

app.Run();

// ======================================================
// REQUEST DTOs
// ======================================================

public record PartRequest(
    string Code,
    string Name,
    string? Category,
    string? Specification,
    string? Unit,
    decimal UnitCost,
    decimal ReorderLevel,
    string? Supplier,
    string? Remarks);

public record UpdatePartRequest(
    string Code,
    string Name,
    string? Category,
    string? Specification,
    string? Unit,
    decimal UnitCost,
    decimal ReorderLevel,
    string? Supplier,
    string? Remarks);

public record ProductRequest(
    string Code,
    string Name,
    decimal SellingPrice,
    string? Description);

public record BomLineRequest(
    int PartId,
    decimal QuantityPerProduct);

public record ReceiveStockRequest(
    int PartId,
    decimal Quantity,
    decimal UnitCost,
    string? Supplier,
    string? Reference,
    string? Notes);

public record AdjustStockRequest(
    int PartId,
    decimal QuantityChange,
    string Reason);

public record OrderLineRequest(
    int ProductId,
    decimal Quantity);

public record OrderRequest(
    string CustomerName,
    List<OrderLineRequest> Lines);

public record StartProductionRequest(
    int OrderId,
    int ProductId,
    decimal PlannedQuantity);

public record ActualConsumptionRequest(
    int PartId,
    decimal ActualQuantity,
    int? ActualPartId = null);

public record CompleteProductionRequest(
    decimal CompletedQuantity,
    List<ActualConsumptionRequest> Consumptions);

public record DeliverFinishedGoodsRequest(
    int ProductId,
    decimal Quantity,
    string? Reference,
    string? Notes);
