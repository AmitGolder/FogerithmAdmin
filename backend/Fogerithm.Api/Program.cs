using Fogerithm.Api.Data;
using Fogerithm.Api.Models;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("Fogerithm")
    ?? throw new InvalidOperationException("Missing ConnectionStrings:Fogerithm configuration.");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddCors(options => options.AddPolicy("local-ui", policy =>
    policy.WithOrigins("http://localhost:4200").AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
app.UseCors("local-ui");
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/api/health", () => Results.Ok(new { status = "ok", app = "Fogerithm Admin API" }));

app.MapGet("/api/parts", async (AppDbContext db) =>
    Results.Ok(await db.Parts.OrderBy(p => p.Code).ToListAsync()));

app.MapPost("/api/parts", async (Part input, AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(input.Code) || string.IsNullOrWhiteSpace(input.Name))
        return Results.BadRequest(new { message = "Part code and name are required." });
    var code = input.Code.Trim().ToUpperInvariant();
    if (await db.Parts.AnyAsync(p => p.Code == code))
        return Results.Conflict(new { message = $"Part code {code} already exists." });
    input.Code = code;
    db.Parts.Add(input);
    await db.SaveChangesAsync();
    return Results.Created($"/api/parts/{input.Id}", input);
});

app.MapGet("/api/products", async (AppDbContext db) =>
    Results.Ok(await db.Products.Include(p => p.BomLines)
        .ThenInclude(line => line.Part).OrderBy(p => p.Name).ToListAsync()));

app.MapPost("/api/products", async (Product input, AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(input.Code) || string.IsNullOrWhiteSpace(input.Name))
        return Results.BadRequest(new { message = "Product code and name are required." });
    if (await db.Products.AnyAsync(p => p.Code == input.Code.Trim().ToUpperInvariant()))
        return Results.Conflict(new { message = "Product code already exists." });
    input.Code = input.Code.Trim().ToUpperInvariant();
    db.Products.Add(input);
    await db.SaveChangesAsync();
    return Results.Created($"/api/products/{input.Id}", input);
});

app.MapPost("/api/products/{productId:int}/bom-lines", async (int productId, CreateBomLineRequest request, AppDbContext db) =>
{
    if (request.PartId <= 0 || request.QuantityPerProduct <= 0)
        return Results.BadRequest(new { message = "Choose a valid part and quantity greater than zero." });
    var product = await db.Products.FirstOrDefaultAsync(p => p.Id == productId);
    if (product is null) return Results.NotFound(new { message = "Product not found." });
    var part = await db.Parts.FirstOrDefaultAsync(p => p.Id == request.PartId);
    if (part is null) return Results.NotFound(new { message = "Part not found." });
    var existing = await db.BomLines.FirstOrDefaultAsync(x => x.ProductId == productId && x.PartId == request.PartId);
    if (existing is not null)
        return Results.Conflict(new { message = "This part is already in the BOM. Remove the existing line first, or use a different component." });
    var line = new BomLine { ProductId = productId, PartId = request.PartId, QuantityPerProduct = request.QuantityPerProduct };
    db.BomLines.Add(line);
    await db.SaveChangesAsync();
    return Results.Created($"/api/bom-lines/{line.Id}", new { line.Id, line.ProductId, line.PartId, line.QuantityPerProduct, Part = new { part.Id, part.Code, part.Name, part.Unit } });
});

app.MapDelete("/api/bom-lines/{lineId:int}", async (int lineId, AppDbContext db) =>
{
    var line = await db.BomLines.FirstOrDefaultAsync(x => x.Id == lineId);
    if (line is null) return Results.NotFound(new { message = "BOM line not found." });
    var hasActiveReservations = await db.ReservationLines.AnyAsync(r => r.PartId == line.PartId && r.Status == ReservationStatus.Active);
    if (hasActiveReservations)
        return Results.Conflict(new { message = "This component has active order reservations. Cancel or complete the affected orders before changing this BOM." });
    db.BomLines.Remove(line);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

app.MapGet("/api/inventory", async (AppDbContext db) =>
{
    var parts = await db.Parts.OrderBy(p => p.Code).ToListAsync();
    var movements = await db.StockMovements.ToListAsync();
    var reservations = await db.ReservationLines
        .Where(x => x.Status == ReservationStatus.Active)
        .ToListAsync();

    return Results.Ok(parts.Select(p =>
    {
        var physical = p.OpeningStock
            + movements.Where(m => m.PartId == p.Id).Sum(m => m.QuantityIn - m.QuantityOut);
        var reserved = reservations.Where(r => r.PartId == p.Id).Sum(r => r.Quantity);
        return new
        {
            p.Id, p.Code, p.Name, p.Category, p.Unit, p.ReorderLevel,
            PhysicalStock = physical,
            ReservedStock = reserved,
            AvailableStock = physical - reserved,
            StockStatus = physical - reserved <= 0 ? "OUT OF STOCK"
                : physical - reserved <= p.ReorderLevel ? "LOW STOCK" : "OK"
        };
    }));
});

app.MapGet("/api/orders", async (AppDbContext db) =>
    Results.Ok(await db.Orders.Include(o => o.Lines).ThenInclude(l => l.Product)
        .OrderByDescending(o => o.CreatedAt).ToListAsync()));

app.MapPost("/api/orders", async (CreateOrderRequest request, AppDbContext db) =>
{
    if (request.Lines.Count == 0)
        return Results.BadRequest(new { message = "Add at least one product to the order." });
    if (request.Lines.Any(x => x.Quantity <= 0))
        return Results.BadRequest(new { message = "Order quantities must be greater than zero." });

    await using var tx = await db.Database.BeginTransactionAsync();
    var order = new CustomerOrder
    {
        OrderNumber = string.IsNullOrWhiteSpace(request.OrderNumber)
            ? $"ORD-{DateTime.Now:yyyyMMdd-HHmmss}" : request.OrderNumber.Trim(),
        CustomerName = request.CustomerName.Trim(),
        Status = OrderStatus.Accepted,
        CreatedAt = DateTime.UtcNow
    };

    foreach (var requested in request.Lines)
    {
        var product = await db.Products.Include(p => p.BomLines)
            .FirstOrDefaultAsync(p => p.Id == requested.ProductId);
        if (product is null)
        {
            await tx.RollbackAsync();
            return Results.BadRequest(new { message = $"Product {requested.ProductId} was not found." });
        }
        if (product.BomLines.Count == 0)
        {
            await tx.RollbackAsync();
            return Results.BadRequest(new { message = $"Product {product.Name} has no BOM lines." });
        }
        order.Lines.Add(new OrderLine { ProductId = product.Id, Quantity = requested.Quantity });
    }

    db.Orders.Add(order);
    await db.SaveChangesAsync();

    foreach (var line in order.Lines)
    {
        var product = await db.Products.Include(p => p.BomLines).FirstAsync(p => p.Id == line.ProductId);
        foreach (var bom in product.BomLines)
        {
            db.ReservationLines.Add(new ReservationLine
            {
                OrderId = order.Id, PartId = bom.PartId,
                Quantity = bom.QuantityPerProduct * line.Quantity,
                Status = ReservationStatus.Active
            });
        }
    }
    await db.SaveChangesAsync();

    var shortageList = await GetShortages(db);
    var shortages = shortageList.Where(x => x.AvailableStock < 0).ToList();
    if (shortages.Count > 0)
    {
        await tx.RollbackAsync();
        return Results.Conflict(new
        {
            message = "Order cannot be accepted because one or more components would be over-reserved.",
            shortages
        });
    }

    await tx.CommitAsync();
    return Results.Created($"/api/orders/{order.Id}", new
    {
        order.Id, order.OrderNumber, order.CustomerName, Status = order.Status.ToString(),
        message = "Order accepted; BOM components are reserved, not consumed."
    });
});

app.MapGet("/api/shortages", async (AppDbContext db) => Results.Ok(await GetShortages(db)));

app.Run();

static async Task<List<ShortageDto>> GetShortages(AppDbContext db)
{
    var parts = await db.Parts.ToListAsync();
    var movements = await db.StockMovements.ToListAsync();
    var reservations = await db.ReservationLines
        .Where(x => x.Status == ReservationStatus.Active).ToListAsync();
    return parts.Select(p =>
    {
        var physical = p.OpeningStock + movements.Where(m => m.PartId == p.Id)
            .Sum(m => m.QuantityIn - m.QuantityOut);
        var reserved = reservations.Where(r => r.PartId == p.Id).Sum(r => r.Quantity);
        return new ShortageDto(p.Code, p.Name, physical, reserved, physical - reserved, p.ReorderLevel);
    }).Where(x => x.AvailableStock <= x.ReorderLevel).ToList();
}

public record CreateOrderRequest(string CustomerName, string? OrderNumber, List<CreateOrderLine> Lines);
public record CreateOrderLine(int ProductId, decimal Quantity);
public record ShortageDto(string Code, string Name, decimal PhysicalStock, decimal ReservedStock, decimal AvailableStock, decimal ReorderLevel);

public record CreateBomLineRequest(int PartId, decimal QuantityPerProduct);
