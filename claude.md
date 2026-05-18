# Project Rules

## Tech Stack
- .NET 10, C# 13, EF Core 10
- ASP.NET Core Web API
- SQLite, Redis

## Architecture
- Clean Architecture (API, Application, Domain, Infrastructure)

## Conventions
- Use vertical slice for new features.
- Async methods must end with `Async`.
- DTOs in Application Layer.

## Commands
- Run: `dotnet run --project src/Api`
- Test: `dotnet test`
- Migration: `dotnet ef migrations add <Name> --project src/Infrastructure --startup-project src/Api`

## Rules
- Do not use Newtonsoft.Json; use System.Text.Json.
- Validate inputs using FluentValidation.

## Docker Strategy
- Use Multi-stage builds (SDK -> Runtime)
- Base image: ://microsoft.com
- Run as non-root user (USER app)
- Include HEALTHCHECK in Dockerfile

## Build/Run
- Use `docker compose up --build` to run locally
- Port 8080 is exposed

## .dockerignore
- Must exclude /bin, /obj, /.vs

## Security
- NEVER check secrets into git. Use environment variables in docker-compose.