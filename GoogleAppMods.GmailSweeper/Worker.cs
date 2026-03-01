using Microsoft.Extensions.Options;

namespace GoogleAppMods.GmailSweeper;

public class Worker(ILogger<Worker> logger, IOptions<GoogleProjectOptions> googleProjectOptions) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var tokenStorePath = googleProjectOptions.Value.TokenStorePath;

        while (!stoppingToken.IsCancellationRequested)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Worker running at: {time}, TokenStorePath: {path}", DateTimeOffset.Now, tokenStorePath);
            }
            await Task.Delay(1000, stoppingToken);
        }
    }
}
