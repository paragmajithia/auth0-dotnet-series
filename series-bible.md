# Auth0 + .NET Series: Bible

Master copy lives in the Claude doc "Auth0 + .NET Series: Bible and Prompt Pack". This file is the working copy for Claude Code.

## A1. Goal
A 6-episode blog and YouTube series (paragmajithia.com) teaching modern authentication and authorization with Auth0 and .NET. One shared sample app grows across all episodes. Audience: developers who know basic OAuth.

## A2. Style rules for every answer
- Simple, structured explanations, no long theory.
- Real examples, one code snippet per concept, then an end-to-end example.
- Diagrams where useful. Readable on mobile.
- Tables for comparisons.
- Explain how each concept is implemented, not just what it is.
- Never ask me to paste secrets. Use placeholders.

## A3. Tech stack
| Item | Choice |
|---|---|
| IDE | JetBrains Rider (Community) |
| Runtime | .NET newest LTS (check with `dotnet --list-sdks`) |
| Identity | Auth0 (free tenant) |
| Fine-grained authz | OpenFGA (local Docker) or Auth0 FGA |
| MCP | Official C# MCP SDK |
| Run | Local machine first, GCP Cloud Run optional |
| Testing | curl or Postman, MCP Inspector |
| Source control | GitHub repo auth0-dotnet-series, one folder or branch per episode |

## A5. Sample app: DocShare
A small document-sharing API with synthetic data.

| Concept | Detail |
|---|---|
| Entities | Organization (tenant), User, Folder, Document |
| Actions | read, write, share, delete |
| Roles | viewer, editor, owner |
| Sample data | Fake orgs (Acme, Globex), fake users, fake documents |

No real company data, ever.

## A6. Repo structure
```
auth0-dotnet-series/
  series-bible.md
  AGENTS.md              # project rules (single source of truth)
  CLAUDE.md              # one line: @AGENTS.md (local only, gitignored)
  src/
    DocShare.Api/        # ASP.NET Core API (Ep 1+)
    DocShare.Web/        # simple login client (Ep 1, 2)
    DocShare.Mcp/        # MCP server (Ep 4+)
  tests/
  deploy/                # Dockerfile, Cloud Run notes
  docs/
    ep1/ ... ep6/        # README, blog draft, video script
```

## A7. Conventions
- Config in appsettings.json and user-secrets. Secrets never in Git.
- Auth0 names: tenant friendly name `authseries-dev` (the real tenant name is auto-generated and fixed), API audience `https://docshare-api`.
- Permissions named `read:documents`, `write:documents`, `share:documents`.
- Each episode adds code to the same repo, never a rewrite.

## A8. Auth0 settings table (names only, no secrets)
| Setting | Value |
|---|---|
| Tenant domain | dev-ev6nkpg558gup6nd.us.auth0.com |
| API audience | https://docshare-api |
| API permissions | read:documents, write:documents, share:documents |
| Web app client ID | 3S8XGZYv3itsfBqbsfIOk0aWgG1r6QkO |
| Organizations | |
| FGA store | |

## A9. Episodes
| Ep | Topic | Must cover | Status |
|---|---|---|---|
| 1 | AuthN vs AuthZ, OIDC, JWT, Auth0 + .NET basics | Login, JWT-protected API, permissions, policy-based authz | In progress: Stages 1-2 done, Stage 3 next |
| 2 | Multi-tenant with Auth0 Organizations | Per-tenant login, org claim in token, tenant isolation in API | Not started |
| 3 | Fine-grained authz with OpenFGA | Relationship model, check API, RBAC vs ReBAC | Not started |
| 4 | Secure a C# MCP server with Auth0 | OAuth 2.1 for MCP, discovery, client registration, scopes, MCP Inspector | Not started |
| 5 | OBO token exchange | MCP server swaps token to call DocShare API as the user | Not started |
| 6 | Token Vault + async approval | Third-party API calls on behalf of user, human approval step (check early-access status first) | Not started |

## A13. Stages (every episode follows the same 8 stages)
The stage number equals the prompt number in prompts.md (B1 to B8).

| Stage | Output | Where |
|---|---|---|
| 1. Concept | Simple explanation, diagram, real example | Chat |
| 2. Auth0 setup | Click-by-click dashboard steps and a table of values to copy | Chat |
| 3. Code | C# project, snippet per concept | Claude Code |
| 4. Run and test | Run steps, test requests, troubleshooting | Claude Code |
| 5. Deploy (optional) | Cloud Run steps with a local fallback | Claude Code |
| 6. Docs | README and blog post draft | Chat |
| 7. Video script | Script, titles, description, timestamps | Chat |
| 8. Handoff | 10-line summary pasted into A10 | Either |

## A10. Handoff log (append after each episode)
- Ep 1:
- Ep 2:
- Ep 3:
- Ep 4:
- Ep 5:
- Ep 6:
