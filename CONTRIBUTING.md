# Contributing

## Setup

```bash
docker compose up -d db     # SQL Server on localhost:1433
dotnet run                  # application on http://localhost:5000 with demo data
dotnet test                 # integration and unit tests, each class gets its own database
```

## Before opening a pull request

```bash
dotnet format whitespace DentalClinic.sln          # apply .editorconfig
dotnet build DentalClinic.sln -warnaserror         # no warnings allowed
dotnet test --collect:"XPlat Code Coverage" --settings coverlet.runsettings
python3 docker/smoke_test.py http://localhost:5000 # with the application running
```

CI runs the same checks and a smoke test of the whole Docker stack.

## Conventions

- Controllers stay thin and are protected with `[RoleRequired(...)]`. Every state change is a POST.
- New behaviour comes with a test in `DentalClinic.Tests` going through real HTTP
  (see `TestApp.PostFormAsync` for forms with antiforgery tokens).
- Views use the design tokens from `wwwroot/css/site.css`. Inline scripts are not allowed by the Content Security Policy.
- Text shown to users is in Russian.
