
## Endpoints

All endpoints are grouped under `/api/auth/google` and mapped by `GoogleAuthEndpoints.MapGoogleAuthEndpoints()`.

| Endpoint | Method | Description |
|---|---|---|
| `/api/auth/google/status` | `GET` | Returns whether a valid token exists, whether it includes a refresh token, when it was issued, and its scopes. |
| `/api/auth/google/authorize` | `GET` | Generates a PKCE code verifier/challenge pair, stores the verifier in an `HttpOnly` cookie, and redirects the browser to Google's consent screen. |
| `/api/auth/google/callback` | `GET` | Google redirects here after consent. Exchanges the authorization code (along with the PKCE code verifier) for an access + refresh token, then persists the token to the shared file store. |
| `/api/auth/google/revoke` | `POST` | Deletes the stored token from disk. A new authorization flow is required afterward. |

You can exercise these endpoints with the `auth.http` file in the Server project.

## PKCE (Proof Key for Code Exchange)

### What is PKCE?

PKCE ([RFC 7636](https://datatracker.ietf.org/doc/html/rfc7636)) is an extension to the OAuth 2.0 Authorization Code flow that prevents authorization code interception and injection attacks. It works by binding the initial authorization request to the subsequent token exchange request using a cryptographic proof.

### Why use PKCE with a Web (confidential) client?

Even though the application uses a **Web Application** OAuth client type with a client secret, PKCE is still recommended per [OAuth 2.0 Security Best Current Practice](https://datatracker.ietf.org/doc/html/draft-ietf-oauth-security-topics) and the OAuth 2.1 draft. The client secret proves the *application's* identity, but PKCE proves the *session's* identity. Specifically:

| Threat | Client secret alone | Client secret + PKCE |
|---|---|---|
| **Authorization code interception** (attacker steals the code in transit) | ✅ Mitigated — attacker still needs the secret | ✅ Mitigated — double protection |
| **Authorization code injection** (attacker tricks the server into exchanging a code from a different session) | ❌ **Not mitigated** — the server has the secret and will exchange the injected code | ✅ **Mitigated** — the code verifier is bound to the session that started the flow |

### How it works in this application

1. **`/authorize`** — The server generates a cryptographically random `code_verifier` (32 bytes, base64url-encoded) and derives a `code_challenge` by SHA-256 hashing it. The challenge is appended to the Google authorization URL. The verifier is stored in a short-lived, `HttpOnly` cookie scoped to `/api/auth/google`.

2. **`/callback`** — When Google redirects back with the authorization code, the server reads the `code_verifier` from the cookie and sends it alongside the code in the token exchange request. Google verifies that `SHA256(code_verifier) == code_challenge` from step 1 before issuing the token.

3. **Cookie cleanup** — The PKCE cookie is deleted immediately after use, regardless of success or failure.

If the cookie is missing (e.g., it expired after 10 minutes, or the user started the flow in a different browser), the callback returns a `400 Bad Request` instructing the user to restart the authorization flow.

## Token Storage & Sharing

All services share a single token via `FileDataStore` at the path configured in `GoogleProject:TokenStorePath` (relative to `AppData`). The architecture works as follows:

- **`GoogleAppMods.Server`** is the only project that *writes* the token (during the OAuth callback).
- **Worker services** (`GmailSweeper`, `YouTubeWatchTheseCleanup`) *read* the token via `GoogleTokenProvider`, which also handles automatic refresh when the token is stale.
- If no token is found on disk, `GoogleTokenProvider` throws an `InvalidOperationException` with a message directing the user to authorize via the web UI.

This design means the workers never need browser access — they run headlessly and rely on the server having completed the OAuth flow at least once.

## Scopes

All OAuth scopes are declared in `GoogleScopes.All`:

| Scope | API |
|---|---|
| `https://www.googleapis.com/auth/gmail.modify` | Gmail API |
| `https://www.googleapis.com/auth/youtube` | YouTube Data API v3 |

When adding a new Google API integration, add its scope to `GoogleScopes.All` and re-authorize through the web UI.

## Localhost Redirect URI Handling

Google OAuth only accepts `localhost` as a valid loopback redirect hostname. Because Aspire's DCP proxy uses subdomains like `server-app.dev.localhost`, the `GetRedirectUri()` helper rewrites any `*.localhost` host to plain `localhost` before building the redirect URI. This ensures the redirect URI sent to Google matches the one registered in the Google Cloud Console.

## Configuration Reference

| Setting | Description |
|---|---|
| `GoogleProject:ClientId` | OAuth 2.0 Client ID from Google Cloud Console (store in user secrets). |
| `GoogleProject:ClientSecret` | OAuth 2.0 Client Secret (store in user secrets). |
| `GoogleProject:TokenStorePath` | Relative path under `AppData` where the token is persisted. Shared by all services. |

See the [README](../README.md#configuration) for full setup instructions.