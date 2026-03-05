# Deployment Guide

This guide covers deploying Google App Mods to your Synology NAS or any Docker-compatible environment.

## Overview

The application is deployed as Docker containers:
- **server** - ASP.NET Core API + React frontend + OAuth authentication
- **gmail-sweeper** - Background worker for Gmail archival
- **youtube-cleanup** - Background worker for YouTube automation
- **redis** - Cache for API responses

All container images are automatically built and published to GitHub Container Registry (GHCR) via GitHub Actions.

## Prerequisites

1. **Docker Environment**
   - Synology NAS with Container Manager (Docker)
   - Or any Docker-compatible host

2. **GitHub Container Registry Access**
   - Container images are published to `ghcr.io/behm/google-app-mods-*`
   - For private repositories, you'll need a GitHub Personal Access Token (PAT) with `read:packages` scope
   - See: [GitHub Container Registry Authentication](https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-container-registry)

3. **Google OAuth Credentials**
   - Create a project in the [Google Cloud Console](https://console.cloud.google.com/)
   - Enable the Gmail API and YouTube Data API v3
   - Create OAuth 2.0 Client ID credentials (Web application type)
   - Add `http://your-nas-ip:8080/api/auth/google/callback` as an authorized redirect URI

## Deployment Options

### Option 1: Docker Compose (Recommended)

The easiest way to deploy on Synology NAS or any Docker host.

1. **Create deployment directory**
   ```bash
   mkdir -p ~/google-app-mods
   cd ~/google-app-mods
   ```

2. **Download docker-compose.yml**
   ```bash
   wget https://raw.githubusercontent.com/behm/google-app-mods/main/docker-compose.yml
   ```

3. **Create .env file**
   ```bash
   cp .env.example .env
   # Edit .env with your credentials
   nano .env
   ```

   Required environment variables:
   ```env
   GOOGLE_CLIENT_ID=your-client-id.apps.googleusercontent.com
   GOOGLE_CLIENT_SECRET=your-client-secret

   # Optional - Gmail Sweeper Configuration
   GMAIL_SWEEPER_SCHEDULE=0 12 * * *  # Daily at noon UTC
   GMAIL_SWEEPER_QUERY=in:inbox category:promotions -is:pinned -is:starred older_than:14d
   GMAIL_SWEEPER_BATCH_SIZE=100
   ```

4. **Authenticate with GitHub Container Registry** (if private)
   ```bash
   docker login ghcr.io -u YOUR_GITHUB_USERNAME
   # Enter your GitHub Personal Access Token when prompted
   ```

5. **Start the application**
   ```bash
   docker-compose up -d
   ```

6. **Complete OAuth Setup**
   - Open http://your-nas-ip:8080 in your browser
   - Navigate to the authorization endpoint: http://your-nas-ip:8080/api/auth/google/authorize
   - Complete the Google OAuth consent flow
   - The token will be stored in the shared volume and accessible to all workers

7. **Verify deployment**
   ```bash
   docker-compose ps
   docker-compose logs -f
   ```

### Option 2: Synology Container Manager (GUI)

For users who prefer the Synology DSM Container Manager interface:

1. **Enable SSH and connect to your Synology NAS**
   ```bash
   ssh admin@your-nas-ip
   ```

2. **Create directories for volumes**
   ```bash
   mkdir -p /volume1/docker/google-app-mods/tokens
   mkdir -p /volume1/docker/google-app-mods/redis-data
   ```

3. **Open Container Manager in DSM**

4. **Registry Settings**
   - Go to Registry → Settings
   - Add GitHub Container Registry: `ghcr.io`
   - If private, add authentication credentials

5. **Pull Images**
   - Registry → Search for: `ghcr.io/behm/google-app-mods-server`
   - Download: `latest` tag
   - Repeat for:
     - `ghcr.io/behm/google-app-mods-gmail-sweeper`
     - `ghcr.io/behm/google-app-mods-youtube-cleanup`
   - Also download: `redis:7-alpine`

6. **Create Network**
   - Network → Add → Bridge network
   - Name: `google-app-mods`

7. **Create Redis Container**
   - Container → Create
   - Image: `redis:7-alpine`
   - Name: `google-app-mods-redis`
   - Network: `google-app-mods`
   - Volume: Mount `/volume1/docker/google-app-mods/redis-data` to `/data`
   - Auto-restart: Yes

8. **Create Server Container**
   - Container → Create
   - Image: `ghcr.io/behm/google-app-mods-server:latest`
   - Name: `google-app-mods-server`
   - Network: `google-app-mods`
   - Port Mapping: 8080 → 8080
   - Volume: Mount `/volume1/docker/google-app-mods/tokens` to `/app/tokens`
   - Environment Variables:
     ```
     ASPNETCORE_ENVIRONMENT=Production
     ASPNETCORE_URLS=http://+:8080
     ConnectionStrings__cache=google-app-mods-redis:6379
     GoogleProject__ClientId=your-client-id
     GoogleProject__ClientSecret=your-client-secret
     GoogleProject__TokenStorePath=/app/tokens
     ```
   - Links: redis → google-app-mods-redis
   - Auto-restart: Yes

9. **Create Worker Containers** (repeat for gmail-sweeper and youtube-cleanup)
   - Container → Create
   - Image: `ghcr.io/behm/google-app-mods-gmail-sweeper:latest`
   - Name: `google-app-mods-gmail-sweeper`
   - Network: `google-app-mods`
   - Volume: Mount `/volume1/docker/google-app-mods/tokens` to `/app/tokens`
   - Environment Variables:
     ```
     DOTNET_ENVIRONMENT=Production
     GoogleProject__ClientId=your-client-id
     GoogleProject__ClientSecret=your-client-secret
     GoogleProject__TokenStorePath=/app/tokens
     GmailSweeper__Schedule=0 12 * * *
     GmailSweeper__Queries__0=in:inbox category:promotions -is:pinned -is:starred older_than:14d
     GmailSweeper__BatchSize=100
     ```
   - Auto-restart: Yes

10. **Complete OAuth Setup**
    - Navigate to: http://your-nas-ip:8080/api/auth/google/authorize
    - Complete Google OAuth consent
    - Restart worker containers to pick up the new token

### Option 3: Manual Docker Commands

For direct Docker CLI usage:

```bash
# Create network
docker network create google-app-mods

# Create volume
docker volume create google-tokens
docker volume create redis-data

# Start Redis
docker run -d \
  --name google-app-mods-redis \
  --network google-app-mods \
  -v redis-data:/data \
  --restart unless-stopped \
  redis:7-alpine

# Start Server
docker run -d \
  --name google-app-mods-server \
  --network google-app-mods \
  -p 8080:8080 \
  -v google-tokens:/app/tokens \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e ASPNETCORE_URLS=http://+:8080 \
  -e ConnectionStrings__cache=google-app-mods-redis:6379 \
  -e GoogleProject__ClientId=your-client-id \
  -e GoogleProject__ClientSecret=your-client-secret \
  -e GoogleProject__TokenStorePath=/app/tokens \
  --restart unless-stopped \
  ghcr.io/behm/google-app-mods-server:latest

# Start Gmail Sweeper
docker run -d \
  --name google-app-mods-gmail-sweeper \
  --network google-app-mods \
  -v google-tokens:/app/tokens \
  -e DOTNET_ENVIRONMENT=Production \
  -e GoogleProject__ClientId=your-client-id \
  -e GoogleProject__ClientSecret=your-client-secret \
  -e GoogleProject__TokenStorePath=/app/tokens \
  -e GmailSweeper__Schedule="0 12 * * *" \
  -e 'GmailSweeper__Queries__0=in:inbox category:promotions -is:pinned -is:starred older_than:14d' \
  -e GmailSweeper__BatchSize=100 \
  --restart unless-stopped \
  ghcr.io/behm/google-app-mods-gmail-sweeper:latest

# Start YouTube Cleanup
docker run -d \
  --name google-app-mods-youtube-cleanup \
  --network google-app-mods \
  -v google-tokens:/app/tokens \
  -e DOTNET_ENVIRONMENT=Production \
  -e GoogleProject__ClientId=your-client-id \
  -e GoogleProject__ClientSecret=your-client-secret \
  -e GoogleProject__TokenStorePath=/app/tokens \
  --restart unless-stopped \
  ghcr.io/behm/google-app-mods-youtube-cleanup:latest
```

## CI/CD Pipeline

The GitHub Actions workflow automatically builds and publishes container images:

- **Trigger**: Push to `main` branch, tags (e.g., `v1.0.0`), or manual workflow dispatch
- **Registry**: GitHub Container Registry (`ghcr.io`)
- **Images Built**:
  - `ghcr.io/behm/google-app-mods-server:latest`
  - `ghcr.io/behm/google-app-mods-gmail-sweeper:latest`
  - `ghcr.io/behm/google-app-mods-youtube-cleanup:latest`
- **Platforms**: `linux/amd64`, `linux/arm64` (supports Synology x86 and ARM NAS models)

### Workflow Features

- Multi-platform builds (AMD64 and ARM64)
- Layer caching for faster builds
- Semantic versioning tags (on git tags)
- Automatic `latest` tag on main branch
- Build matrix for parallel image creation

### Monitoring Build Status

Check the [Actions tab](../../actions) in GitHub to monitor builds.

## Configuration

### Environment Variables

All configuration is done via environment variables following the .NET configuration pattern.

#### Server Configuration
| Variable | Description | Default |
|----------|-------------|---------|
| `ASPNETCORE_ENVIRONMENT` | ASP.NET environment | `Production` |
| `ASPNETCORE_URLS` | Listening URLs | `http://+:8080` |
| `ConnectionStrings__cache` | Redis connection string | `redis:6379` |
| `GoogleProject__ClientId` | Google OAuth Client ID | **Required** |
| `GoogleProject__ClientSecret` | Google OAuth Client Secret | **Required** |
| `GoogleProject__TokenStorePath` | Token storage path | `/app/tokens` |

#### Gmail Sweeper Configuration
| Variable | Description | Default |
|----------|-------------|---------|
| `GmailSweeper__Schedule` | Cron expression (UTC) | `0 12 * * *` |
| `GmailSweeper__Queries__0` | Gmail search query | See below |
| `GmailSweeper__BatchSize` | Batch size for archiving | `100` |

Default query: `in:inbox category:promotions -is:pinned -is:starred older_than:14d`

To add multiple queries, use `GmailSweeper__Queries__1`, `GmailSweeper__Queries__2`, etc.

### Gmail Search Query Examples

| Query | Effect |
|-------|--------|
| `in:inbox category:promotions older_than:14d` | Archive promotions older than 14 days |
| `in:inbox from:noreply older_than:30d` | Archive no-reply emails older than 30 days |
| `in:inbox category:social -is:starred older_than:7d` | Archive unstarred social emails older than 7 days |
| `in:inbox larger:10M older_than:90d` | Archive large emails (>10MB) older than 90 days |

See [Gmail Search Operators](https://support.google.com/mail/answer/7190) for more options.

### Cron Schedule Examples

| Expression | Meaning |
|------------|---------|
| `0 6 * * *` | Daily at 6:00 AM UTC |
| `0 6 * * 1` | Every Monday at 6:00 AM UTC |
| `0 */6 * * *` | Every 6 hours |
| `0 8 1 * *` | 1st of each month at 8:00 AM UTC |
| `0 0,12 * * *` | Twice daily at midnight and noon UTC |

## Updating the Application

### Docker Compose
```bash
cd ~/google-app-mods
docker-compose pull
docker-compose up -d
```

### Synology Container Manager
1. Registry → Search for images
2. Download new `:latest` tags
3. Container → Stop each container
4. Action → Reset (uses new image)
5. Start containers

### Manual Docker
```bash
docker pull ghcr.io/behm/google-app-mods-server:latest
docker pull ghcr.io/behm/google-app-mods-gmail-sweeper:latest
docker pull ghcr.io/behm/google-app-mods-youtube-cleanup:latest

docker-compose restart
# Or restart individual containers
docker restart google-app-mods-server
docker restart google-app-mods-gmail-sweeper
docker restart google-app-mods-youtube-cleanup
```

## Troubleshooting

### Viewing Logs

**Docker Compose:**
```bash
docker-compose logs -f
docker-compose logs -f server
docker-compose logs -f gmail-sweeper
```

**Docker CLI:**
```bash
docker logs -f google-app-mods-server
docker logs -f google-app-mods-gmail-sweeper
```

**Synology Container Manager:**
- Container → Select container → Details → Log

### Common Issues

#### 1. OAuth Token Not Found
**Error**: "OAuth2 token not found"

**Solution**: Complete OAuth flow:
1. Navigate to: `http://your-nas-ip:8080/api/auth/google/authorize`
2. Authorize the application with Google
3. Restart worker containers

#### 2. Connection Refused to Redis
**Error**: "Connection refused" when connecting to Redis

**Solution**: Ensure Redis container is running and on the same network:
```bash
docker network inspect google-app-mods
```

#### 3. Port Already in Use
**Error**: "Port 8080 is already allocated"

**Solution**: Change the host port mapping in docker-compose.yml:
```yaml
ports:
  - "8081:8080"  # Change 8081 to any available port
```

#### 4. Permission Denied on Token Storage
**Error**: Permission errors accessing `/app/tokens`

**Solution**: Ensure the volume has correct permissions:
```bash
# Docker Compose
docker-compose down
docker volume rm google-app-mods_google-tokens
docker-compose up -d

# Or for Synology, set directory permissions
sudo chmod -R 777 /volume1/docker/google-app-mods/tokens
```

#### 5. Worker Not Processing
**Error**: Worker starts but doesn't process items

**Solution**:
1. Check token exists: `docker exec google-app-mods-gmail-sweeper ls -la /app/tokens`
2. Verify cron schedule is in UTC
3. Check logs for errors
4. Ensure worker can access the server for token refresh

## Backup and Restore

### Backup OAuth Token
```bash
# Docker Compose
docker run --rm -v google-app-mods_google-tokens:/tokens \
  -v $(pwd):/backup alpine \
  tar czf /backup/tokens-backup.tar.gz -C /tokens .

# Synology
tar czf tokens-backup.tar.gz -C /volume1/docker/google-app-mods/tokens .
```

### Restore OAuth Token
```bash
# Docker Compose
docker run --rm -v google-app-mods_google-tokens:/tokens \
  -v $(pwd):/backup alpine \
  tar xzf /backup/tokens-backup.tar.gz -C /tokens

# Synology
tar xzf tokens-backup.tar.gz -C /volume1/docker/google-app-mods/tokens
```

## Security Considerations

1. **Keep OAuth credentials secure** - Never commit them to version control
2. **Use HTTPS in production** - Set up a reverse proxy (e.g., Traefik, nginx)
3. **Restrict network access** - Use firewall rules to limit access to port 8080
4. **Regular updates** - Pull and deploy new images regularly for security patches
5. **Token storage** - The OAuth token volume should have restricted permissions

## Performance Tuning

### Gmail Sweeper Batch Size
Increase `GmailSweeper__BatchSize` for faster processing (max 1000):
```yaml
environment:
  - GmailSweeper__BatchSize=1000
```

### Redis Cache Configuration
For high-traffic scenarios, add Redis memory limit:
```yaml
redis:
  image: redis:7-alpine
  command: redis-server --maxmemory 256mb --maxmemory-policy allkeys-lru
```

## Support

For issues, questions, or contributions:
- **GitHub Issues**: [behm/google-app-mods/issues](../../issues)
- **Documentation**: [README.md](../README.md)
- **OAuth Setup Guide**: [docs/auth.md](auth.md)
