# Build .NET application
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy project files and restore
COPY ["GoogleAppMods.GmailSweeper/GoogleAppMods.GmailSweeper.csproj", "GoogleAppMods.GmailSweeper/"]
COPY ["GoogleAppMods.Google/GoogleAppMods.Google.csproj", "GoogleAppMods.Google/"]
RUN dotnet restore "GoogleAppMods.GmailSweeper/GoogleAppMods.GmailSweeper.csproj"

# Copy source and build
COPY GoogleAppMods.GmailSweeper/ GoogleAppMods.GmailSweeper/
COPY GoogleAppMods.Google/ GoogleAppMods.Google/
WORKDIR "/src/GoogleAppMods.GmailSweeper"
RUN dotnet build "GoogleAppMods.GmailSweeper.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Publish
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "GoogleAppMods.GmailSweeper.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

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

ENTRYPOINT ["dotnet", "GoogleAppMods.GmailSweeper.dll"]
