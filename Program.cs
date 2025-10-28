using Microsoft.EntityFrameworkCore;
using Npgsql.EntityFrameworkCore.PostgreSQL;
using Microsoft.AspNetCore.Identity;
using CallbreakApp.Data;
using Microsoft.Extensions.Configuration;  // Added for IConfiguration

var builder = WebApplication.CreateBuilder(args);

// Services
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString ?? throw new InvalidOperationException("Connection string is null")));

builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = false)
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
builder.Services.AddSession();

var app = builder.Build();

// Auto-migrations with connect retry (inject config in scope)
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var config = services.GetRequiredService<IConfiguration>();  // Fetch fresh config
    var connStr = config.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string is null in scope");
    Console.WriteLine($"Scope Connection String: [{connStr}]");  // Log in scope

    var retryCount = 0;
    const int maxRetries = 10;
    ApplicationDbContext? context = null;
    while (retryCount < maxRetries)
    {
        try
        {
            context = services.GetRequiredService<ApplicationDbContext>();
            context.Database.EnsureCreated();
            context.Database.Migrate();
            break;
        }
        catch (Exception ex) when (retryCount < maxRetries - 1)
        {
            retryCount++;
            Console.WriteLine($"Migration retry {retryCount}: {ex.Message}");
            Thread.Sleep(3000 * retryCount);
        }
    }
    if (context == null) throw new InvalidOperationException("Failed to connect to DB after retries.");
}

// Pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

app.Run();