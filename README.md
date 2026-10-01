# Aurora Dent

> 🇬🇧 English | [🇷🇺 Русский](README.ru.md)

[![CI](https://github.com/DogNellaf/dental-clinic/actions/workflows/ci.yml/badge.svg)](https://github.com/DogNellaf/dental-clinic/actions/workflows/ci.yml)
![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)
![EF Core](https://img.shields.io/badge/EF%20Core-8-6C3FC5)
![SQL Server](https://img.shields.io/badge/SQL%20Server-2022-CC2927)
![Tests](https://img.shields.io/badge/tests-96%20passing-brightgreen)
![Coverage](https://img.shields.io/badge/coverage-97%25%20lines-brightgreen)
![License](https://img.shields.io/badge/license-PolyForm%20Noncommercial-orange)

Web application for a dental clinic. A public site offers online booking, and
separate cabinets serve patients, doctors, managers and administrators. Data is
stored in SQL Server, and an empty database is filled with a demo clinic on the
first start. The interface is in Russian. The clinic, doctors and reviews are
fictional, photos are from [Unsplash](https://unsplash.com).

![Home page](docs/screenshots/home.png)

## Quick start

Docker Compose starts the application together with SQL Server.

```bash
docker compose up --build
```

Open <http://localhost:8080>. The login page has one-click buttons for the demo
accounts, and the shared password is `Demo123!`.

| Role | Email | Scope |
|---|---|---|
| Patient | `client@clinic.demo` | Booking and cancelling visits, recommendations, a review |
| Doctor | `doctor@clinic.demo` | Live dashboard of the current visit, extending or finishing a visit, patient records |
| Manager | `manager@clinic.demo` | Review moderation queue |
| Administrator | `admin@clinic.demo` | Clinic overview, schedule, profiles, reviews |

For development on the host machine, start only the database and run the app.

```bash
docker compose up -d db
dotnet run
```

The app listens on <http://localhost:5000>. A ready image is published to
GitHub Container Registry by CI.

```bash
docker run -p 8080:8080 -e ConnectionStrings__DefaultConnection="..." ghcr.io/dognellaf/dental-clinic:latest
```

## Case study

### Problem

A small clinic needs a single place where patients see free time and book
visits without a phone call, doctors run the day including the visit in progress,
managers control which reviews get published, and administrators handle
everything else. Two patients must never end up with the same time slot, and
every role must see only the data required for the role.

### Solution

| Role | Cabinet |
|---|---|
| **Patient** | Upcoming visits and history with the doctor's recommendations, cancelling up to 2 hours before a visit, one review passing moderation |
| **Doctor** | Current visit with a progress bar, schedule for the day, extending or finishing a visit with a reason, recommendations, patient history |
| **Manager** | Pending and published reviews with a counter, publish and hide actions |
| **Administrator** | Counters and the next visits, CRUD for schedule slots, profiles (create, edit, ban, delete) and reviews |

Booking is a three-step flow on one page with an optional service, a doctor
filtered by the service, and a free time. Free time is visible to everyone, and
booking is reserved for patients.

### Engineering highlights

- **A slot cannot be booked twice.** Booking is a single
  `UPDATE ... WHERE Id = @id AND ClientId = 0` (`ExecuteUpdate`). Of two
  simultaneous requests only one changes a row, and the other receives a
  message about the time just taken. Integration tests cover the race
  between two patients.
- **Authorization lives in one place.** The `[RoleRequired]` filter resolves
  the signed-in profile once per request, checks the role, and signs out
  accounts banned or deleted while the cookie was still valid. Anonymous users
  are sent to the login page and users with another role to the access denied
  page. Tests check every role against every cabinet.
- **Schema and roles come from the model.** The four roles are part of the EF
  model (`HasData`), so a fresh database is usable immediately.
- **Idempotent demo data.** The seeder runs only on an empty database and
  builds the schedule relative to the current date, including a visit in
  progress, so the doctor dashboard is never empty.
- **Consistent deletes.** Deleting a profile frees the future slots of the
  patient and removes the reviews. Deleting a doctor also removes the staff
  card and the schedule. Creating a doctor creates the staff card as well, so
  the cabinet works immediately.
- **Operations built in.** A `/health` endpoint checks the database, and the
  Docker health check uses the endpoint. Every response carries security
  headers (Content Security Policy without inline scripts, `X-Frame-Options`,
  `nosniff`, referrer and permissions policies). Versioned static files are
  cached for a long time. Database connections are retried while the server
  starts.
- **No client-side framework.** Hand-written CSS design system with tokens in
  one stylesheet, about 100 lines of vanilla JS, an inline SVG icon sprite and
  self-hosted fonts. Cyrillic is rendered as is instead of `&#x...;` entities.
- **Accessible and responsive.** Semantic markup, visible focus states,
  `prefers-reduced-motion` support, a mobile menu, layouts checked at 390 px.

### Security

- Every POST carries an antiforgery token (`AutoValidateAntiforgeryToken`).
  Logout and booking are POST-only, so a link or an image cannot trigger either action.
- Banned accounts cannot sign in and are signed out on the next request.
- Patients open and cancel only own visits, and anything else returns 404.
  Doctors work only with visits assigned to the doctor.
- The post-login `returnUrl` is accepted only when local.
- Input is validated on the server with Russian messages. Profile edits bind
  explicit view models instead of entities.
- A Content Security Policy forbids inline scripts and third-party code. A test
  scans the pages and fails on any inline script or `onclick`.
- Passwords are hashed by ASP.NET Core Identity. Demo accounts share a
  published password, so `Seed__DemoData=false` is required in any real
  deployment.

### Architecture

```mermaid
flowchart LR
    U[Browser] -->|HTTP| C[MVC controllers]
    C -->|RoleRequired filter| P[(Profiles)]
    C --> D[EF Core DatabaseContext]
    D --> DB[(SQL Server)]
    S[DemoDataSeeder] -->|empty database only| D
    C --> V[Razor views and layouts]
```

| Module | Responsibility |
|---|---|
| `Controllers/HomeController.cs` | Public pages, schedule browsing and atomic booking |
| `Controllers/{Auth,Client,Doctor,Manager,Admin}Controller.cs` | One controller per role, protected by `[RoleRequired]` |
| `Infrastructure/RoleRequiredAttribute.cs` | Authentication, role check and ban enforcement |
| `Infrastructure/SecurityHeaders.cs` | CSP and other security headers |
| `Infrastructure/Fmt.cs` | Russian formatting for money, dates and declensions |
| `Data/DemoDataSeeder.cs` | Demo clinic with services, doctors, schedule, patients and reviews |
| `Models/ViewModels/` | Shapes passed to views, which keeps views free of extra queries |
| `Views/Shared/_CabinetLayout.cshtml` | Shared layout of the four cabinets |

## Screenshots

| Online booking | Patient cabinet |
|---|---|
| ![Schedule](docs/screenshots/schedule.png) | ![Patient cabinet](docs/screenshots/client.png) |

| Doctor dashboard | Administrator overview |
|---|---|
| ![Doctor](docs/screenshots/doctor.png) | ![Admin](docs/screenshots/admin.png) |

| Services | Review moderation |
|---|---|
| ![Services](docs/screenshots/services.png) | ![Manager](docs/screenshots/manager.png) |

| Mobile home | Mobile booking |
|---|---|
| ![Mobile home](docs/screenshots/mobile-home.png) | ![Mobile booking](docs/screenshots/mobile-schedule.png) |

## Configuration

Settings come from `appsettings.json` or environment variables such as
`ConnectionStrings__DefaultConnection`.

| Key | Purpose | Default |
|---|---|---|
| `ConnectionStrings:DefaultConnection` | SQL Server connection string | `localhost,1433`, database `dental_clinic`, the `db` service of `docker-compose.yml` |
| `Seed:DemoData` | Fill an empty database with demo data | `true` |
| `Hosting:HttpsRedirection` | Redirect HTTP to HTTPS, off because TLS is usually terminated by a proxy | `false` |

Tables and roles are created on the first start with `EnsureCreated`. Migrations
are not used, the schema is built from the model. The first administrator of a
real deployment has to be created in the database. Role ids are 1 client,
2 administrator, 3 manager and 4 doctor.

## Tests

Tests need a SQL Server. The `db` service of Compose provides one.

```bash
docker compose up -d db
dotnet test
```

There are 96 tests with 97% line coverage. Most are integration tests starting
the whole application on a fresh database and use real HTTP requests,
cookies and antiforgery tokens. They cover public pages, access control for
every role, registration, login and bans, booking including the race for one
slot, cancelling, the visit controls of the doctor, the clean-up rules of the
administrator, review moderation, security headers and the health check. The
rest are unit tests of the formatting helpers. Every test class creates a
separate database and drops the database afterwards. Another server can be
selected with the `TEST_SQLSERVER_CONNECTION` variable, a connection string without a database
name.

An end-to-end smoke test walks the real user journey over HTTP against a
running instance, covering health, public pages, sign in, booking and
cancelling, and role separation.

```bash
python3 docker/smoke_test.py http://localhost:8080
```

### CI

[`ci.yml`](.github/workflows/ci.yml) runs on every push and pull request.

| Job | Action |
|---|---|
| **Format and warnings** | `dotnet format` against `.editorconfig` and a Release build with warnings as errors |
| **Tests with coverage** | Whole suite against a SQL Server service container, failing below 85% line coverage, coverage written to the job summary |
| **Docker** | Builds the image, starts the Compose stack, waits for the health checks and runs the smoke test |

[`docker-publish.yml`](.github/workflows/docker-publish.yml) pushes the image to
GitHub Container Registry from `master` and on version tags. Dependabot tracks
NuGet packages, GitHub Actions and base images. Local checks are listed in
[CONTRIBUTING.md](CONTRIBUTING.md).

## Project structure

```
├── Controllers/           # Public site and one controller per role
├── Data/                  # Demo-data seeder
├── Infrastructure/        # [RoleRequired] filter, security headers, formatting helpers
├── Models/                # EF entities, DTOs and view models
├── Views/                 # Razor views, layouts and partials
├── wwwroot/               # CSS, JS, fonts and images
├── DentalClinic.Tests/    # xUnit integration and unit tests
├── docker/smoke_test.py   # End-to-end smoke test of a running instance
├── docs/screenshots/
├── Dockerfile
├── docker-compose.yml     # Application and SQL Server
└── .github/               # CI, image publishing, Dependabot, PR template
```

## Credits

Photos from [Unsplash](https://unsplash.com) under the Unsplash License.
Clinic interior by Benyamin Bohlouli, X-ray review by Jonathan Borba, smile by
Dr Farid Sharifi, doctor portraits by Siednji Leon, Bruno Rodrigues and Usman
Yousaf. Fonts Manrope and Playfair Display (SIL OFL) via Fontsource.

## License

[PolyForm Noncommercial 1.0.0](LICENSE). Use, modification and redistribution
are allowed for any noncommercial purpose, provided the notice
`Copyright (c) 2026 DogNellaf` is kept. Commercial use requires a separate
license from [DogNellaf](https://github.com/DogNellaf).
