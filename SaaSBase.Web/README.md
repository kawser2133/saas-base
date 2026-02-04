# SaaSBase - Frontend

A modern, enterprise-grade Angular application for SaaSBase platform.

## 🚀 Features

### ✅ Completed
- **Modern Angular 17+** with standalone components
- **Professional Enterprise Layout** with responsive design
- **Theme System** with light/dark mode support
- **Dashboard** with customizable widgets
- **Clean Architecture** with modular structure

### 📁 Project Structure
```
src/
├── app/
│   ├── core/                    # Core services and guards
│   ├── shared/                  # Shared components and utilities
│   │   └── layout/             # Header, Sidebar, Footer, Main Layout
│   └── features/               # Feature modules
│       ├── auth/               # Authentication
│       ├── dashboard/          # Dashboard & Analytics
│       ├── profile/            # User Profile
│       └── organization/       # Organization Management
```

## 🛠️ Technology Stack

- **Angular 17+** - Modern web framework
- **TypeScript** - Type-safe development
- **SCSS** - Enhanced CSS with variables and mixins
- **Font Awesome** - Professional icons
- **CSS Grid & Flexbox** - Modern layout system
- **CSS Variables** - Dynamic theming system

## 🎨 Design System

### Colors
- **Primary**: #2563eb (Blue)
- **Secondary**: #64748b (Gray)
- **Success**: #10b981 (Green)
- **Warning**: #f59e0b (Amber)
- **Error**: #ef4444 (Red)

## Getting Started

### Prerequisites
- Node.js 18+ and npm

### Installation
1. Install dependencies: `npm install`
2. Update API endpoint in `src/environments/environment.ts` if needed
3. Start development server: `npm start`
4. Navigate to `http://localhost:4200`

### Build
- Production build: `npm run build`
- Build output will be in `dist/` directory

## Features

### Authentication & Authorization
- Login/Logout
- Password reset
- Email verification
- Multi-factor authentication (MFA)
- Session management
- Role-based access control (RBAC)
- Permission-based UI rendering

### User Management
- User CRUD operations
- User profile management
- User activity tracking

### Role & Permission Management
- Role management
- Permission management
- Role-permission assignment

### Organization Management
- Organization setup
- Department management
- Position management
- Location management

### Security Features
- Password policy enforcement
- Session management
- MFA configuration

## Development

### Code Style
- Follow Angular style guide
- Use TypeScript strict mode
- Implement proper error handling
- Use RxJS for async operations

### Testing
- Unit tests: `npm test`
- E2E tests: `npm run e2e`

## License

[Specify your license here]
