# Contributing

## Setup

```bash
docker compose up -d db mail   # SQL Server on localhost:1433, mail catcher on http://localhost:8025
dotnet tool restore            # the local EF tool
dotnet run --project src/DentalClinic   # Development profile, http://localhost:5000, demo data on (src/DentalClinic/appsettings.Development.json)
dotnet test                    # integration and unit tests, each class gets its own database
```

Changing the model needs a migration. A test fails when the model and the latest migration differ.

```bash
dotnet ef migrations add Name --project src/DentalClinic/DentalClinic.csproj -o Data/Migrations
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

- Controllers stay thin and are protected with `[RoleRequired(...)]`. Rules live in services under `src/DentalClinic/Services/`. Every state change is a POST.
- Times are clinic local time, taken from `IClinicClock`, never from `DateTime.Now`.
- New behaviour comes with a test in `tests/DentalClinic.Tests` going through real HTTP
  (see `TestApp.PostFormAsync` for forms with antiforgery tokens).
- Views use the design tokens from `src/DentalClinic/wwwroot/css/site.css`. Inline scripts are not allowed by the Content Security Policy.
- Russian is the source language. Text shown to users is written in Russian and passed through the localizer, `@T["..."]` in views and `T["..."]` in controllers, and every text needs an entry in `src/DentalClinic/Localization/Resources/en.json`, `fr.json` and `de.json`. A test lists missing entries. Text typed in by users or managers is shown as entered.
