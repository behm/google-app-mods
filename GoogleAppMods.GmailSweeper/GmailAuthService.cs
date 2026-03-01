using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Util.Store;
using Microsoft.Extensions.Options;

namespace GoogleAppMods.GmailSweeper;

public class GmailAuthService(IOptions<GoogleProjectOptions> googleProjectOptions, ILogger<GmailAuthService> logger)
{
    private static readonly string[] Scopes = [GmailService.Scope.GmailModify];

    public async Task<UserCredential> AuthorizeAsync(CancellationToken cancellationToken)
    {
        var options = googleProjectOptions.Value;

        logger.LogInformation("Authorizing with Google using token store at: {TokenStorePath}", options.TokenStorePath);

        var clientSecrets = new ClientSecrets
        {
            ClientId = options.ClientId,
            ClientSecret = options.ClientSecret
        };

        var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
            clientSecrets,
            Scopes,
            "user",
            cancellationToken,
            new FileDataStore(options.TokenStorePath, fullPath: false));

        logger.LogInformation("Google authorization successful");

        return credential;
    }
}
