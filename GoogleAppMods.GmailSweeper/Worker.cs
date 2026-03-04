using Cronos;
using Microsoft.Extensions.Options;

namespace GoogleAppMods.GmailSweeper;

public class Worker(
    ILogger<Worker> logger,
    GmailArchiveService archiveService,
    IOptions<GmailSweeperOptions> sweeperOptions,
    TimeProvider timeProvider) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        CronExpression schedule;
        try
        {
            schedule = CronExpression.Parse(sweeperOptions.Value.Schedule);
        }
        catch (CronFormatException ex)
        {
            logger.LogError(ex, "Invalid cron schedule configured for GmailSweeper: {Schedule}. Worker will stop.", sweeperOptions.Value.Schedule);
            return;
        }

        logger.LogInformation("GmailSweeper started with schedule: {Schedule}", sweeperOptions.Value.Schedule);

        while (!stoppingToken.IsCancellationRequested)
        {
            var utcNow = timeProvider.GetUtcNow();
            var nextOccurrence = schedule.GetNextOccurrence(utcNow.UtcDateTime);

            if (nextOccurrence is null)
            {
                logger.LogWarning("No next occurrence found for schedule: {Schedule}. Stopping.", sweeperOptions.Value.Schedule);
                break;
            }

            var delay = nextOccurrence.Value - utcNow;
            logger.LogInformation("Next sweep scheduled at {NextRun} UTC (in {Delay})", nextOccurrence.Value, delay);

            await Task.Delay(delay, timeProvider, stoppingToken);

            try
            {
                logger.LogInformation("GmailSweeper running scheduled sweep");
                await archiveService.RunAllQueriesAsync(stoppingToken);
                logger.LogInformation("GmailSweeper sweep completed successfully");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                logger.LogInformation("GmailSweeper was cancelled");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "GmailSweeper encountered an error during sweep");
            }
        }
    }
}
