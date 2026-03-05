using Microsoft.EntityFrameworkCore;
using dadaApp.Data;
using dadaApp.Services;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllersWithViews();

// Enregistrer le service AVANT DbContext
builder.Services.AddScoped<ImportComptabiliteService>();
builder.Services.AddScoped<GrandLivreService>();

// the connection string can come from appsettings.json (used in development)
// or it can be supplied via an environment variable by the host (fly.io, Supabase, etc.).
// Supabase exposes a standard DATABASE_URL that contains the full PostgreSQL
// connection string.  The configuration system will already read a
// "ConnectionStrings:DefaultConnection" value from appsettings, but in cloud
// environments we fall back to the env var if it is present.
var connString = builder.Configuration.GetConnectionString("DefaultConnection");
// if the configured value is the localhost development string, prefer the env var
if (string.IsNullOrWhiteSpace(connString) || connString.Contains("localhost"))
{
    var env = Environment.GetEnvironmentVariable("DATABASE_URL");
    if (!string.IsNullOrWhiteSpace(env))
    {
        connString = env;
    }
}

// if the connection string comes from a URI (as Supabase or Neon supply), we need
// to translate it into the key=value format that Npgsql (and EF Core) expect.
// NpgsqlConnectionStringBuilder doesn't currently handle the URI form.
// This also parses any query parameters (e.g., sslmode, channel_binding from Neon).
if (connString != null && (connString.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
    || connString.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase)))
{
    var uri = new Uri(connString);
    var userInfo = uri.UserInfo.Split(':', 2);
    var user = userInfo.Length > 0 ? userInfo[0] : string.Empty;
    var password = userInfo.Length > 1 ? userInfo[1] : string.Empty;
    var host = uri.Host;
    var port = uri.Port;
    var database = uri.AbsolutePath.TrimStart('/');
    
    // start with basic connection parameters
    var connBuilder = new System.Text.StringBuilder();
    connBuilder.Append($"Host={host};");
    connBuilder.Append($"Port={port};");
    connBuilder.Append($"Database={database};");
    connBuilder.Append($"Username={user};");
    connBuilder.Append($"Password={password};");
    
    // parse any query parameters (e.g., sslmode=require, channel_binding=require from Neon)
    if (!string.IsNullOrEmpty(uri.Query))
    {
        var queryParams = System.Web.HttpUtility.ParseQueryString(uri.Query);
        foreach (string key in queryParams.Keys)
        {
            var value = queryParams[key];
            // convert snake_case to PascalCase for Npgsql (e.g. sslmode -> SSL Mode)
            var npgsqlKey = System.Text.RegularExpressions.Regex.Replace(
                key,
                @"(?:^|_)(.)",
                m => m.Groups[1].Value.ToUpper()
            );
            connBuilder.Append($"{npgsqlKey}={value};");
        }
    }
    
    connString = connBuilder.ToString();
}

// log the final connection string for diagnostic purposes (omit secrets in
// any shared logs!)
Console.WriteLine("Using connection string: " + connString);
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connString));

var app = builder.Build();

// apply any pending EF Core migrations automatically. this is handy when the
// container starts in a fresh supabase database – the migrations in our
// project will be executed without manual intervention.  However, if the
// database is unreachable (e.g. you're on a machine without IPv6 connectivity
// and the Supabase host only has an AAAA record), we catch the exception and
// continue running.  In production you generally want migrations to succeed or
// the app to fail loudly, but for local development it's annoying to crash.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        db.Database.Migrate();
    }
    catch (Exception ex)
    {
        Console.WriteLine("Warning: could not apply migrations: " + ex.Message);
        Console.WriteLine("This often happens when the database host is IPv6-only and"
                          + " the current network has no IPv6 connectivity.");
    }
}

// fly.io (and many other hosts) communicate the port the container should
// listen on via the PORT environment variable.  Kestrel will bind to that
// value if we add it to the URLs collection; default to 8080 for local
// testing.
var portValue = Environment.GetEnvironmentVariable("PORT") ?? "8080";
app.Urls.Add("http://0.0.0.0:8080");
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Commentez cette ligne si vous n'avez pas de HTTPS configuré
// app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

app.Run();