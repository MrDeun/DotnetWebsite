using EcosystemSimulation.Hubs;
using EcosystemSimulation.Services;
using EcosystemSimulation.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSignalR();
builder.Services.AddHostedService<SimulationService>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAntiforgery();

app.UseRouting();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapHub<SimulationHub>("/simulationhub");

app.Run();