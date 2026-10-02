# Changelog

## 1.0.0

First stable release of Aurora Dent, a web application for a dental clinic.

### Features

- Public site with services, doctors, FAQ and a three step online booking flow.
- Cabinets for patients, doctors, managers and administrators with role based access.
- Atomic booking that never gives one slot to two patients, cancelling up to two hours before a visit, calendar files and email confirmations.
- Live doctor dashboard with extending and finishing a visit, recommendations and patient records.
- Review moderation, bulk schedule generation, profile management with bans, search and paging in every list.
- Interface, validation messages, emails and calendar files in Russian, English, French and German.

### Engineering

- ASP.NET Core 8 MVC, EF Core 8 with migrations on SQL Server, ASP.NET Identity with lockout and password reset.
- Service layer, a background email queue over SMTP, Serilog logging and a health endpoint.
- Content Security Policy, antiforgery tokens, trusted proxy configuration and persisted data protection keys.
- 246 tests with about 97% line coverage, an end to end smoke test, CI with format, build, coverage and Docker jobs.
- Docker Compose stack with SQL Server and a mail catcher, a container image published to GitHub Container Registry.
- README in four languages.

### License

PolyForm Noncommercial 1.0.0.
