# Aurora Dent — dental clinic web app

> 🇬🇧 English | [🇷🇺 Русский](README.ru.md)

A full-stack web application for a dental clinic: a public marketing site with online booking, and role-based cabinets for patients, doctors, managers and administrators. Built with **ASP.NET Core 8 MVC** and **Entity Framework Core**, runs out of the box on **SQLite** with realistic demo data — no database setup required.

> Portfolio project. The clinic, doctors and reviews are fictional; photos are from [Unsplash](https://unsplash.com).

<p align="center">
  <img src="docs/screenshots/home.png" alt="Home page" width="900">
</p>

## Try it in 30 seconds

```bash
git clone <repository-url>
cd dental-clinic
dotnet run
```

Open <http://localhost:5000> (the port is printed in the console). The first start creates `App_Data/clinic.db` and fills it with services, doctors, a two-week schedule, patients and reviews.

Sign in with a demo account (password for all: `Demo123!`) — the login page has one-click buttons:

| Role | Email | What you can do |
|---|---|---|
| Patient | `client@clinic.demo` | book / cancel visits, read doctor's recommendations, leave a review |
| Doctor | `doctor@clinic.demo` | live "current appointment" dashboard, extend / finish early, write recommendations, patient records |
| Manager | `manager@clinic.demo` | moderate patient reviews (publish / hide) |
| Administrator | `admin@clinic.demo` | overview stats, manage schedule slots, profiles (ban / delete) and reviews |

## Screenshots

| Online booking | Patient cabinet |
|---|---|
| ![Schedule](docs/screenshots/schedule.png) | ![Patient cabinet](docs/screenshots/client.png) |
| **Doctor dashboard** | **Admin overview** |
| ![Doctor](docs/screenshots/doctor.png) | ![Admin](docs/screenshots/admin.png) |
| **Services** | **Review moderation** |
| ![Services](docs/screenshots/services.png) | ![Manager](docs/screenshots/manager.png) |

<p align="center">
  <img src="docs/screenshots/mobile-home.png" alt="Mobile home" width="260">
  <img src="docs/screenshots/mobile-schedule.png" alt="Mobile booking" width="260">
</p>

## Features

**Public site**
- Landing page with hero, services, doctors, how-it-works, patient reviews and CTA
- Services catalogue with category filter and service detail pages (price, duration, doctors who perform it)
- Step-by-step online booking: service → doctor → free time slot
- Doctors, About, FAQ (accordion), Contacts with map
- Fully responsive, accessible (semantic HTML, focus states, reduced-motion support), self-hosted fonts

**Patients** — register, book and cancel (up to 2 h before the visit), visit history with the doctor's recommendations, one review that goes through moderation.

**Doctors** — live dashboard with the current visit and progress bar, today's schedule, extend / finish early with a reason, recommendations, patient history.

**Managers** — review moderation queue with counters.

**Administrators** — clinic overview, schedule slots CRUD, profile management (create, edit, ban / unban, delete with consistent clean-up), review management.

**Engineering details**
- Role-based authorization through a single `[RoleRequired]` filter; banned and deleted accounts are signed out on the next request
- Antiforgery tokens on every POST (`AutoValidateAntiforgeryToken`), logout and booking are POST-only
- Atomic slot booking (`ExecuteUpdate … WHERE ClientId = 0`) so two patients can never take the same time
- Open-redirect-safe `returnUrl`, server-side validation with Russian messages
- Provider switch: SQLite (default) or SQL Server via configuration
- Idempotent demo-data seeder, roles are part of the EF model (`HasData`)
- 50+ integration tests that boot the real app on a temporary SQLite database
- Dockerfile and GitHub Actions workflow

## Tech stack

| Layer | Technology |
|---|---|
| Backend | C#, ASP.NET Core 8 MVC, Razor |
| ORM | Entity Framework Core 8 |
| Database | SQLite (default) / SQL Server |
| Auth | ASP.NET Core Identity (cookie), role filter |
| Frontend | Hand-written CSS design system, vanilla JS, jQuery Validation |
| Tests | xUnit, `WebApplicationFactory` |
| DevOps | Docker, GitHub Actions |

## Configuration

`appsettings.json`:

| Key | Description | Default |
|---|---|---|
| `Database:Provider` | `Sqlite` or `SqlServer` | `Sqlite` |
| `ConnectionStrings:DefaultConnection` | Connection string for the chosen provider | `Data Source=App_Data/clinic.db` |
| `Seed:DemoData` | Fill an empty database with demo data | `true` |
| `Hosting:HttpsRedirection` | Redirect HTTP → HTTPS | `false` |

Using SQL Server instead of SQLite:

```bash
dotnet run --Database:Provider=SqlServer \
  --ConnectionStrings:DefaultConnection="Server=.\SQLEXPRESS;Database=dental_clinic;Trusted_Connection=True;Encrypt=False;"
```

For a real deployment set `Seed__DemoData=false` and create the first administrator through the database (roles are created automatically: 1 — client, 2 — admin, 3 — manager, 4 — doctor).

## Docker

```bash
docker build -t aurora-dent .
docker run -p 8080:8080 -v aurora-data:/data aurora-dent
```

## Tests

```bash
dotnet test
```

The tests start the whole application against a temporary SQLite file, so no external services are required.

## Project structure

```
dental-clinic/
├── Controllers/            # MVC controllers: Home (public + booking), Auth, Client, Doctor, Manager, Admin
├── Data/DemoDataSeeder.cs  # Demo clinic: services, doctors, schedule, patients, reviews
├── Infrastructure/         # [RoleRequired] filter, formatting helpers
├── Models/                 # EF entities, DTOs and view models
├── Views/                  # Razor views (public site + role cabinets with a shared layout)
├── wwwroot/                # CSS design system, JS, fonts, images
├── DentalClinic.Tests/     # xUnit integration + unit tests
├── Dockerfile
└── Program.cs              # Composition root
```

## Credits

Photos from [Unsplash](https://unsplash.com) (free to use under the Unsplash License): clinic interior — Benyamin Bohlouli, X-ray review — Jonathan Borba, smile — Dr Farid Sharifi, doctor portraits — Siednji Leon, Bruno Rodrigues, Usman Yousaf. Fonts: Manrope and Playfair Display (SIL OFL), via Fontsource.

## License

[MIT](LICENSE)
