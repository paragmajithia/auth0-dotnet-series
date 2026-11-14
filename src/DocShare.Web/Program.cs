using System.Net.Http.Headers;
using System.Net;
using Auth0.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

// Why: Domain, ClientId and Audience are not secrets, so they live in appsettings.json.
var auth0 = builder.Configuration.GetSection("Auth0");
var audience = auth0["Audience"] ?? throw new InvalidOperationException("Auth0:Audience is missing");

// Why: handles the login redirect, the /callback return and the session cookie for us.
// ClientSecret is NOT in any file: it comes from user-secrets (key Auth0:ClientSecret).
builder.Services
    .AddAuth0WebAppAuthentication(options =>
    {
        options.Domain = auth0["Domain"]!;
        options.ClientId = auth0["ClientId"]!;
        options.ClientSecret = auth0["ClientSecret"]; // from user-secrets
        options.Scope = "openid profile email";
    })
    // Why: asking for our API's audience makes Auth0 issue a JWT access token the API can validate.
    .WithAccessToken(options =>
    {
        options.Audience = audience;
    });

// Why: HttpClientFactory gives us a ready HttpClient for calling DocShare.Api.
builder.Services.AddHttpClient("DocShareApi", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["DocShareApi:BaseUrl"]!);
});

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

// Why: tiny helper so every page shares the same layout and menu without a view engine.
static IResult Page(string title, string body, bool loggedIn)
{
    var nav = loggedIn
        ? "<a href='/'>Home</a> | <a href='/profile'>Claims</a> | <a href='/call-api'>Call API</a> | <a href='/logout'>Logout</a>"
        : "<a href='/'>Home</a> | <a href='/login'>Login</a>";
    const string style = "body{font-family:sans-serif;margin:2rem auto;max-width:48rem;padding:0 1rem}"
        + "table{border-collapse:collapse;width:100%}td,th{border:1px solid #ccc;padding:.4rem;text-align:left;word-break:break-all}"
        + "pre{background:#f4f4f4;padding:1rem;overflow:auto}";
    var html = "<!doctype html><html><head><meta charset='utf-8'>"
        + "<meta name='viewport' content='width=device-width, initial-scale=1'>"
        + $"<title>{title}</title><style>{style}</style></head>"
        + $"<body><nav>{nav}</nav><h1>{title}</h1>{body}</body></html>";
    return Results.Content(html, "text/html");
}

// Why: public home page with a login link.
app.MapGet("/", (HttpContext ctx) =>
    Page("DocShare Web", "<p>Server-side login client for the DocShare API.</p>",
        ctx.User.Identity?.IsAuthenticated == true));

// Why: starts the Auth0 login. After login, Auth0 returns to /callback, then we land on /profile.
app.MapGet("/login", async (HttpContext ctx) =>
{
    var props = new LoginAuthenticationPropertiesBuilder().WithRedirectUri("/profile").Build();
    await ctx.ChallengeAsync(Auth0Constants.AuthenticationScheme, props);
});

// Why: clear the Auth0 session and our local cookie, then return home (an allowed logout URL).
app.MapGet("/logout", async (HttpContext ctx) =>
{
    var props = new LogoutAuthenticationPropertiesBuilder().WithRedirectUri("/").Build();
    await ctx.SignOutAsync(Auth0Constants.AuthenticationScheme, props);
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
});

// Why: shows what Auth0 told us about the user (the ID token claims). Needs a login.
app.MapGet("/profile", (HttpContext ctx) =>
{
    var rows = string.Join("", ctx.User.Claims.Select(c =>
        $"<tr><td>{WebUtility.HtmlEncode(c.Type)}</td><td>{WebUtility.HtmlEncode(c.Value)}</td></tr>"));
    return Page("Your claims", $"<table><tr><th>Claim</th><th>Value</th></tr>{rows}</table>", true);
}).RequireAuthorization();

// Why: sends the access token to the API as a Bearer token and shows what came back.
app.MapGet("/call-api", async (HttpContext ctx, IHttpClientFactory factory) =>
{
    // Why: the access token (not the ID token) is what the API expects.
    var accessToken = await ctx.GetTokenAsync("access_token");
    if (string.IsNullOrEmpty(accessToken))
        return Page("Call API", "<p>No access token found. Log in again.</p>", true);

    var client = factory.CreateClient("DocShareApi");
    using var request = new HttpRequestMessage(HttpMethod.Get, "/documents");
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

    using var response = await client.SendAsync(request);
    var body = await response.Content.ReadAsStringAsync();

    var html = $"<p>GET /documents returned <strong>{(int)response.StatusCode} {response.StatusCode}</strong></p>"
             + $"<pre>{WebUtility.HtmlEncode(body)}</pre>";
    return Page("Call API", html, true);
}).RequireAuthorization();

app.Run();
