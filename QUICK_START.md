# Quick Start Guide

Get SaaSBase up and running in minutes!

## 🚀 Prerequisites

Before you begin, ensure you have the following installed:

- [.NET 7.0 SDK](https://dotnet.microsoft.com/download) or later
- [Node.js 18+](https://nodejs.org/) and npm
- [PostgreSQL](https://www.postgresql.org/download/) (or your preferred database)
- [Redis](https://redis.io/download) (for caching)
- [Git](https://git-scm.com/)

## ⚡ Quick Setup (5 Minutes)

### Step 1: Clone the Repository

```bash
git clone https://github.com/kawser2133/saas-base.git
cd saas-base
```

### Step 2: Database Setup

1. **Create PostgreSQL Database**:
   ```sql
   CREATE DATABASE saas_base_db;
   ```

2. **Configure Connection String**:
   - Copy `SaaSBase.Api/appsettings.example.json` to `SaaSBase.Api/appsettings.json`
   - Update the connection string with your PostgreSQL credentials:
   ```json
   "ConnectionStrings": {
     "Default": "Server=localhost;Port=5432;Database=saas_base_db;User Id=postgres;Password=YOUR_PASSWORD"
   }
   ```

### Step 3: Redis Setup

1. **Start Redis** (if not running):
   ```bash
   # Windows (using Redis for Windows)
   redis-server
   
   # Linux/Mac
   redis-server
   
   # Docker
   docker run -d -p 6379:6379 redis
   ```

2. **Verify Redis Connection**:
   - Default: `localhost:6379`
   - Update in `appsettings.json` if different

### Step 4: Backend Setup

```bash
cd SaaSBase.Api

# Restore dependencies
dotnet restore

# Run database migrations
dotnet ef database update

# Start the API
dotnet run
```

The API will start at `http://localhost:5091`

### Step 5: Frontend Setup

Open a new terminal:

```bash
cd SaaSBase.Web

# Install dependencies
npm install

# Start development server
npm start
```

The frontend will start at `http://localhost:4200`

### Step 6: Access the Application

1. Open your browser: `http://localhost:4200`
2. **Default Login Credentials**:
   - Email: `admin@saasbase.com`
   - Password: `Admin@123!`

⚠️ **Important**: Change the default password immediately after first login!

## 🎯 What's Next?

### Explore the Features

- ✅ **Authentication**: Login, Register, Password Reset
- ✅ **User Management**: Create and manage users
- ✅ **Role Management**: Define roles and permissions
- ✅ **Organization Setup**: Create your first organization
- ✅ **Multi-Tenant**: Set up multiple organizations
- ✅ **Dashboard**: View analytics and insights

### Customize for Your Needs

1. **Update Branding**: Modify logo, colors, and branding
2. **Add Your Features**: Extend the foundation with your business logic
3. **Configure Email**: Set up SendGrid for email notifications
4. **Configure SMS**: Set up Twilio for SMS (optional)
5. **Deploy**: Follow deployment guides for production

## 🔧 Configuration

### Environment Variables (Optional)

For production, consider using environment variables:

**Backend (.NET)**:
```bash
export ConnectionStrings__Default="your-connection-string"
export Jwt__Key="your-jwt-secret-key"
```

**Frontend (Angular)**:
Update `src/environments/environment.prod.ts`:
```typescript
export const environment = {
  production: true,
  apiBaseUrl: 'https://your-api-domain.com',
  apiVersion: 'v1'
};
```

### JWT Configuration

Generate a strong JWT secret key (minimum 64 characters):

```bash
# Using OpenSSL
openssl rand -base64 64

# Or use an online generator
```

Update in `appsettings.json`:
```json
"Jwt": {
  "Key": "YOUR_GENERATED_SECRET_KEY_HERE"
}
```

## 🐛 Troubleshooting

### Database Connection Issues

- Verify PostgreSQL is running: `pg_isready`
- Check connection string format
- Ensure database exists
- Verify user permissions

### Redis Connection Issues

- Verify Redis is running: `redis-cli ping`
- Check Redis configuration in `appsettings.json`
- Ensure port 6379 is not blocked

### Frontend Not Connecting to Backend

- Check API URL in `environment.ts`
- Verify backend is running on correct port
- Check CORS configuration in backend
- Review browser console for errors

### Migration Issues

```bash
# Remove existing migrations (if needed)
dotnet ef migrations remove

# Create new migration
dotnet ef migrations add InitialCreate

# Update database
dotnet ef database update
```

## 📚 Additional Resources

- [Full Documentation](README.md)
- [Contributing Guide](CONTRIBUTING.md)
- [API Documentation](http://localhost:5091/swagger) (when running)
- [Architecture Overview](README.md#architecture-highlights)

## 🆘 Need Help?

- Check [GitHub Issues](https://github.com/kawser2133/saas-base/issues)
- Read the [FAQ](#) (coming soon)
- Join our [Discussions](https://github.com/kawser2133/saas-base/discussions)

---

**Happy Building! 🚀**
