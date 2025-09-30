using Microsoft.EntityFrameworkCore;
using SmartFleet.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                       ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapControllers();

app.MapGet("/health", async (ApplicationDbContext context, CancellationToken cancellationToken) =>
    {
        var canConnect = await context.Database.CanConnectAsync(cancellationToken);
        return canConnect
            ? Results.Ok(new { status = "Healthy" })
            : Results.StatusCode(503);
    })
    .WithOpenApi();

app.Run();
