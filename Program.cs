using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using smart_pet_care_api.Data;
using smart_pet_care_api.Extensions;
using smart_pet_care_api.Infrastructure.Cloudinary;
using smart_pet_care_api.Modules.AuthModule;
using smart_pet_care_api.Modules.AuthModule.Infrastructure;
using smart_pet_care_api.Modules.PetModule;
using smart_pet_care_api.Modules.ReminderModule;
using smart_pet_care_api.Modules.UserModule;

var builder = WebApplication.CreateBuilder(args);

var cs = builder.Configuration.GetConnectionString("DbConnection");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(cs));

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddUserModule();
builder.Services.AddPetModule();
builder.Services.AddAuthModule(builder.Configuration);
builder.Services.AddReminderModule();
builder.Services.AddScalarConfig();
builder.Services.Configure<CloudinaryOptions>(options =>
{
    options.CloudName = builder.Configuration["CLOUDINARY_NAME"] ?? string.Empty;
    options.ApiKey = builder.Configuration["CLOUDINARY_API_KEY"] ?? string.Empty;
    options.ApiSecret = builder.Configuration["CLOUDINARY_API_SECRET"] ?? string.Empty;
});
builder.Services.AddScoped<ICloudinaryService, CloudinaryService>();

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

var app = builder.Build();

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedProto
});

app.UseScalarConfig();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    try
    {
        db.Database.CanConnect();
        db.Database.Migrate();
        Console.WriteLine("✅ Database connected");
    }
    catch (Exception ex)
    {
        Console.WriteLine("❌ Database connection failed: " + ex.Message);
    }
}

app.Lifetime.ApplicationStarted.Register(() =>
{
    Console.WriteLine("\n🚀 API started!");

    foreach (var url in app.Urls)
    {
        Console.WriteLine($"👉 Scalar available at: {url}/scalar/v1");
    }

    Console.WriteLine();
});

if (!app.Environment.IsProduction())
    app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseMiddleware<AuthMiddleware>();
app.UseAuthorization();
app.MapControllers();

app.Run();
