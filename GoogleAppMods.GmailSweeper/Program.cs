using GoogleAppMods.GmailSweeper;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<GoogleProjectOptions>(
    builder.Configuration.GetSection(GoogleProjectOptions.SectionName));

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
