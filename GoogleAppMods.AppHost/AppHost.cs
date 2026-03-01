using GoogleAppMods.AppHost;
using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

var googleProjectSection = builder.Configuration.GetSection("GoogleProject");
if (!googleProjectSection.GetChildren().Any())
    throw new InvalidOperationException("GoogleProject configuration section is not configured.");

var cache = builder.AddRedis("cache");

var server = builder.AddProject<Projects.GoogleAppMods_Server>("server")
    .WithReference(cache)
    .WaitFor(cache)
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints()
    .WithGoogleProjectConfig(googleProjectSection);

var webfrontend = builder.AddViteApp("webfrontend", "../frontend")
    .WithReference(server)
    .WaitFor(server);

server.PublishWithContainerFiles(webfrontend, "wwwroot");

builder.AddProject<Projects.GoogleAppMods_GmailSweeper>("googleappmods-gmailsweeper")
    .WithGoogleProjectConfig(googleProjectSection);

builder.AddProject<Projects.GoogleAppMods_YouTubeWatchTheseCleanup>("googleappmods-youtubewatchthesecleanup")
    .WithGoogleProjectConfig(googleProjectSection);

builder.Build().Run();
