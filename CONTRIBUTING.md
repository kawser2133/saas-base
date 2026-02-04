# Contributing to SaaSBase

Thank you for your interest in contributing to SaaSBase! This document provides guidelines and instructions for contributing to this open-source foundation project.

## What is SaaSBase?

SaaSBase is an open-source, production-ready foundation for building multi-tenant SaaS applications. It provides a complete starter kit with authentication, authorization, organization management, and enterprise-grade features that can be used as a foundation for any industry or business domain.

## How Can You Contribute?

We welcome contributions of all kinds:

- 🐛 **Bug Reports**: Found a bug? Please report it!
- 💡 **Feature Requests**: Have an idea? Share it with us!
- 📝 **Documentation**: Help improve our docs
- 🔧 **Code Contributions**: Fix bugs, add features, improve performance
- 🎨 **UI/UX Improvements**: Enhance the user experience
- 🧪 **Testing**: Add tests or improve test coverage
- 🌐 **Translations**: Help translate the application

## Getting Started

### Prerequisites

- .NET 7.0 SDK or later
- Node.js 18+ and npm
- PostgreSQL (or your preferred database)
- Redis (for caching)
- Git

### Development Setup

1. **Fork the repository** and clone your fork:
   ```bash
   git clone https://github.com/kawser2133/saas-base.git
   cd saas-base
   ```

2. **Set up the backend**:
   ```bash
   cd SaaSBase.Api
   # Update appsettings.json with your database and Redis connection strings
   dotnet restore
   dotnet ef database update
   dotnet run
   ```

3. **Set up the frontend**:
   ```bash
   cd SaaSBase.Web
   npm install
   # Update API endpoint in environment files if needed
   npm start
   ```

## Development Guidelines

### Code Style

- **Backend (.NET)**: Follow C# coding conventions and use meaningful names
- **Frontend (Angular)**: Follow Angular style guide and TypeScript best practices
- **Commit Messages**: Use clear, descriptive commit messages
- **Comments**: Add comments for complex logic

### Architecture

- Follow **Clean Architecture** principles
- Keep layers separated (Domain, Application, Infrastructure, API)
- Use **Repository Pattern** for data access
- Implement **Dependency Injection** throughout

### Pull Request Process

1. **Create a branch** from `main`:
   ```bash
   git checkout -b feature/your-feature-name
   # or
   git checkout -b fix/your-bug-fix
   ```

2. **Make your changes**:
   - Write clean, maintainable code
   - Add tests if applicable
   - Update documentation if needed

3. **Test your changes**:
   - Ensure backend API runs without errors
   - Test frontend functionality
   - Verify database migrations work correctly

4. **Commit your changes**:
   ```bash
   git add .
   git commit -m "Add: Description of your changes"
   ```

5. **Push and create a Pull Request**:
   ```bash
   git push origin feature/your-feature-name
   ```
   Then create a PR on GitHub with a clear description.

### Pull Request Guidelines

- **Title**: Clear and descriptive
- **Description**: Explain what changes you made and why
- **Related Issues**: Link to any related issues
- **Screenshots**: Include screenshots for UI changes
- **Testing**: Describe how you tested your changes

### Commit Message Format

Use clear, descriptive commit messages:

```
Add: Multi-tenant organization isolation
Fix: Password reset email not sending
Update: Angular dependencies to latest version
Refactor: User service for better performance
Docs: Update README with deployment instructions
```

## Code Review

All contributions go through code review:

- Be patient and respectful
- Address feedback promptly
- Be open to suggestions
- Ask questions if something is unclear

## Reporting Issues

### Bug Reports

When reporting bugs, please include:

- **Description**: Clear description of the bug
- **Steps to Reproduce**: Detailed steps to reproduce
- **Expected Behavior**: What should happen
- **Actual Behavior**: What actually happens
- **Environment**: OS, .NET version, Node version, etc.
- **Screenshots**: If applicable

### Feature Requests

When requesting features:

- **Use Case**: Describe the problem you're solving
- **Proposed Solution**: How you envision it working
- **Alternatives**: Other solutions you've considered
- **Additional Context**: Any other relevant information

## Project Structure

```
SaaSBase/
├── SaaSBase.Api/              # Web API layer
├── SaaSBase.Application/      # Application layer (Services, DTOs)
├── SaaSBase.Domain/           # Domain models
├── SaaSBase.Infrastructure/   # Infrastructure layer
└── SaaSBase.Web/             # Angular frontend
```

## Testing

- Write unit tests for new features
- Test API endpoints
- Test frontend components
- Ensure backward compatibility

## Documentation

- Update README.md for major changes
- Add code comments for complex logic
- Update API documentation (Swagger)
- Keep CHANGELOG.md updated

## Questions?

- Open an issue for questions
- Check existing issues and discussions
- Review the README.md for setup instructions

## Code of Conduct

Please read and follow our [Code of Conduct](CODE_OF_CONDUCT.md).

## License

By contributing, you agree that your contributions will be licensed under the MIT License.

---

Thank you for contributing to SaaSBase! 🎉
