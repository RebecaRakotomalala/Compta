using Microsoft.EntityFrameworkCore;
using Npgsql;
using dadaApp.Data;
using dadaApp.Models;
using dadaApp.Services;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllersWithViews();

// Enregistrer le service AVANT DbContext
builder.Services.AddScoped<ImportComptabiliteService>();
builder.Services.AddScoped<GrandLivreService>();

// the connection string can come from appsettings.json (used in development)
// or it can be supplied via an environment variable by the host (Render, Railway, etc.).
// In development, we prioritize appsettings.json for local database.
// In production, we use DATABASE_URL environment variable if available.
var connString = builder.Configuration.GetConnectionString("DefaultConnection");

// In development, always use appsettings.json (local database)
// In production, prefer DATABASE_URL environment variable
if (builder.Environment.IsDevelopment())
{
    // Development: use appsettings.json, ignore DATABASE_URL
    if (string.IsNullOrWhiteSpace(connString))
    {
        throw new InvalidOperationException(
            "No connection string found in appsettings.json for development. " +
            "Please configure ConnectionStrings:DefaultConnection in appsettings.json or appsettings.Development.json");
    }
    Console.WriteLine("Development mode: Using local database from appsettings.json");
}
else
{
    // Production: prefer DATABASE_URL, fallback to appsettings.json
    var envDbUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
    if (!string.IsNullOrWhiteSpace(envDbUrl))
    {
        connString = envDbUrl;
        Console.WriteLine("Production mode: Using DATABASE_URL from environment");
    }
    else if (string.IsNullOrWhiteSpace(connString))
    {
        throw new InvalidOperationException(
            "No PostgreSQL connection string found. Configure DATABASE_URL environment variable or " +
            "ConnectionStrings:DefaultConnection in appsettings.json");
    }
    else
    {
        Console.WriteLine("Production mode: Using connection string from appsettings.json");
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
    var user = userInfo.Length > 0 ? Uri.UnescapeDataString(userInfo[0]) : string.Empty;
    var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty;
    var host = uri.Host;
    var port = uri.Port;
    var database = uri.AbsolutePath.TrimStart('/');
    
    // start with basic connection parameters
    var connBuilder = new System.Text.StringBuilder();
    connBuilder.Append($"Host={host};");
    // Some providers omit the port in DATABASE_URL; Npgsql rejects -1.
    connBuilder.Append($"Port={(port > 0 ? port : 5432)};");
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

    // Managed PostgreSQL on Render requires TLS. Internal URLs sometimes omit sslmode.
    var interim = connBuilder.ToString();
    if (host.Contains("render.com", StringComparison.OrdinalIgnoreCase)
        && interim.IndexOf("SSL", StringComparison.OrdinalIgnoreCase) < 0)
    {
        connBuilder.Append("SSL Mode=Require;");
    }

    // Hostname without a dot is almost always a truncated copy-paste (missing .xxx-postgres.render.com).
    if (host.IndexOf('.', StringComparison.Ordinal) < 0)
    {
        Console.WriteLine(
            "WARNING: DATABASE_URL host looks incomplete (no domain). " +
            "Use the full URL from Render → Database → Connect → Internal or External Database URL.");
    }

    connString = connBuilder.ToString();
}

if (string.IsNullOrWhiteSpace(connString))
{
    throw new InvalidOperationException(
        "No PostgreSQL connection string found. " +
        (builder.Environment.IsDevelopment()
            ? "Configure ConnectionStrings:DefaultConnection in appsettings.json"
            : "Configure DATABASE_URL environment variable or ConnectionStrings:DefaultConnection"));
}

connString = TuneConnectionForRenderHosting(connString);

// Log connection info (without password for security)
var connInfo = connString;
if (connString.Contains("Password="))
{
    var pwdIdx = connString.IndexOf("Password=", StringComparison.OrdinalIgnoreCase);
    if (pwdIdx >= 0)
    {
        var endIdx = connString.IndexOf(';', pwdIdx);
        if (endIdx < 0) endIdx = connString.Length;
        connInfo = connString.Substring(0, pwdIdx) + "Password=***" + connString.Substring(endIdx);
    }
}
Console.WriteLine($"Database connection string detected: {connInfo}");
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(connString);
});

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

    var skipMigrate =
        string.Equals(Environment.GetEnvironmentVariable("SKIP_STARTUP_MIGRATIONS"),
            "true",
            StringComparison.OrdinalIgnoreCase);

    if (skipMigrate)
    {
        Console.WriteLine(
            "SKIP_STARTUP_MIGRATIONS=true — aucune migration au démarrage (temporaire). Appliquez les migrations à la main (Render Shell ou CI).");
    }
    else
    {
        const int migrateAttempts = 6;
        Exception? migrateFailure = null;

        for (var attempt = 1; attempt <= migrateAttempts; attempt++)
        {
            try
            {
                db.Database.Migrate();
                migrateFailure = null;
                if (attempt > 1)
                    Console.WriteLine($"Migrations appliquées au bout de la tentative {attempt}.");
                break;
            }
            catch (Exception ex)
            {
                migrateFailure = ex;
                Console.WriteLine($"Migration tentative {attempt}/{migrateAttempts} : {ex.Message}");
                if (ex.InnerException != null)
                    Console.WriteLine($"  → Cause interne : {ex.InnerException.Message}");

                if (attempt < migrateAttempts)
                    Thread.Sleep(TimeSpan.FromSeconds(Math.Min(25, 4 * attempt)));
            }
        }

        if (migrateFailure != null)
        {
            var ex = migrateFailure;
            Console.WriteLine("Erreur migrations après plusieurs tentatives : " + ex.Message);
            Console.WriteLine(
                "Astuces Render : même région pour le Web Service et la base ; essayez « External Database URL » " +
                "dans DATABASE_URL si l’internal échoue au démarrage ; SCRAM/TLS → Channel Binding désactivé dans le code.");
            Console.WriteLine(
                "Pour débloquer un déploiement : SKIP_STARTUP_MIGRATIONS=true puis appliquez les migrations manuellement.");
            if (!app.Environment.IsDevelopment())
                throw migrateFailure;
        }
    }

    try
    {
        var defaultUsername = Environment.GetEnvironmentVariable("DEFAULT_LOGIN_USERNAME") ?? "Dada";
        var defaultPassword = Environment.GetEnvironmentVariable("DEFAULT_LOGIN_PASSWORD") ?? "reriro";

        var existingUser = db.Users.FirstOrDefault(u => u.Username == defaultUsername);
        if (existingUser == null)
        {
            db.Users.Add(new User
            {
                Username = defaultUsername,
                Password = defaultPassword
            });
            db.SaveChanges();
            Console.WriteLine($"Default login user created: {defaultUsername}");
        }
        else
        {
            // Keep this account usable even if password changed manually in DB.
            if (existingUser.Password != defaultPassword)
            {
                existingUser.Password = defaultPassword;
                db.SaveChanges();
                Console.WriteLine($"Default login user password reset: {defaultUsername}");
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine("Warning: could not create default login user: " + ex.Message);
    }
}

// In production (Render, Railway, etc.), the PORT environment variable is provided.
// In development, we use launchSettings.json URLs.
if (!app.Environment.IsDevelopment())
{
    // Production: use PORT environment variable
    var portValue = Environment.GetEnvironmentVariable("PORT") ?? "8080";
    app.Urls.Add($"http://0.0.0.0:{portValue}");
    Console.WriteLine($"🚀 Application started on port {portValue} (production mode)");
}
else
{
    // Development: let ASP.NET Core use launchSettings.json URLs
    // We'll log the URLs after app.Run() is called, but we can also check them here
    Console.WriteLine("🚀 Application starting in Development mode...");
    Console.WriteLine("   Check launchSettings.json for configured URLs (typically http://localhost:5271)");
}

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

// Log the URLs the app is listening on
Console.WriteLine("🌐 Application URLs:");
var urls = app.Urls.ToList();
if (urls.Any())
{
    foreach (var url in urls)
    {
        Console.WriteLine($"   → {url}");
    }
}
else
{
    // In development, URLs come from launchSettings.json (will be shown by ASP.NET Core)
    Console.WriteLine("   → Using URLs from launchSettings.json");
    Console.WriteLine("   → HTTP: http://localhost:5271");
    Console.WriteLine("   → HTTPS: https://localhost:7167 (if https profile is used)");
}

app.Run();

/// <summary>
/// Ajustements pour PostgreSQL managé Render (SCRAM/TLS): délais, désactivation du channel binding TLS
/// (certains réseaux font tomber la connexion pendant Authenticate sans cela).
/// </summary>
static string TuneConnectionForRenderHosting(string connectionString)
{
    try
    {
        var b = new NpgsqlConnectionStringBuilder(connectionString);
        if (string.IsNullOrEmpty(b.Host) ||
            !b.Host.Contains("render.com", StringComparison.OrdinalIgnoreCase))
            return connectionString;

        b.SslMode = SslMode.Require;
        b.Timeout = Math.Max(b.Timeout, 120);
        b.ChannelBinding = ChannelBinding.Disable;
        return b.ConnectionString;
    }
    catch (Exception ex)
    {
        Console.WriteLine("Warning: TuneConnectionForRenderHosting: " + ex.Message);
        return connectionString;
    }
}