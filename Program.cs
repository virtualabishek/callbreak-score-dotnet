using Microsoft.EntityFrameworkCore;
using Npgsql.EntityFrameworkCore.PostgreSQL;
using Microsoft.AspNetCore.Identity;
using CallbreakApp.Data;
using Microsoft.Extensions.Configuration;  

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
// Auto-migrations with smart connect retry
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var config = services.GetRequiredService<IConfiguration>();
    var connStr = config.GetConnectionString("DefaultConnection")
                  ?? throw new InvalidOperationException("Connection string is null in scope");
    Console.WriteLine($"Scope Connection String: [{connStr}]");

    var retryCount = 0;
    const int maxRetries = 5;

    while (retryCount < maxRetries)
    {
        try
        {
            var context = services.GetRequiredService<ApplicationDbContext>();
            // ONLY run migrations; don't use EnsureCreated()
            context.Database.Migrate();
            Console.WriteLine("Database migration succeeded.");
            break; // success, exit loop
        }
        catch (Npgsql.PostgresException ex) when (ex.SqlState == "08001" || ex.SqlState == "57P01")
        {
            // Connection-related errors, safe to retry
            retryCount++;
            Console.WriteLine($"Database connection failed, retry {retryCount}/{maxRetries}: {ex.Message}");
            Thread.Sleep(3000 * retryCount);
        }
        catch (Exception ex)
        {
            // Schema conflicts or other issues, don't retry endlessly
            Console.WriteLine($"Database migration failed: {ex.Message}");
            throw; // crash, so you can fix schema issues
        }
    }
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