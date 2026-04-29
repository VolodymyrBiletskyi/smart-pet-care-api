using Microsoft.EntityFrameworkCore;
using smart_pet_care_api.Data;
using smart_pet_care_api.Modules.UserModule.Domain;
using smart_pet_care_api.Modules.UserModule.Repository;

var builder = WebApplication.CreateBuilder(args);

var cs = builder.Configuration.GetConnectionString("DbConnection");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(cs));

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// DI
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IUserService, UserService>();

// Controllers
builder.Services.AddControllers();

var app = builder.Build();

// Swagger
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// DB check
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    try
    {
        db.Database.CanConnect();
        Console.WriteLine("✅ Database connected");
    }
    catch (Exception ex)
    {
        Console.WriteLine("❌ Database connection failed: " + ex.Message);
    }
}

// Console output
app.Lifetime.ApplicationStarted.Register(() =>
{
    Console.WriteLine("\n🚀 API started!");

    foreach (var url in app.Urls)
    {
        Console.WriteLine($"👉 Swagger available at: {url}/swagger");
    }

    Console.WriteLine();
});

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization();

app.MapControllers();

app.Run();