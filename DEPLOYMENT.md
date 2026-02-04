# Deployment Guide

This guide covers deploying SaaSBase to various platforms and environments.

## 📋 Table of Contents

- [Prerequisites](#prerequisites)
- [Production Checklist](#production-checklist)
- [Docker Deployment](#docker-deployment)
- [Azure Deployment](#azure-deployment)
- [AWS Deployment](#aws-deployment)
- [Linux Server Deployment](#linux-server-deployment)
- [Environment Configuration](#environment-configuration)
- [Database Migration](#database-migration)
- [SSL/HTTPS Setup](#sslhttps-setup)
- [Monitoring & Logging](#monitoring--logging)

## Prerequisites

- Production-ready codebase
- Domain name configured
- SSL certificate
- Database server (PostgreSQL)
- Redis server
- Web server (Nginx/IIS)

## Production Checklist

### Security

- [ ] Change all default passwords
- [ ] Generate strong JWT secret key (64+ characters)
- [ ] Configure HTTPS/SSL
- [ ] Set up firewall rules
- [ ] Enable CORS properly
- [ ] Configure rate limiting
- [ ] Set up security headers
- [ ] Enable MFA for admin users
- [ ] Review and update password policies
- [ ] Remove development endpoints

### Configuration

- [ ] Update connection strings
- [ ] Configure email service (SendGrid)
- [ ] Configure SMS service (if needed)
- [ ] Set production environment variables
- [ ] Configure logging
- [ ] Set up monitoring
- [ ] Configure backup strategy

### Performance

- [ ] Enable response caching
- [ ] Configure Redis caching
- [ ] Set up CDN (if needed)
- [ ] Optimize database indexes
- [ ] Configure connection pooling
- [ ] Enable compression

## Docker Deployment

### Backend Dockerfile

Create `SaaSBase.Api/Dockerfile`:

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["SaaSBase.Api/SaaSBase.Api.csproj", "SaaSBase.Api/"]
COPY ["SaaSBase.Application/SaaSBase.Application.csproj", "SaaSBase.Application/"]
COPY ["SaaSBase.Domain/SaaSBase.Domain.csproj", "SaaSBase.Domain/"]
COPY ["SaaSBase.Infrastructure/SaaSBase.Infrastructure.csproj", "SaaSBase.Infrastructure/"]
RUN dotnet restore "SaaSBase.Api/SaaSBase.Api.csproj"
COPY . .
WORKDIR "/src/SaaSBase.Api"
RUN dotnet build "SaaSBase.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "SaaSBase.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SaaSBase.Api.dll"]
```

### Frontend Dockerfile

Create `SaaSBase.Web/Dockerfile`:

```dockerfile
FROM node:18-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
RUN npm run build -- --configuration production

FROM nginx:alpine
COPY --from=build /app/dist/saas-base-web/browser /usr/share/nginx/html
COPY nginx.conf /etc/nginx/nginx.conf
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
```

### Docker Compose

Create `docker-compose.yml`:

```yaml
version: '3.8'

services:
  postgres:
    image: postgres:15
    environment:
      POSTGRES_DB: saas_base_db
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: ${DB_PASSWORD}
    volumes:
      - postgres_data:/var/lib/postgresql/data
    ports:
      - "5432:5432"

  redis:
    image: redis:7-alpine
    ports:
      - "6379:6379"
    volumes:
      - redis_data:/data

  api:
    build:
      context: .
      dockerfile: SaaSBase.Api/Dockerfile
    environment:
      - ConnectionStrings__Default=Server=postgres;Database=saas_base_db;User Id=postgres;Password=${DB_PASSWORD}
      - Redis__Configuration=redis:6379
      - Jwt__Key=${JWT_SECRET_KEY}
    depends_on:
      - postgres
      - redis
    ports:
      - "5091:80"

  web:
    build:
      context: ./SaaSBase.Web
      dockerfile: Dockerfile
    ports:
      - "4200:80"
    depends_on:
      - api

volumes:
  postgres_data:
  redis_data:
```

### Deploy with Docker

```bash
# Build and start
docker-compose up -d

# View logs
docker-compose logs -f

# Stop
docker-compose down
```

## Azure Deployment

### App Service Deployment

1. **Create App Service**:
   ```bash
   az webapp create --resource-group myResourceGroup --plan myAppServicePlan --name saasbase-api
   ```

2. **Configure Connection Strings**:
   ```bash
   az webapp config connection-string set \
     --resource-group myResourceGroup \
     --name saasbase-api \
     --connection-string-type PostgreSQL \
     --settings Default="Server=..."
   ```

3. **Deploy**:
   ```bash
   az webapp deployment source config-zip \
     --resource-group myResourceGroup \
     --name saasbase-api \
     --src api.zip
   ```

### Azure Database for PostgreSQL

1. Create PostgreSQL server
2. Configure firewall rules
3. Update connection string
4. Run migrations

## AWS Deployment

### EC2 Deployment

1. **Launch EC2 Instance** (Ubuntu Server)
2. **Install Dependencies**:
   ```bash
   sudo apt update
   sudo apt install -y dotnet-sdk-7.0 nodejs npm nginx postgresql-client
   ```

3. **Deploy Application**:
   ```bash
   # Clone repository
   git clone https://github.com/kawser2133/saas-base.git
   cd saas-base
   
   # Build and publish
   dotnet publish SaaSBase.Api -c Release
   ```

4. **Configure Nginx**:
   ```nginx
   server {
       listen 80;
       server_name your-domain.com;
       
       location / {
           proxy_pass http://localhost:5091;
           proxy_http_version 1.1;
           proxy_set_header Upgrade $http_upgrade;
           proxy_set_header Connection keep-alive;
           proxy_set_header Host $host;
           proxy_cache_bypass $http_upgrade;
       }
   }
   ```

5. **Set up Systemd Service**:
   ```ini
   [Unit]
   Description=SaaSBase API
   
   [Service]
   WorkingDirectory=/var/www/saasbase/SaaSBase.Api
   ExecStart=/usr/bin/dotnet /var/www/saasbase/SaaSBase.Api/SaaSBase.Api.dll
   Restart=always
   RestartSec=10
   
   [Install]
   WantedBy=multi-user.target
   ```

## Linux Server Deployment

### Manual Deployment

1. **Install .NET Runtime**:
   ```bash
   wget https://dot.net/v1/dotnet-install.sh
   chmod +x dotnet-install.sh
   ./dotnet-install.sh --runtime aspnetcore
   ```

2. **Build Application**:
   ```bash
   dotnet publish SaaSBase.Api -c Release -o /var/www/saasbase/api
   ```

3. **Configure Nginx** (see AWS section)

4. **Set up Service** (see AWS section)

## Environment Configuration

### Backend Environment Variables

```bash
# Database
ConnectionStrings__Default="Server=..."

# JWT
Jwt__Key="your-secret-key"
Jwt__Issuer="saasbase"
Jwt__Audience="saasbase-clients"

# Redis
Redis__Configuration="localhost:6379"

# Email
SendGrid__ApiKey="your-api-key"
SendGrid__FromEmail="noreply@yourdomain.com"

# App Settings
AppSettings__BaseUrl="https://yourdomain.com"
AppSettings__BackendUrl="https://api.yourdomain.com"
```

### Frontend Environment

Update `environment.prod.ts`:

```typescript
export const environment = {
  production: true,
  apiBaseUrl: 'https://api.yourdomain.com',
  apiVersion: 'v1'
};
```

## Database Migration

### Production Migration

```bash
# Set connection string
export ConnectionStrings__Default="Server=..."

# Run migrations
dotnet ef database update --project SaaSBase.Infrastructure --startup-project SaaSBase.Api
```

### Backup Before Migration

```bash
# PostgreSQL backup
pg_dump -h localhost -U postgres saas_base_db > backup.sql

# Restore if needed
psql -h localhost -U postgres saas_base_db < backup.sql
```

## SSL/HTTPS Setup

### Let's Encrypt (Free SSL)

```bash
# Install Certbot
sudo apt install certbot python3-certbot-nginx

# Get certificate
sudo certbot --nginx -d yourdomain.com

# Auto-renewal
sudo certbot renew --dry-run
```

### Nginx SSL Configuration

```nginx
server {
    listen 443 ssl http2;
    server_name yourdomain.com;
    
    ssl_certificate /etc/letsencrypt/live/yourdomain.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/yourdomain.com/privkey.pem;
    
    # SSL configuration
    ssl_protocols TLSv1.2 TLSv1.3;
    ssl_ciphers HIGH:!aNULL:!MD5;
    
    location / {
        proxy_pass http://localhost:5091;
    }
}

# Redirect HTTP to HTTPS
server {
    listen 80;
    server_name yourdomain.com;
    return 301 https://$server_name$request_uri;
}
```

## Monitoring & Logging

### Application Insights (Azure)

```csharp
// In Program.cs
builder.Services.AddApplicationInsightsTelemetry();
```

### Serilog Configuration

```json
{
  "Serilog": {
    "WriteTo": [
      {
        "Name": "File",
        "Args": {
          "path": "/var/log/saasbase/log-.txt",
          "rollingInterval": "Day"
        }
      }
    ]
  }
}
```

### Health Checks

```csharp
builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString)
    .AddRedis(redisConnection);
```

---

For more information, see [README.md](README.md) and [QUICK_START.md](QUICK_START.md).
