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
| DocShare example | Alice logs in | Alice may `read:documents` but not share |

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
1. Sign up at auth0.com (free plan).
2. Name the tenant `authseries-dev`.
3. Pick your nearest region.

### Step 2: Create the API
1. **Applications → APIs → Create API**.
2. Name: `DocShare API`.
3. Identifier: `https://docshare-api` (the audience, a label and not a real URL).
4. Signing algorithm: RS256. Click **Create**.
5. In the API's **Settings** tab, under RBAC Settings:
   - Turn on **Enable RBAC**.
   - Turn on **Add Permissions in the Access Token**. Without it the `permissions` claim will not appear.
   - Save.

### Step 3: Add permissions
In the API's **Permissions** tab:

| Permission | Description |
|---|---|
| `read:documents` | Read documents |
| `write:documents` | Create and edit documents |
| `share:documents` | Share documents |

### Step 4: Create roles
**User Management → Roles → Create Role**. For each role, open **Permissions → Add Permissions**, select `DocShare API` and tick the permissions.

| Role | Permissions |
|---|---|
| viewer | `read:documents` |
| editor | `read:documents`, `write:documents` |
| owner | `read:documents`, `write:documents`, `share:documents` |

### Step 5: Create the web application
1. **Applications → Applications → Create Application**.
2. Name: `DocShare Web`. Type: **Regular Web Applications**.
3. In **Settings**, replace `7001` with the HTTPS port from `launchSettings.json` once the project exists:

| Field | Value |
|---|---|
| Allowed Callback URLs | `https://localhost:7001/callback` |
| Allowed Logout URLs | `https://localhost:7001/` |
| Allowed Web Origins | `https://localhost:7001` |

4. Save. On the **Connections** tab, make sure Username-Password-Authentication is enabled.

### Step 6: Create fake users
**User Management → Users → Create User** (default database connection, synthetic emails only). On each user's **Roles** tab, click **Assign Roles**.

| User | Email | Role |
|---|---|---|
| Alice (Acme) | `alice@acme.test` | owner |
| Bob (Acme) | `bob@acme.test` | editor |
| Carol (Globex) | `carol@globex.test` | viewer |

### Step 7: Values to copy

| Setting | Where to find it | Goes to |
|---|---|---|
| Tenant domain | Application → Settings → Domain | appsettings |
| API audience | `https://docshare-api` | appsettings |
| API permissions | `read:documents`, `write:documents`, `share:documents` | series-bible A8 |
| Web app client ID | Application → Settings → Client ID | appsettings |
| Web app client secret | Application → Settings → Client Secret | user-secrets only |

Placeholder `appsettings.json` layout (the secret is set with `dotnet user-secrets`):
```json
{
  "Auth0": {
    "Domain": "<your-tenant>.auth0.com",
    "Audience": "https://docshare-api",
    "ClientId": "<client-id>"
  }
}
```

### Step 8: Sanity check (no code yet)
1. On the API's **Test** tab, copy the sample token.
2. Paste it into jwt.io. Check that `aud` is `https://docshare-api` and `iss` is your tenant.
3. That token comes from a machine-to-machine app, so it only has permissions if you grant them to that app. Tokens for users with roles are tested in Stage 4, once login works.

### Checklist
- [ ] Tenant `authseries-dev` exists
- [ ] API created, RBAC and "Add Permissions in the Access Token" on
- [ ] 3 permissions and 3 roles created
- [ ] `DocShare Web` created with callback and logout URLs
- [ ] 3 fake users created and assigned roles
- [ ] Domain, audience and client ID noted; secret kept out of Git and chat
