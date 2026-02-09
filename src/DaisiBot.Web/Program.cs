using Daisi.Orc.Core.Data.Db;
using Daisi.SDK.Models;
using Daisi.SDK.Web.Extensions;
using DaisiBot.Agent.Extensions;
using DaisiBot.Web.Components;
using DaisiBot.Web.Services;
using DaisiBot.Core.Interfaces;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();

// DAISI SDK for web (cookie-based auth)
builder.Services.AddDaisiForWeb()
                .AddDaisiMiddleware()
                .AddDaisiCookieKeyProvider();

// Cosmos DB for skill data
builder.Services.AddSingleton<Cosmo>();

// Skill service backed by Cosmos
builder.Services.AddScoped<ISkillService, CosmoSkillService>();

var app = builder.Build();
app.UseDaisiMiddleware();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAntiforgery();
app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(DaisiBot.Shared.UI._Imports).Assembly);

DaisiStaticSettings.LoadFromConfiguration(
    builder.Configuration.AsEnumerable().ToDictionary(x => x.Key, x => x.Value!));

app.Run();
