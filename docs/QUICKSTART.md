# Quick Start - Docker Deployment

## Prerequisites
1. Docker and Docker Compose installed
2. Google OAuth credentials from [Google Cloud Console](https://console.cloud.google.com/)

## Deploy in 5 Steps

### 1. Clone or Download Files
```bash
# Option A: Clone repository
git clone https://github.com/behm/google-app-mods.git
cd google-app-mods

# Option B: Download just the deployment files
wget https://raw.githubusercontent.com/behm/google-app-mods/main/docker-compose.yml
wget https://raw.githubusercontent.com/behm/google-app-mods/main/.env.example
```

### 2. Configure Environment
```bash
cp .env.example .env
nano .env  # or use any text editor
```

Add your credentials:
```env
GOOGLE_CLIENT_ID=your-client-id.apps.googleusercontent.com
GOOGLE_CLIENT_SECRET=your-client-secret
```

### 3. Authenticate with GitHub Container Registry
```bash
# Create a GitHub Personal Access Token with read:packages scope
# Then login:
docker login ghcr.io -u YOUR_GITHUB_USERNAME
```

### 4. Start Services
```bash
docker-compose up -d
```

### 5. Complete OAuth Setup
Open your browser:
```
http://localhost:8080/api/auth/google/authorize
```

Complete the Google OAuth consent flow. Done! 🎉

## Verify Deployment
```bash
# Check all containers are running
docker-compose ps

# View logs
docker-compose logs -f

# Check specific service
docker-compose logs -f gmail-sweeper
```

## Common Commands

### View Logs
```bash
docker-compose logs -f              # All services
docker-compose logs -f server       # Server only
docker-compose logs -f gmail-sweeper # Worker only
```

### Restart Services
```bash
docker-compose restart              # All services
docker-compose restart server       # Server only
```

### Update to Latest Version
```bash
docker-compose pull                 # Pull latest images
docker-compose up -d                # Restart with new images
```

### Stop Services
```bash
docker-compose down                 # Stop and remove containers
docker-compose down -v              # Also remove volumes
```

## Configuration Reference

### Gmail Sweeper Settings

Control what emails get archived:

```env
# Run daily at noon UTC
GMAIL_SWEEPER_SCHEDULE=0 12 * * *

# Archive promotions older than 14 days
GMAIL_SWEEPER_QUERY=in:inbox category:promotions -is:pinned -is:starred older_than:14d

# Process 100 emails per batch
GMAIL_SWEEPER_BATCH_SIZE=100
```

### Common Cron Schedules

| Schedule | Runs |
|----------|------|
| `0 6 * * *` | Daily at 6 AM UTC |
| `0 */6 * * *` | Every 6 hours |
| `0 0,12 * * *` | Twice daily (midnight & noon UTC) |
| `0 6 * * 1` | Every Monday at 6 AM UTC |

### Common Gmail Queries

| Query | Effect |
|-------|--------|
| `category:promotions older_than:14d` | Archive old promotions |
| `from:noreply older_than:30d` | Archive old no-reply emails |
| `category:social -is:starred older_than:7d` | Archive unstarred social emails |
| `larger:10M older_than:90d` | Archive large old emails |

## Troubleshooting

### Containers Won't Start
```bash
# Check logs for errors
docker-compose logs

# Verify .env file exists and has correct values
cat .env
```

### OAuth Token Error
Navigate to the authorization URL and complete OAuth flow:
```
http://localhost:8080/api/auth/google/authorize
```

Then restart workers:
```bash
docker-compose restart gmail-sweeper youtube-cleanup
```

### Port Already in Use
Edit `docker-compose.yml` and change the port:
```yaml
ports:
  - "8081:8080"  # Change 8081 to any available port
```

## Need More Help?

- **Full Documentation**: [docs/deployment.md](deployment.md)
- **OAuth Setup**: [docs/auth.md](auth.md)
- **Main README**: [README.md](../README.md)
- **Report Issues**: [GitHub Issues](https://github.com/behm/google-app-mods/issues)
