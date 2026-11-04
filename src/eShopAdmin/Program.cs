using Asp.Versioning;
using eShopAdmin.Auth;
using eShopAdmin.BusinessRules;
using eShopAdmin.Catalog;
using eShopAdmin.Components;
using eShopAdmin.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddAdminAuthentication();

builder.Services.AddBusinessRulesClient();

// Catalog only supplies names for the target pickers, so a slow or missing Catalog must not hold a page up.
builder.Services.AddCatalogLookupClient();

builder.Services.AddRazorComponents();
builder.Services.AddCascadingAuthenticationState();

var app = builder.Build();

app.MapDefaultEndpoints();

if (!app.Environment.IsDevelopment())
{
    // First in the pipeline so it sees every failure the ReauthenticateMiddleware lets through.
    app.UseExceptionHandler("/error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseMiddleware<ReauthenticateMiddleware>();
app.UseAuthorization();
app.UseAntiforgery();

// The fallback policy covers every endpoint, so anything that must work before sign-in opts out.
app.MapStaticAssets().AllowAnonymous();
app.MapAdminAuthenticationEndpoints();
app.MapRazorComponents<App>();

app.Run();
