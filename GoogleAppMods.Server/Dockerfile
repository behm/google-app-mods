# Build frontend
FROM node:22-slim AS frontend-build
WORKDIR /frontend
COPY frontend/package*.json ./
RUN npm install
COPY frontend/ ./
RUN npm run build

# Build .NET application
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy project files and restore
COPY ["GoogleAppMods.Server/GoogleAppMods.Server.csproj", "GoogleAppMods.Server/"]
COPY ["GoogleAppMods.Google/GoogleAppMods.Google.csproj", "GoogleAppMods.Google/"]
RUN dotnet restore "GoogleAppMods.Server/GoogleAppMods.Server.csproj"

# Copy source and build
COPY GoogleAppMods.Server/ GoogleAppMods.Server/
COPY GoogleAppMods.Google/ GoogleAppMods.Google/
WORKDIR "/src/GoogleAppMods.Server"
RUN dotnet build "GoogleAppMods.Server.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Publish
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "GoogleAppMods.Server.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Final stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Install dependencies for Google API client
RUN apt-get update && apt-get install -y \
    ca-certificates \
    && rm -rf /var/lib/apt/lists/*

EXPOSE 8080
EXPOSE 8081

# Copy published app
COPY --from=publish /app/publish .

# Copy frontend build
COPY --from=frontend-build /frontend/dist ./wwwroot

# Set environment
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "GoogleAppMods.Server.dll"]
