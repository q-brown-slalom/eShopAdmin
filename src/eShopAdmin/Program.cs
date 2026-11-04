using Asp.Versioning;
using eShopAdmin.Auth;
using eShopAdmin.BusinessRules;
using eShopAdmin.Components;
using eShopAdmin.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddAdminAuthentication();

builder.Services.AddBusinessRulesClient();

builder.Services.AddRazorComponents();
builder.Services.AddCascadingAuthenticationState();

var app = builder.Build();

app.MapDefaultEndpoints();

if (!app.Environment.IsDevelopment())
{
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
