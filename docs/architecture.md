# Architecture and implementation

Engineering standard for the CryptoTracker codebase: layer boundaries, HTTP conventions, authentication, and cross-cutting rules.

**Related documentation:** [Product specification](project.md) · [UI guide](ui.md) · [Documentation index](docs.md) · [README](../README.md)

---

## Overview

| Item | Value |
| ---- | ----- |
| **Stack** | ASP.NET Core 8, EF Core 8, SQLite (backend); React 18, TypeScript, Vite 6 (frontend) |
| **Default flow** | Controller → Service → Data (DbContext); DTOs at the API boundary |
| **API shape** | Envelope `{ success, message, data }` on all JSON responses; paginated lists as documented below |
| **Auth** | JWT bearer, `[Authorize]`, `ICurrentUser` from claims for user-scoped data |

---

## Principles

Prefer, in order: simplicity over complexity; consistency over one-off patterns; explicit behavior over implicit magic; maintainability over clever shortcuts; configuration over hardcoded values.

When multiple designs are valid, choose the simplest one that preserves consistency and leaves room to scale (pagination, configuration, clear layers).

---

## Layered architecture

### Default pattern

**Controller → Service → Data** (DbContext / persistence).

| Layer | Responsibility |
| ----- | -------------- |
| **Controller** | HTTP only: routing, status codes, binding to DTOs |
| **Service** | Validation, business rules, orchestration, mapping entity ↔ DTO |
| **Data** | Persistence and queries only |
| **DTO** | Public API contract; no leaking EF entities in responses |

### Repository layout

**Backend:** `Controllers`, `Services`, `Interfaces`, `Data`, `Models`, `DTOs`, `Middleware`, `Exceptions`, `Migrations`, `Properties`.

**Frontend:** `components`, `services`, `types`, `utils`, and `style.css` in the current flat layout. New code should follow the patterns in [docs/ui.md](ui.md).

---

## API conventions

- REST-style resources under `/api/...`; correct HTTP verbs; optional future versioning (`/api/v1/...`).
- Endpoint details and request bodies: [docs/project.md#api-reference](project.md#api-reference).

### Response envelope (required)

```json
{
  "success": true,
  "message": "string",
  "data": {}
}
```

- Do not return raw EF entities.
- Do not put business rules in controllers.
- Prefer idempotent operations where the domain allows it.
- List endpoints are pagination-ready.

On errors, `success` is `false` and `message` is a safe client-facing string. Validation failures return **400** with the same shape. Global exception middleware maps failures to this envelope without stack traces or internal details.

### Paginated lists

`GET /api/{resource}?page=1&pageSize=10`

```json
{
  "success": true,
  "message": "",
  "data": {
    "items": [],
    "page": 1,
    "pageSize": 10,
    "totalCount": 0,
    "totalPages": 0
  }
}
```

Implement paging with `Skip` / `Take` in the service layer.

### Filtering and search

Use query parameters; implement filters with conditional `IQueryable` (or equivalent) in the service layer.

### Caching

Use in-memory caching only for read-heavy, non-critical paths; short TTL (1–5 minutes); cache in services, not in controllers. Do not cache data that must be real-time.

### Logging

Structured logging at **Information** / **Warning** / **Error**. Log errors, important mutations, and outbound integration calls. Avoid noisy console output and never log secrets or raw tokens.

---

## Configuration

- Use `appsettings.json`, environment variables, and user secrets for deployment.
- No secrets or environment-specific URLs hardcoded in source.
- Runtime settings (JWT, CORS, portfolio filters, exchange integration): [README](../README.md#configuration).

---

## DTO mapping

Always map **Entity → DTO → response**. Never expose persistence models as the public JSON shape. JSON property names use **camelCase** so TypeScript types align with API payloads.

---

## Authentication and authorization

- **Registration and login** use a unique **username** (stored normalized). Password hashing and validation live in `AuthService`. Request and response shapes: [API reference](project.md#api-reference).
- **JWT bearer** access tokens for authenticated routes; validate issuer, audience, signing key, and lifetime via ASP.NET Core JWT bearer middleware.
- Use **`[Authorize]`** on endpoints that require a user; keep **`AuthController`** registration and login **`[AllowAnonymous]`**.
- Resolve the current user in a scoped service (`ICurrentUser`) backed by claims; scope all user-owned data in services by that id.
- Refresh tokens and role-based authorization are not implemented in the current release.

---

## Backend standards

- Constructor injection for all services; async I/O end-to-end; pass **`CancellationToken`** into async service methods.
- Validate request DTOs with annotations where useful; enforce business rules in services.
- One global exception pipeline; centralized logging configuration.
- Entities hold persistence fields only. Domain logic and mapping live in services.

---

## Frontend standards

- UI is presentation-only; the API is the source of business truth.
- Central **`services/api.ts`** for HTTP; unwrap the standard envelope; strong TypeScript types aligned with DTOs.
- Handle loading, error, and empty states for every user-initiated fetch.
- Auth, layout, and view patterns: [docs/ui.md](ui.md).

The backend owns authoritative data. The frontend consumes it and does not duplicate business rules.

---

## Testing

| Area | Expectation |
| ---- | ----------- |
| **Backend** | Unit tests for services; integration tests for HTTP endpoints |
| **Frontend** | Component tests and API contract tests as the suite grows |

Manual verification steps: [docs/project.md#testing](project.md#testing).

---

## Constraints

Avoid mixed responsibilities, hidden side effects, inconsistent folder or naming patterns, and unnecessary abstraction before a second use case exists.
