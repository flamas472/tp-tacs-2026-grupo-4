using System.Text.Json.Serialization;
using FiguritasApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// For in-memory persistence (Not sure if this will stay in the future. If so, we shouldn't clutter this file)
builder.Services.AddSingleton<FiguritaRepository>();
builder.Services.AddSingleton<UserRepository>();
builder.Services.AddSingleton<InventoryFiguritaRepository>();
builder.Services.AddSingleton<ExchangeProposalRepository>();
builder.Services.AddSingleton<AuctionRepository>();
builder.Services.AddSingleton<AuctionOfferRepository>();

// Services
builder.Services.AddSingleton<UserService>();

// Para que al hacer un POST se puedan pasar los ENUM por su valor en texto en vez de su valor numérico.
builder.Services.AddControllers()
    .AddJsonOptions(options => {
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter());
    });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // Para que abra directo la vista en Swagger
    app.Use(async (context , next) => {
    if (context.Request.Path == "/") {
        context.Response.Redirect("/swagger/index.html", permanent: false);
        return;
    }
    await next();
    });
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
