using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using TalaPress.Api.Security;
using TalaPress.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

// Configure Cookie Authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Login";
        options.ExpireTimeSpan = TimeSpan.FromHours(2);
    })
    .AddScheme<AuthenticationSchemeOptions, PearlAuthenticationHandler>(PearlAuthenticationDefaults.AuthenticationScheme, null);

builder.Services.AddControllers();

// Configure Response Compression for insanely fast loading
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});

var app = builder.Build();

await DatabaseInitializer.EnsureDatabaseUpdatesAsync(app.Configuration);

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// Enable Response Compression middleware
app.UseResponseCompression();

// Serve static files with aggressive caching (1 year cache)
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        // Uploaded branding/media should refresh soon after changes in Settings.
        if (ctx.Context.Request.Path.StartsWithSegments("/uploads"))
        {
            ctx.Context.Response.Headers.Append("Cache-Control", "public, max-age=3600");
            return;
        }

        const int cacheDurationInSeconds = 365 * 24 * 60 * 60; // 1 year
        ctx.Context.Response.Headers.Append("Cache-Control", $"public, max-age={cacheDurationInSeconds}");
    }
});

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.UseMiddleware<ApiEnabledMiddleware>();

app.MapRazorPages();
app.MapControllers();

app.Run();
