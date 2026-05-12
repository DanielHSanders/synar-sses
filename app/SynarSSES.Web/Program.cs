using Dapper;
using Microsoft.AspNetCore.HttpOverrides;
using SynarSSES.Core.Repositories;
using SynarSSES.Core.Services;

DefaultTypeMap.MatchNamesWithUnderscores = true;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Synar")
    ?? throw new InvalidOperationException("ConnectionStrings:Synar is not configured.");

builder.Services.AddRazorPages();

// Forwarded headers: nginx terminates TLS and proxies HTTP to Kestrel on
// 127.0.0.1:5001. Pick up the real scheme/host/IP from the proxy.
builder.Services.Configure<ForwardedHeadersOptions>(opts =>
{
    opts.ForwardedHeaders = ForwardedHeaders.XForwardedFor
                          | ForwardedHeaders.XForwardedProto
                          | ForwardedHeaders.XForwardedHost;
    opts.KnownNetworks.Clear();
    opts.KnownProxies.Clear();
    opts.KnownProxies.Add(System.Net.IPAddress.Parse("127.0.0.1"));
    opts.KnownProxies.Add(System.Net.IPAddress.IPv6Loopback);
});

builder.Services.AddScoped<MicrodataRepository>(_ => new MicrodataRepository(connectionString));
builder.Services.AddScoped<MapLocationRepository>(_ => new MapLocationRepository(connectionString));
builder.Services.AddScoped<OutlineRepository>(_ => new OutlineRepository(connectionString));
builder.Services.AddSingleton<MarkdownRenderer>();
builder.Services.AddSingleton<SssCalculator>();
builder.Services.AddSingleton<SssWorkbookWriter>();
builder.Services.AddSingleton<SampleSizeCalculator>();
builder.Services.AddSingleton<SampleDrawer>();
builder.Services.AddSingleton<CheckTypeAssigner>();
builder.Services.AddSingleton<SampleSizeWorkbookWriter>();

var app = builder.Build();

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseRouting();

app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();

app.Run();
