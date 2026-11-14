using DocShare.Api;

var builder = WebApplication.CreateBuilder(args);

// Why: read the tenant domain and audience from config, never hard-code them.
var domain = builder.Configuration["Auth0:Domain"]
    ?? throw new InvalidOperationException("Auth0:Domain is missing");
var audience = builder.Configuration["Auth0:Audience"]
    ?? throw new InvalidOperationException("Auth0:Audience is missing");

// Why: trust only tokens issued by our Auth0 tenant (Authority) and meant for this API (Audience).
// The signing keys (JWKS) are downloaded from the authority automatically.
builder.Services
    .AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        options.Authority = $"https://{domain}/";
        options.Audience = audience;
        // Why: keep claim names as they are in the token, so "permissions" stays "permissions".
        options.MapInboundClaims = false;
    });

// Why: turn each permission in the token's "permissions" claim into a reusable policy.
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("read:documents", p => p.RequireClaim("permissions", "read:documents"))
    .AddPolicy("write:documents", p => p.RequireClaim("permissions", "write:documents"))
    .AddPolicy("share:documents", p => p.RequireClaim("permissions", "share:documents"));

// Why: one shared in-memory store for all requests.
builder.Services.AddSingleton<DocumentStore>();

var app = builder.Build();

// Why: order matters. Authenticate (who are you) first, then authorize (what may you do).
app.UseAuthentication();
app.UseAuthorization();

// Why: public endpoint with no token, to check that the API is up.
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// Why: needs read:documents. No token gives 401, token without permission gives 403.
app.MapGet("/documents", (DocumentStore store) => Results.Ok(store.All()))
    .RequireAuthorization("read:documents");

// Why: needs write:documents to create a document.
app.MapPost("/documents", (NewDocument body, DocumentStore store) =>
{
    var doc = store.Add(body.Org, body.Title);
    return Results.Created($"/documents/{doc.Id}", doc);
}).RequireAuthorization("write:documents");

// Why: needs share:documents. Only owners have it, so viewers and editors get 403.
app.MapPost("/documents/{id:int}/share", (int id, DocumentStore store) =>
{
    var doc = store.Find(id);
    return doc is null
        ? Results.NotFound()
        : Results.Ok(new { shared = true, doc.Id, doc.Title });
}).RequireAuthorization("share:documents");

app.Run();

// Why: request body for POST /documents.
public record NewDocument(string Org, string Title);
