using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Util.Store;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GoogleAppMods.Google;

public class GoogleTokenProvider(
    IOptions<GoogleProjectOptions> googleProjectOptions,
    ILogger<GoogleTokenProvider> logger)
{
    private const string TokenUser = "user";

    public async Task<UserCredential> GetCredentialAsync(CancellationToken cancellationToken)
    {
        var options = googleProjectOptions.Value;
        var dataStore = new FileDataStore(options.TokenStorePath, fullPath: false);

        var tokenResponse = await dataStore.GetAsync<TokenResponse>(TokenUser);
        if (tokenResponse is null)
        {
            throw new InvalidOperationException(
                "No Google OAuth token found. Please authorize via the web UI first.");
        }

        var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
        {
            ClientSecrets = new ClientSecrets
            {
                ClientId = options.ClientId,
                ClientSecret = options.ClientSecret
            },
            Scopes = GoogleScopes.All,
            DataStore = dataStore
        });

        var credential = new UserCredential(flow, TokenUser, tokenResponse);

        if (credential.Token.IsStale)
        {
            logger.LogInformation("Google token is stale, refreshing...");
            if (!await credential.RefreshTokenAsync(cancellationToken))
            {
                throw new InvalidOperationException(
                    "Failed to refresh Google OAuth token. Please re-authorize via the web UI.");
            }
            logger.LogInformation("Google token refreshed successfully");
        }

        return credential;
    }
}
