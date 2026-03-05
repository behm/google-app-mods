# Build .NET application
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy project files and restore
COPY ["GoogleAppMods.YouTubeWatchTheseCleanup/GoogleAppMods.YouTubeWatchTheseCleanup.csproj", "GoogleAppMods.YouTubeWatchTheseCleanup/"]
COPY ["GoogleAppMods.Google/GoogleAppMods.Google.csproj", "GoogleAppMods.Google/"]
RUN dotnet restore "GoogleAppMods.YouTubeWatchTheseCleanup/GoogleAppMods.YouTubeWatchTheseCleanup.csproj"

# Copy source and build
COPY GoogleAppMods.YouTubeWatchTheseCleanup/ GoogleAppMods.YouTubeWatchTheseCleanup/
COPY GoogleAppMods.Google/ GoogleAppMods.Google/
WORKDIR "/src/GoogleAppMods.YouTubeWatchTheseCleanup"
RUN dotnet build "GoogleAppMods.YouTubeWatchTheseCleanup.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Publish
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "GoogleAppMods.YouTubeWatchTheseCleanup.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Final stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Install dependencies for Google API client
RUN apt-get update && apt-get install -y \
    ca-certificates \
    && rm -rf /var/lib/apt/lists/*

# Copy published app
COPY --from=publish /app/publish .

# Set environment
ENV DOTNET_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "GoogleAppMods.YouTubeWatchTheseCleanup.dll"]
