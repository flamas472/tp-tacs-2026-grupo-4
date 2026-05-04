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

builder.Services.AddControllers();

builder.Services.AddScoped<NationalTeamService>();
builder.Services.AddScoped<TeamService>();
builder.Services.AddScoped<CategoryService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<StickerService>();
builder.Services.AddScoped<ExchangeProposalService>();
builder.Services.AddScoped<ExchangeService>();

//TODO los repository deberan ser Scoped cuando no sea in-memory
builder.Services.AddSingleton<ExchangeRepository>();
builder.Services.AddSingleton<NationalTeamRepository>();
builder.Services.AddSingleton<TeamRepository>();
builder.Services.AddSingleton<StickerRepository>();
builder.Services.AddSingleton<CategoryRepository>();
builder.Services.AddSingleton<UserRepository>();
builder.Services.AddSingleton<UserStickerRepository>();
builder.Services.AddSingleton<ExchangeProposalRepository>();
builder.Services.AddSingleton<AuctionRepository>();
builder.Services.AddSingleton<AuctionOfferRepository>();

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
