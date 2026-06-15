# Poseidon Server

.NET 10 ASP.NET Core API with Docker, Docker Compose, a health ping endpoint, and Entity Framework Core wired through an in-memory provider until the real database is chosen.

## Run locally

```powershell
dotnet restore
dotnet run
```

Ping:

```powershell
curl http://localhost:5076/health/ping
```

## Run with Docker Compose

```powershell
docker compose up --build
```

Ping:

```powershell
curl http://localhost:8080/health/ping
```

## NuGet Packages

Docker downloads these during `dotnet restore` in the build stage:

- `Microsoft.AspNetCore.OpenApi` `10.0.8`
- `Microsoft.EntityFrameworkCore.InMemory` `10.0.8`

`Microsoft.EntityFrameworkCore.InMemory` also restores EF Core transitive dependencies. When the database is selected, replace the in-memory provider in `Program.cs` with the matching provider package and `Use...` registration.
