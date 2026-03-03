using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using GoogleAppMods.Google;
using Microsoft.Extensions.Options;

namespace GoogleAppMods.GmailSweeper;

public class GmailArchiveService(
    GoogleTokenProvider tokenProvider,
    IOptions<GmailSweeperOptions> sweeperOptions,
    ILogger<GmailArchiveService> logger)
{
    private const string UserId = "me";
    private const string InboxLabel = "INBOX";

    public async Task RunAllQueriesAsync(CancellationToken cancellationToken)
    {
        var options = sweeperOptions.Value;

        if (options.Queries.Count == 0)
        {
            logger.LogWarning("No Gmail sweep queries configured. Skipping.");
            return;
        }

        var credential = await tokenProvider.GetCredentialAsync(cancellationToken);

        using var gmailService = new GmailService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "GoogleAppMods.GmailSweeper"
        });

        foreach (var query in options.Queries)
        {
            await RunQueryAndArchiveAsync(gmailService, query, options.BatchSize, cancellationToken);
        }
    }

    private async Task RunQueryAndArchiveAsync(
        GmailService gmailService,
        string query,
        int batchSize,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Running Gmail query: {Query}", query);

        var messageIds = await GetAllMatchingMessageIdsAsync(gmailService, query, cancellationToken);

        if (messageIds.Count == 0)
        {
            logger.LogInformation("No messages found for query: {Query}", query);
            return;
        }

        logger.LogInformation("Found {Count} messages for query: {Query}. Archiving...", messageIds.Count, query);

        // Archive in batches using batchModify
        foreach (var batch in messageIds.Chunk(batchSize))
        {
            var batchModifyRequest = new BatchModifyMessagesRequest
            {
                Ids = batch.ToList(),
                RemoveLabelIds = [InboxLabel]
            };

            await gmailService.Users.Messages.BatchModify(batchModifyRequest, UserId).ExecuteAsync(cancellationToken);

            logger.LogInformation("Archived batch of {Count} messages", batch.Length);
        }

        logger.LogInformation("Finished archiving {Count} messages for query: {Query}", messageIds.Count, query);
    }

    private static async Task<List<string>> GetAllMatchingMessageIdsAsync(
        GmailService gmailService,
        string query,
        CancellationToken cancellationToken)
    {
        var messageIds = new List<string>();
        string? pageToken = null;

        do
        {
            var request = gmailService.Users.Messages.List(UserId);
            request.Q = query;
            request.PageToken = pageToken;
            request.MaxResults = 500;

            var response = await request.ExecuteAsync(cancellationToken);

            if (response.Messages is not null)
            {
                messageIds.AddRange(response.Messages.Select(m => m.Id));
            }

            pageToken = response.NextPageToken;
        }
        while (pageToken is not null);

        return messageIds;
    }
}
