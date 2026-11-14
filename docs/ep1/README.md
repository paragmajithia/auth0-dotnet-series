# Episode 1: AuthN vs AuthZ, OIDC, JWT, Auth0 + .NET basics

Status: Stage 1 (Concept) and Stage 2 (Auth0 setup) done. Later stages are added below as they are finished.

---

## Stage 1: Concept

### AuthN vs AuthZ

| | AuthN (authentication) | AuthZ (authorization) |
|---|---|---|
| Question | Who are you? | What may you do? |
| Standard | OpenID Connect (OIDC) | OAuth 2.0 |
| Token | ID token | Access token |
| Who reads it | The client app | The API |
| DocShare example | Carol logs in | Carol (viewer) may `read:documents` but not `share:documents` |

OIDC is a thin identity layer on top of OAuth 2.0. OAuth alone says what a token can do. OIDC adds who the user is.

### The flow (Authorization Code + PKCE)

```
Browser        DocShare.Web      Auth0         DocShare.Api
   |  open app      |              |                |
   |--------------->|              |                |
   |  redirect to /authorize       |                |
   |<--------------|------------->|                |
   |  login (Alice)                |                |
   |------------------------------>|                |
   |  redirect back with ?code=    |                |
   |<------------------------------|                |
   |        |-- code -> tokens --->|                |
   |        |<- ID + access token -|                |
   |        |-- GET /documents + Bearer token ----->|
   |        |        validate JWT + check permission|
   |        |<-------------- 200 or 403 ------------|
```

- **Web app:** handles the login and stores the tokens.
- **API:** never sees a password. It only validates the access token.

### What a JWT is

A JWT is three base64url parts: `header.payload.signature`. The API checks the signature against Auth0's public keys (JWKS), then trusts the payload.

```json
{
  "iss": "https://<tenant>.auth0.com/",
  "sub": "auth0|abc123",
  "aud": "https://docshare-api",
  "exp": 1760000000,
  "permissions": ["read:documents", "write:documents"]
}
```

| Claim | API checks |
|---|---|
| `iss` | Token came from my tenant |
| `aud` | Token was meant for this API |
| `exp` | Token has not expired |
| `permissions` | Used for authorization |

### How it maps to .NET

Previews only. The real code is in Stage 3.

**1. Authentication: validate the JWT**
```csharp
// Why: trust only tokens from our Auth0 tenant and audience
builder.Services.AddAuthentication().AddJwtBearer(o =>
{
    o.Authority = "https://<tenant>/";   // issuer, keys fetched automatically
    o.Audience  = "https://docshare-api";
});
```

**2. Authorization: a policy on a permission**
```csharp
// Why: turns "read:documents" in the token into a reusable rule
options.AddPolicy("read:documents", p =>
    p.RequireClaim("permissions", "read:documents"));
```

**3. Enforce it on an endpoint**
```csharp
// Why: no valid token gives 401, valid token without the permission gives 403
app.MapGet("/documents", () => Results.Ok()).RequireAuthorization("read:documents");
```

### End-to-end example

| Step | Who | What happens |
|---|---|---|
| 1 | Alice (Acme, owner) | Clicks Login in DocShare.Web |
| 2 | Auth0 | Authenticates her, returns ID token and access token |
| 3 | Web app | Calls `GET /documents` with the access token |
| 4 | API | Validates `iss`, `aud`, `exp` and signature |
| 5 | API | Finds `read:documents`, returns **200** |
| 6 | Carol (Globex, viewer) | Same flow, then calls `POST /documents/1/share` |
| 7 | API | No `share:documents` in her token, returns **403** |

| Status | Meaning |
|---|---|
| 401 | Not authenticated: no token, or the token is invalid |
| 403 | Authenticated, but not allowed |

---

## Stage 2: Auth0 setup

Dashboard labels move occasionally, so treat names as a guide. Never commit secrets. The Client Secret goes only into user-secrets.

### Step 1: Tenant

**Why:** the tenant is your private Auth0 space. Every app, API, user and role below lives inside it, and its domain is the address our code talks to.

1. Sign up at auth0.com (free plan).
2. Pick your nearest region. Auth0 generates the tenant name (for example `dev-xxxxxxxx`) and it cannot be changed. It becomes part of your domain.
3. Go to **Settings → General** and set **Friendly Name** to `authseries-dev`. This is only a display label. The code uses the Domain from the Application settings.

### Step 2: Create the API

**Why:** Auth0 needs to know our API exists, for three reasons:
- **Audience:** the identifier `https://docshare-api` becomes the `aud` claim in access tokens. The API rejects tokens meant for another audience.
- **Permissions:** `read:documents` and the others are defined on the API. Without it there is nothing to authorize against.
- **Access token:** if the web app does not ask for this audience, Auth0 returns an opaque token that the API cannot validate. With it, Auth0 issues a signed JWT for our API.

We register two things in Auth0, and they have different jobs:

| Registered in Auth0 | Role | Question it answers |
|---|---|---|
| `DocShare Web` (Step 5) | The client that logs users in | Who is the user? (AuthN) |
| `DocShare API` (this step) | The resource that tokens are issued for | What can the token holder do? (AuthZ) |

1. **Applications → APIs → Create API**.
2. Name: `DocShare API`.
3. Identifier: `https://docshare-api` (the audience, a label and not a real URL).
4. Signing algorithm: RS256. Click **Create**.
5. In the API's **Settings** tab, under RBAC Settings:
   - Turn on **Enable RBAC**.
   - Turn on **Add Permissions in the Access Token**. Without it the `permissions` claim will not appear.
   - Save.

### Step 3: Add permissions

**Why:** permissions are the fine-grained actions the API can check. They end up in the token's `permissions` claim and drive the policies in Stage 3.

In the API's **Permissions** tab:

| Permission | Description |
|---|---|
| `read:documents` | Read documents |
| `write:documents` | Create and edit documents |
| `share:documents` | Share documents |

### Step 4: Create roles

**Why:** assigning permissions to every user one by one does not scale. A role bundles permissions, so we assign a role to a user and Auth0 puts the right permissions in their token.

**User Management → Roles → Create Role**. For each role, open **Permissions → Add Permissions**, select `DocShare API` and tick the permissions.

| Role | Permissions |
|---|---|
| viewer | `read:documents` |
| editor | `read:documents`, `write:documents` |
| owner | `read:documents`, `write:documents`, `share:documents` |

### Step 5: Create the web application

**Why:** Auth0 only redirects users back to, and issues tokens to, applications it knows about. Registering `DocShare Web` gives us:
- **Client ID and Secret:** they identify our app to Auth0, so nobody else can impersonate it.
- **Allowed URLs:** the callback and logout URLs are an allow-list. Auth0 refuses to redirect anywhere else, which blocks token theft through fake redirects.
- **Login settings:** it decides which sign-in methods users see (here, username and password).

The web app does the AuthN (logs the user in). The API from Step 2 does the AuthZ. The web app asks for an access token for the API's audience and sends it to the API.

1. **Applications → Applications → Create Application**.
2. Name: `DocShare Web`. Type: **Regular Web Applications**. We use a server-side app, not a SPA: the whole demo stays in C#, the Client Secret lives in user-secrets, and tokens never reach the browser.
3. In **Settings**, enter the URLs below. `7001` is a placeholder port. In Stage 3 we create the project, check the real HTTPS port in `launchSettings.json`, and come back here to update these three URLs if it differs:

| Field | Value |
|---|---|
| Allowed Callback URLs | `https://localhost:7001/callback` |
| Allowed Logout URLs | `https://localhost:7001/` |
| Allowed Web Origins | `https://localhost:7001` |

4. Save. On the **Connections** tab, make sure Username-Password-Authentication is enabled.

### Step 6: Create fake users

**Why:** we need people to log in as and to show different access levels. Different roles give different results from the same API, for example Carol (viewer) is refused when she tries to share. Synthetic emails keep real data out of the series.

**User Management → Users → Create User** (default database connection, synthetic emails only). On each user's **Roles** tab, click **Assign Roles**.

| User | Email | Role |
|---|---|---|
| Alice (Acme) | `alice@acme.test` | owner |
| Vibha (Acme) | `vibha@acme.test` | editor |
| Carol (Globex) | `carol@globex.test` | viewer |

### Step 7: Values to copy

**Why:** our code needs to know which tenant, which app and which API to talk to. These values are the link between the Auth0 dashboard and the C# project.

Nothing to edit in code yet. The project and `appsettings.json` are created in Stage 3. For now, just note these values somewhere private (a notes app is fine). The "Goes to" column shows where each one ends up later.

**Which application do I open?** Go to **Applications → Applications**. You will see three entries:

| Entry | Type | Use it? |
|---|---|---|
| Default App | Generic | No. Auth0 creates it automatically. |
| DocShare API | Machine-to-Machine | No. Auth0 creates it automatically when you create the API, for the Test tab. It is not our login app. |
| **DocShare Web** | Regular Web Application | **Yes.** This is the app that logs users in, so its Domain, Client ID and Client Secret are the ones we need. |

Open **DocShare Web → Settings** and copy from the **Basic Information** section:

| Setting | Where to find it | Why we need it | Goes to |
|---|---|---|---|
| Tenant domain | DocShare Web → Settings → Domain | Tells the app where Auth0 lives (login page, token and key URLs) | appsettings |
| Web app client ID | DocShare Web → Settings → Client ID | Identifies our app to Auth0 (public, not a secret) | appsettings |
| Web app client secret | DocShare Web → Settings → Client Secret | Proves to Auth0 that the request really comes from our server | user-secrets only |
| API audience | `https://docshare-api` (the identifier you chose in Step 2, not found in this app) | Says which API the access token is for | appsettings |
| API permissions | `read:documents`, `write:documents`, `share:documents` | Used in policies in Stage 3 | series-bible A8 |

Domain and Client ID are not secrets, so they are safe to show. The Client Secret never goes in the repo or in chat.

Preview of the `appsettings.json` we create in Stage 3 (the secret is set with `dotnet user-secrets`):
```json
{
  "Auth0": {
    "Domain": "<your-tenant>.auth0.com",
    "Audience": "https://docshare-api",
    "ClientId": "<client-id>"
  }
}
```

Example with the real, non-secret values from our demo tenant. Use your own:
```json
{
  "Auth0": {
    "Domain": "dev-ev6nkpg558gup6nd.us.auth0.com",
    "Audience": "https://docshare-api",
    "ClientId": "3S8XGZYv3itsfBqbsfIOk0aWgG1r6QkO"
  }
}
```

### Step 8: Sanity check (no code yet)

**Why:** catch setup mistakes now, such as a wrong audience or a missing claim, before they look like code bugs in Stage 3.

1. On the API's **Test** tab, copy the sample token.
2. Paste it into jwt.io. Check that `aud` is `https://docshare-api` and `iss` is your tenant.
3. That token comes from a machine-to-machine app, so it only has permissions if you grant them to that app. Tokens for users with roles are tested in Stage 4, once login works.

### Checklist
- [ ] Tenant created, friendly name set to `authseries-dev`
- [ ] API created, RBAC and "Add Permissions in the Access Token" on
- [ ] 3 permissions and 3 roles created
- [ ] `DocShare Web` created with callback and logout URLs
- [ ] 3 fake users created and assigned roles
- [ ] Domain, audience and client ID noted; secret kept out of Git and chat
