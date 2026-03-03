using System.Security.Cryptography;
using System.Text;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Requests;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Util;
using Google.Apis.Util.Store;
using GoogleAppMods.Google;
using Microsoft.Extensions.Options;

namespace GoogleAppMods.Server;

public static class GoogleAuthEndpoints
{
    private const string TokenUser = "user";
    private const string CallbackPath = "/api/auth/google/callback";
    private const string PkceCookieName = "pkce_code_verifier";

    public static RouteGroupBuilder MapGoogleAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/auth/google");

        group.MapGet("/status", async (IOptions<GoogleProjectOptions> options) =>
        {
            var dataStore = new FileDataStore(options.Value.TokenStorePath, fullPath: false);
            var token = await dataStore.GetAsync<TokenResponse>(TokenUser);

            return Results.Ok(new
            {
                IsAuthorized = token is not null,
                HasRefreshToken = token?.RefreshToken is not null,
                IssuedUtc = token?.IssuedUtc,
                Scopes = token?.Scope
            });
        })
        .WithName("GoogleAuthStatus");

        group.MapGet("/authorize", (HttpContext context, IOptions<GoogleProjectOptions> options, ILogger<GoogleTokenProvider> logger) =>
        {
            var googleOptions = options.Value;
            var redirectUri = GetRedirectUri(context);

            logger.LogInformation("OAuth authorize: redirect_uri={RedirectUri}", redirectUri);

            var flow = CreateFlow(googleOptions);
            var authUri = (GoogleAuthorizationCodeRequestUrl)flow.CreateAuthorizationCodeRequest(redirectUri);
            authUri.Scope = string.Join(" ", GoogleScopes.All);
            authUri.AccessType = "offline";
            authUri.Prompt = "consent";

            // PKCE: generate code_verifier and code_challenge
            var codeVerifier = GenerateCodeVerifier();
            var codeChallenge = GenerateCodeChallenge(codeVerifier);

            var authUrl = authUri.Build().AbsoluteUri
                + "&code_challenge=" + Uri.EscapeDataString(codeChallenge)
                + "&code_challenge_method=S256";

            var isHttps = string.Equals(context.Request.Scheme, "https", StringComparison.OrdinalIgnoreCase);
            context.Response.Cookies.Append(PkceCookieName, codeVerifier, new CookieOptions
            {
                HttpOnly = true,
                Secure = isHttps,
                SameSite = isHttps ? SameSiteMode.Lax : SameSiteMode.Strict,
                MaxAge = TimeSpan.FromMinutes(10),
                Path = "/api/auth/google"
            });

            return Results.Redirect(authUrl);
        })
        .WithName("GoogleAuthAuthorize");

        group.MapGet("/callback", async (
            HttpContext context,
            IOptions<GoogleProjectOptions> options,
            ILogger<GoogleTokenProvider> logger,
            string? code,
            string? error) =>
        {
            if (!string.IsNullOrEmpty(error))
            {
                logger.LogError("Google OAuth error: {Error}", error);
                return Results.BadRequest(new { Error = error });
            }

            if (string.IsNullOrEmpty(code))
            {
                return Results.BadRequest(new { Error = "No authorization code received." });
            }

            var codeVerifier = context.Request.Cookies[PkceCookieName];
            if (string.IsNullOrEmpty(codeVerifier))
            {
                return Results.BadRequest(new { Error = "Missing PKCE code verifier. Please restart the authorization flow." });
            }

            context.Response.Cookies.Delete(PkceCookieName, new CookieOptions { Path = "/api/auth/google" });

            var googleOptions = options.Value;
            var redirectUri = GetRedirectUri(context);

            var flow = CreateFlow(googleOptions);
            var tokenRequest = new PkceAuthorizationCodeTokenRequest
            {
                Code = code,
                RedirectUri = redirectUri,
                CodeVerifier = codeVerifier
            };
            var tokenResponse = await flow.FetchTokenAsync(
                TokenUser, tokenRequest, CancellationToken.None);

            // FetchTokenAsync exchanges the code but does not always persist the
            // token to the DataStore. Store it explicitly so every consumer
            // (status endpoint, worker services) can find it on disk.
            var dataStore = new FileDataStore(googleOptions.TokenStorePath, fullPath: false);
            await dataStore.StoreAsync(TokenUser, tokenResponse);

            logger.LogInformation("Google OAuth token acquired and stored at {Path}", dataStore.FolderPath);

            return Results.Ok(new
            {
                Message = "Authorization successful. All services can now access Google APIs.",
                Scopes = tokenResponse.Scope
            });
        })
        .WithName("GoogleAuthCallback");

        group.MapPost("/revoke", async (IOptions<GoogleProjectOptions> options, ILogger<GoogleTokenProvider> logger) =>
        {
            var dataStore = new FileDataStore(options.Value.TokenStorePath, fullPath: false);
            await dataStore.DeleteAsync<TokenResponse>(TokenUser);

            logger.LogInformation("Google OAuth token revoked");

            return Results.Ok(new { Message = "Token revoked. Re-authorization required." });
        })
        .WithName("GoogleAuthRevoke");

        return group;
    }

    private static GoogleAuthorizationCodeFlow CreateFlow(GoogleProjectOptions options)
    {
        return new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
        {
            ClientSecrets = new ClientSecrets
            {
                ClientId = options.ClientId,
                ClientSecret = options.ClientSecret
            },
            Scopes = GoogleScopes.All,
            DataStore = new FileDataStore(options.TokenStorePath, fullPath: false)
        });
    }

    private static string GetRedirectUri(HttpContext context)
    {
        var request = context.Request;
        var host = request.Host.Host;

        // Aspire's DCP proxy uses subdomains of .localhost (e.g., server-app.dev.localhost).
        // Google OAuth only accepts "localhost" as a valid loopback hostname.
        if (host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase))
            host = "localhost";

        var authority = request.Host.Port.HasValue ? $"{host}:{request.Host.Port}" : host;
        return $"{request.Scheme}://{authority}{CallbackPath}";
    }

    private static string GenerateCodeVerifier()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Base64UrlEncode(bytes);
    }

    private static string GenerateCodeChallenge(string codeVerifier)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        return Base64UrlEncode(hash);
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private sealed class PkceAuthorizationCodeTokenRequest : AuthorizationCodeTokenRequest
    {
        [RequestParameter("code_verifier")]
        public string? CodeVerifier { get; set; }
    }
}
