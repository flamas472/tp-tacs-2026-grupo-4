using System.Text;
using System.Text.Json.Serialization;
using Figuritas.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Figuritas.Api.Middlewares;

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
    var secretKey = builder.Configuration["Jwt:Key"] 
                ?? throw new InvalidOperationException("Falta la configuración 'Jwt:Key' en appsettings.json");

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
    };
});

builder.Services.AddControllers()
    .AddJsonOptions(options => {
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter());
    });

builder.Services.AddSingleton<MongoDbContext>();
builder.Services.AddSingleton<IIdGenerator, MongoIdGenerator>();

// Inicializamos los services
builder.Services.AddScoped<NationalTeamService>();
builder.Services.AddScoped<TeamService>();
builder.Services.AddScoped<CategoryService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<StickerService>();
builder.Services.AddScoped<ExchangeProposalService>();
builder.Services.AddScoped<ExchangeService>();

// Inicializamos los repositorios
builder.Services.AddScoped<ExchangeRepository>();
builder.Services.AddScoped<NationalTeamRepository>();
builder.Services.AddScoped<TeamRepository>();
builder.Services.AddScoped<StickerRepository>();
builder.Services.AddScoped<CategoryRepository>();
builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<UserStickerRepository>();
builder.Services.AddScoped<ExchangeProposalRepository>();
builder.Services.AddScoped<AuctionRepository>();
builder.Services.AddScoped<AuctionOfferRepository>();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("BlazorLocalPolicy", policy =>
    {
        policy.WithOrigins("http://localhost:5280","http://localhost:5048") // permitir CORS
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// Seed initial domain data in MongoDB.
using (var scope = app.Services.CreateScope())
{
    SeedData.EnsureSeedData(scope.ServiceProvider);
}

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

app.UseAuthentication();
app.UseAuthorization();

app.UseCors("BlazorLocalPolicy");

app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
app.MapControllers();

app.Run();
