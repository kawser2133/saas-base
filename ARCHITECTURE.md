# SaaSBase Architecture

This document provides an overview of the SaaSBase architecture, design patterns, and key architectural decisions.

## 🏗️ Architecture Overview

SaaSBase follows **Clean Architecture** principles, ensuring separation of concerns, testability, and maintainability.

```
┌─────────────────────────────────────────────────────────┐
│                    Presentation Layer                   │
│              (Angular Frontend - SaaSBase.Web)          │
└──────────────────────┬──────────────────────────────────┘
                        │
                        │ HTTP/REST API
                        │
┌───────────────────────▼──────────────────────────────────┐
│                      API Layer                          │
│              (Controllers - SaaSBase.Api)               │
└───────────────────────┬──────────────────────────────────┘
                        │
                        │ Service Interfaces
                        │
┌───────────────────────▼──────────────────────────────────┐
│                 Application Layer                       │
│         (Services, DTOs - SaaSBase.Application)          │
└───────────────────────┬──────────────────────────────────┘
                        │
                        │ Domain Models
                        │
┌───────────────────────▼──────────────────────────────────┐
│                    Domain Layer                         │
│              (Entities - SaaSBase.Domain)                │
└───────────────────────┬──────────────────────────────────┘
                        │
                        │ Repository Interfaces
                        │
┌───────────────────────▼──────────────────────────────────┐
│               Infrastructure Layer                       │
│    (Database, External Services - SaaSBase.Infrastructure)│
└──────────────────────────────────────────────────────────┘
```

## 📁 Project Structure

### Backend Layers

#### 1. SaaSBase.Api (Presentation/API Layer)
- **Purpose**: HTTP endpoints, request/response handling
- **Contains**:
  - Controllers (REST API endpoints)
  - Middleware (authentication, error handling)
  - Filters (validation, logging)
  - Background services
  - Configuration (Program.cs, Startup)

#### 2. SaaSBase.Application (Application/Business Logic Layer)
- **Purpose**: Business logic, use cases, orchestration
- **Contains**:
  - Service interfaces (`IServices/`)
  - Service implementations (`Implementations/`)
  - DTOs (Data Transfer Objects)
  - Mapping profiles (AutoMapper)
  - Application-specific exceptions

#### 3. SaaSBase.Domain (Domain/Business Rules Layer)
- **Purpose**: Core business entities and rules
- **Contains**:
  - Domain entities (User, Organization, Role, etc.)
  - Domain enums
  - Domain interfaces
  - Business rules and validations

#### 4. SaaSBase.Infrastructure (Data/External Services Layer)
- **Purpose**: Data access, external service integrations
- **Contains**:
  - Database context (EF Core)
  - Repositories (data access)
  - External service implementations (Email, SMS, File Storage)
  - Infrastructure services

### Frontend Structure

```
SaaSBase.Web/src/app/
├── core/                    # Core functionality
│   ├── guards/             # Route guards (auth, permissions)
│   ├── interceptors/       # HTTP interceptors
│   ├── services/           # Core services (auth, API)
│   └── models/             # Core models/interfaces
├── features/               # Feature modules
│   ├── auth/              # Authentication module
│   ├── dashboard/         # Dashboard module
│   ├── organization/      # Organization management
│   └── ...
└── shared/                # Shared components
    ├── components/         # Reusable components
    ├── layout/            # Layout components
    └── services/          # Shared services
```

## 🔑 Key Design Patterns

### 1. Clean Architecture
- **Separation of Concerns**: Each layer has a specific responsibility
- **Dependency Rule**: Inner layers don't depend on outer layers
- **Independence**: Business logic independent of frameworks and UI

### 2. Repository Pattern
- **Abstraction**: Data access logic abstracted from business logic
- **Testability**: Easy to mock repositories for unit testing
- **Flexibility**: Can switch data sources without changing business logic

### 3. Unit of Work Pattern
- **Transaction Management**: Ensures data consistency
- **Change Tracking**: Tracks all changes in a single transaction
- **Atomic Operations**: All changes succeed or fail together

### 4. Dependency Injection
- **Loose Coupling**: Components depend on abstractions, not concretions
- **Testability**: Easy to inject mock dependencies
- **Maintainability**: Changes in one component don't affect others

### 5. DTO Pattern (Data Transfer Objects)
- **Data Transfer**: Separate objects for API communication
- **Security**: Prevents exposing internal domain models
- **Versioning**: Easy to version APIs without changing domain models

## 🏢 Multi-Tenant Architecture

### Tenant Isolation Strategy

SaaSBase uses **Organization-based tenant isolation**:

1. **Tenant Context**: Every request includes tenant/organization context
2. **Data Filtering**: All queries automatically filter by tenant
3. **Isolation**: Complete data isolation between tenants
4. **Scalability**: Supports thousands of tenants

### Implementation

```csharp
// Tenant context is set per request
public class CurrentTenantService : ICurrentTenantService
{
    // Gets current tenant from JWT token or header
    public Guid? GetCurrentTenantId() { ... }
}

// All repositories filter by tenant
public class Repository<T> where T : BaseEntity
{
    public IQueryable<T> GetAll()
    {
        var tenantId = _currentTenantService.GetCurrentTenantId();
        return _dbSet.Where(x => x.OrganizationId == tenantId);
    }
}
```

## 🔐 Security Architecture

### Authentication Flow

```
1. User Login → AuthController
2. Validate Credentials → AuthService
3. Generate JWT Token → Token Service
4. Return Token → Frontend
5. Store Token → Local Storage
6. Include in Requests → HTTP Interceptor
7. Validate Token → JWT Middleware
8. Extract User Context → CurrentUserService
```

### Authorization Flow

```
1. Request with JWT → API
2. Extract User & Roles → JWT Middleware
3. Check Permission → Permission Service
4. Authorize or Deny → Authorization Filter
5. Execute Action → Controller
```

## 📊 Data Flow

### Read Operation Flow

```
Frontend Request
    ↓
API Controller
    ↓
Application Service
    ↓
Repository (Infrastructure)
    ↓
Database (PostgreSQL)
    ↓
Entity Framework Core
    ↓
Domain Entity
    ↓
DTO Mapping (AutoMapper)
    ↓
Response to Frontend
```

### Write Operation Flow

```
Frontend Request (DTO)
    ↓
API Controller
    ↓
Validation
    ↓
Application Service
    ↓
Business Logic
    ↓
Repository (Unit of Work)
    ↓
Database Transaction
    ↓
Save Changes
    ↓
Return Result
```

## 🗄️ Database Architecture

### Key Tables

- **Users**: User accounts
- **Organizations**: Tenant organizations
- **Roles**: Role definitions
- **Permissions**: Permission definitions
- **RolePermissions**: Role-Permission mapping
- **UserRoles**: User-Role assignment
- **RefreshTokens**: JWT refresh tokens
- **Sessions**: User sessions
- **Departments**: Organization departments
- **Positions**: Job positions

### Relationships

```
Organization (1) ──→ (Many) Users
Organization (1) ──→ (Many) Departments
Organization (1) ──→ (Many) Roles
User (Many) ──→ (Many) Roles
Role (Many) ──→ (Many) Permissions
```

## 🔄 Caching Strategy

### Redis Caching

- **User Sessions**: Cached for quick access
- **Permissions**: Cached per user/role
- **Menu Items**: Cached per role
- **Organization Settings**: Cached per tenant
- **Cache Invalidation**: Automatic on updates

## 📦 Dependency Flow

```
SaaSBase.Api
    ├── SaaSBase.Application
    └── SaaSBase.Infrastructure
            └── SaaSBase.Domain

SaaSBase.Application
    └── SaaSBase.Domain

SaaSBase.Infrastructure
    └── SaaSBase.Domain
```

**Rule**: Dependencies flow inward. Outer layers depend on inner layers, never the reverse.

## 🚀 Scalability Considerations

### Horizontal Scaling
- **Stateless API**: Can scale across multiple servers
- **JWT Tokens**: No server-side session storage
- **Database**: Can use read replicas
- **Redis**: Can cluster for high availability

### Vertical Scaling
- **Caching**: Reduces database load
- **Connection Pooling**: Efficient database connections
- **Async Operations**: Non-blocking I/O

## 🧪 Testing Strategy

### Unit Tests
- **Domain Logic**: Test business rules
- **Services**: Mock dependencies
- **Repositories**: Test data access logic

### Integration Tests
- **API Endpoints**: Test full request/response cycle
- **Database**: Test with test database
- **Authentication**: Test auth flows

### E2E Tests
- **Frontend**: Test user workflows
- **Full Stack**: Test complete features

## 📝 Best Practices

1. **Keep Layers Separate**: Don't mix concerns
2. **Use Interfaces**: Depend on abstractions
3. **Single Responsibility**: Each class has one job
4. **DRY Principle**: Don't repeat yourself
5. **SOLID Principles**: Follow SOLID design principles
6. **Error Handling**: Centralized error handling
7. **Logging**: Comprehensive logging at all layers
8. **Documentation**: Document complex logic

## 🔮 Future Enhancements

- [ ] Event-driven architecture
- [ ] Microservices support
- [ ] GraphQL API
- [ ] Real-time features (SignalR)
- [ ] Advanced caching strategies
- [ ] Multi-database support per tenant

---

For more details, see the [README.md](README.md) and [CONTRIBUTING.md](CONTRIBUTING.md).
