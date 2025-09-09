using BattleshipsGame.Hubs;
using BattleshipsGame.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddControllers(); // For API controllers (AJAX endpoints)
builder.Services.AddSignalR(); // WebSocket communication

// Register custom services
builder.Services.AddSingleton<GameService>();
builder.Services.AddSingleton<GameMatchmakingService>();
builder.Services.AddSingleton<ThreadPoolGameProcessor>();
builder.Services.AddSingleton<BarrierSynchronizationService>();

// Add HttpClient for AJAX calls
builder.Services.AddHttpClient();

// Add logging
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var app = builder.Build();

// Configure pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// Map endpoints
app.MapRazorPages();
app.MapBlazorHub();
app.MapControllers(); // API endpoints for AJAX
app.MapHub<GameHub>("/gamehub"); // WebSocket hub
// app.MapFallbackToPage("/_Host");

app.Run();
