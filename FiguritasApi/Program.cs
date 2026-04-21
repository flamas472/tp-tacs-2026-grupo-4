using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Para persistir en memoria las entidades (No sé si esto queda a futuro. De ser así, habría que hacer una clase initializer para no ensuciar este archivo)
builder.Services.AddSingleton<FiguritaRepository>();
builder.Services.AddSingleton<UsuarioRepository>();
builder.Services.AddSingleton<FiguritaRepetidaRepository>();
builder.Services.AddSingleton<PropuestaIntercambioRepository>();

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
