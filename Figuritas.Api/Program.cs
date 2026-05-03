using System.Text;
using System.Text.Json.Serialization;
using Figuritas.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Todo lo necesario para implementar los JWT
builder.Services.AddAuthorization();
builder.Services.AddAuthentication(options =>
{
    // Definimos que el esquema por defecto es JWT Bearer
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = false, // Valida que el emisor sea el correcto
        ValidateAudience = false, // Valida que el receptor sea el correcto
        //ValidateLifetime = true, // Valida que el token no haya expirado
        ValidateIssuerSigningKey = true, // Valida la firma digital con nuestra Key
        
        //ValidIssuer = builder.Configuration["Jwt:Issuer"], // Nuestra API es la única que emite tokens válidos para la misma.
        //ValidAudience = builder.Configuration["Jwt:Audience"], // Nuestra App es la única autorizada para consumirlos.
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))// Lee la Key del appsettings
    };
});

// Add services to the container.
builder.Services.AddControllers();

// For in-memory persistence (Not sure if this will stay in the future. If so, we shouldn't clutter this file)
builder.Services.AddSingleton<NationalTeamRepository>();
builder.Services.AddSingleton<NationalTeamService>();
builder.Services.AddSingleton<TeamRepository>();
builder.Services.AddSingleton<TeamService>();
builder.Services.AddSingleton<CategoryRepository>();
builder.Services.AddSingleton<CategoryService>();
builder.Services.AddSingleton<StickerRepository>();
builder.Services.AddSingleton<UserRepository>();
builder.Services.AddSingleton<UserStickerRepository>();
builder.Services.AddSingleton<ExchangeProposalRepository>();
builder.Services.AddSingleton<AuctionRepository>();
builder.Services.AddSingleton<AuctionOfferRepository>();

// Services
builder.Services.AddSingleton<UserService>();
builder.Services.AddSingleton<AuthService>();
builder.Services.AddSingleton<StickerService>();
builder.Services.AddSingleton<ExchangeRepository>();
builder.Services.AddSingleton<ExchangeProposalService>();
builder.Services.AddSingleton<ExchangeService>();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Para que al hacer un POST se puedan pasar los ENUM por su valor en texto en vez de su valor numérico.
builder.Services.AddControllers()
    .AddJsonOptions(options => {
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter());
    });

builder.Services.AddCors(options =>
{
    options.AddPolicy("BlazorLocalPolicy", policy =>
    {
        policy.WithOrigins("http://localhost:5280") // permitir CORS solo para 5280 (puerto del cliente Blazor)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
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
app.UseAuthentication();

app.UseCors("BlazorLocalPolicy");

app.MapControllers();

app.Run();
