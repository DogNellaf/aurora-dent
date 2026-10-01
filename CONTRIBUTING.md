# Contributing

## Setup

```bash
dotnet run          # app on SQLite with demo data, http://localhost:5000
dotnet test         # 90+ integration and unit tests, no external services
```

## Before you open a pull request

```bash
dotnet format whitespace DentalClinic.sln          # apply .editorconfig
dotnet build DentalClinic.sln -warnaserror         # no warnings allowed
dotnet test --collect:"XPlat Code Coverage" --settings coverlet.runsettings
python3 docker/smoke_test.py http://localhost:5000 # with the app running
```

CI runs the same checks, plus the whole suite on SQL Server and a smoke test of the Docker stack.

## Conventions

- Controllers stay thin and are protected with `[RoleRequired(...)]`; every state change is a POST.
- New behaviour comes with a test in `DentalClinic.Tests` that goes through real HTTP
  (see `TestApp.PostFormAsync` for forms with antiforgery tokens).
- Views use the design tokens from `wwwroot/css/site.css`; avoid inline scripts (the Content Security Policy forbids them).
- Text shown to users is in Russian.
