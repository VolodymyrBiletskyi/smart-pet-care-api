using Microsoft.EntityFrameworkCore;
using smart_pet_care_api.Data;
using smart_pet_care_api.Extensions;
using smart_pet_care_api.Modules.AuthModule;
using smart_pet_care_api.Modules.AuthModule.Infrastructure;
using smart_pet_care_api.Modules.UserModule.Domain;
using smart_pet_care_api.Modules.UserModule.Repository;

var builder = WebApplication.CreateBuilder(args);

var cs = builder.Configuration.GetConnectionString("DbConnection");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(cs));

// Scalar
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// DI
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddAuthModule(builder.Configuration);
builder.Services.AddScalarConfig();

// Controllers
builder.Services.AddControllers();

var app = builder.Build();

app.UseScalarConfig();
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
        Console.WriteLine($"👉 Scalar available at: {url}/scalar/v1");
    }

    Console.WriteLine();
});

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthentication();
app.UseMiddleware<AuthMiddleware>();
app.UseAuthorization();

app.MapControllers();

app.Run();