using GoogleAppMods.Google;
using GoogleAppMods.GmailSweeper;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<GoogleProjectOptions>(
    builder.Configuration.GetSection(GoogleProjectOptions.SectionName));
builder.Services.AddOptions<GmailSweeperOptions>()
    .BindConfiguration(GmailSweeperOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<GoogleTokenProvider>();
builder.Services.AddSingleton<GmailArchiveService>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
