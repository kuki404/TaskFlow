using TaskFlow.Web.Components;
using TaskFlow.Web.Mapping;
using TaskFlow.Web.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor.Services;

MapsterConfig.Configure();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();

// This Web app never signs a cookie in — auth state comes from CustomAuthStateProvider, backed by
// the JWT the Api issued. A default scheme is still registered because AuthorizeRouteView runs
// through ASP.NET Core's own authorization middleware during the initial static render, before any
// Blazor circuit (and so CustomAuthStateProvider's read of ProtectedSessionStorage) can even exist.
// That first, unauthenticated static request to an [Authorize] page (e.g. "/") gets challenged by
// this cookie scheme; LoginPath must point at this app's real "/login" route, or ASP.NET Core's
// default "/Account/Login" 404s before the user ever sees a login page.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options => options.LoginPath = "/login");
builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthSession>();
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthStateProvider>();
builder.Services.AddScoped<ThemeService>();

var apiBaseUrl = builder.Configuration["Api:BaseUrl"] ?? "http://localhost:5099";
builder.Services.AddHttpClient<TaskFlowApiClient>(client => client.BaseAddress = new Uri(apiBaseUrl));
builder.Services.AddSingleton(_ => new Uri(apiBaseUrl));

builder.Services.AddHealthChecks();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.MapHealthChecks("/health").AllowAnonymous();

app.Run();
