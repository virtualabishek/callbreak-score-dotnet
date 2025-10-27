using Microsoft.EntityFrameworkCore;
using Npgsql.EntityFrameworkCore.PostgreSQLusing;
using Microsoft.AspNetCore.Identity;
using CallbreakApp.Data;

var builder = WebApplication.CreateBuilder(args);

// Services
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = false)
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
builder.Services.AddSession();

var app = builder.Build();

// Auto-migrations with connect retry (wraps DbContext init)
using (var scope = app.Services.CreateScope())
{
    var retryCount = 0;
    const int maxRetries = 10;
    ApplicationDbContext? context = null;
    while (retryCount < maxRetries)
    {
        try
        {
            context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
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