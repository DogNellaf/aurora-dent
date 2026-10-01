# Аврора Дент — веб-приложение стоматологической клиники

> [🇬🇧 English](README.md) | 🇷🇺 Русский

Полноценное веб-приложение для клиники: публичный сайт с онлайн-записью и личные кабинеты для пациентов, врачей, менеджеров и администраторов. Построено на **ASP.NET Core 8 MVC** и **Entity Framework Core**. Запускается «из коробки» на **SQLite** с реалистичными демо-данными — настраивать базу не нужно.

> Портфолио-проект. Клиника, врачи и отзывы вымышлены; фотографии — с [Unsplash](https://unsplash.com).

<p align="center">
  <img src="docs/screenshots/home.png" alt="Главная страница" width="900">
</p>

## Запуск за 30 секунд

```bash
git clone <repository-url>
cd dental-clinic
dotnet run
```

Откройте адрес из консоли (по умолчанию <http://localhost:5000>). При первом запуске создаётся `App_Data/clinic.db`, в который загружаются услуги, врачи, расписание на две недели, пациенты и отзывы.

Демо-аккаунты (пароль у всех `Demo123!`) — на странице входа есть кнопки быстрого входа:

| Роль | Email | Что можно делать |
|---|---|---|
| Пациент | `client@clinic.demo` | записываться и отменять визиты, читать рекомендации врача, оставить отзыв |
| Врач | `doctor@clinic.demo` | дашборд текущего приёма, продление / досрочное завершение, рекомендации, карта пациента |
| Менеджер | `manager@clinic.demo` | модерация отзывов (публикация / скрытие) |
| Администратор | `admin@clinic.demo` | обзор клиники, расписание, профили (блокировка / удаление), отзывы |

## Скриншоты

| Онлайн-запись | Кабинет пациента |
|---|---|
| ![Запись](docs/screenshots/schedule.png) | ![Кабинет пациента](docs/screenshots/client.png) |
| **Кабинет врача** | **Панель администратора** |
| ![Врач](docs/screenshots/doctor.png) | ![Админ](docs/screenshots/admin.png) |
| **Услуги** | **Модерация отзывов** |
| ![Услуги](docs/screenshots/services.png) | ![Менеджер](docs/screenshots/manager.png) |

<p align="center">
  <img src="docs/screenshots/mobile-home.png" alt="Мобильная версия" width="260">
  <img src="docs/screenshots/mobile-schedule.png" alt="Запись с телефона" width="260">
</p>

## Возможности

**Публичный сайт**
- Лендинг: герой-блок, услуги, врачи, «как это работает», отзывы, призыв к действию
- Каталог услуг с фильтром по категориям и страницами услуг (цена, длительность, врачи)
- Пошаговая онлайн-запись: услуга → врач → свободное время
- Врачи, О клинике, FAQ (аккордеон), Контакты с картой
- Адаптивная вёрстка, доступность (семантика, фокус, `prefers-reduced-motion`), шрифты self-hosted

**Пациенты** — регистрация, запись и отмена (не позднее чем за 2 часа), история визитов с рекомендациями, один отзыв с модерацией.

**Врачи** — дашборд текущего приёма с прогрессом, расписание на день, продление / досрочное завершение с причиной, рекомендации, история пациента.

**Менеджеры** — очередь модерации отзывов.

**Администраторы** — обзор клиники, CRUD окон расписания, управление профилями (создание, правка, блокировка, удаление с очисткой связанных данных), управление отзывами.

**Инженерные детали**
- Авторизация по ролям через единый фильтр `[RoleRequired]`; заблокированные и удалённые аккаунты разлогиниваются при следующем запросе
- Antiforgery-токены на всех POST (`AutoValidateAntiforgeryToken`), выход и запись — только POST
- Атомарная запись на приём (`ExecuteUpdate … WHERE ClientId = 0`): два пациента не займут одно время
- Безопасный `returnUrl`, серверная валидация с русскими сообщениями
- Переключение провайдера БД: SQLite (по умолчанию) или SQL Server
- Идемпотентный сидер демо-данных, роли заданы в модели EF (`HasData`)
- 50+ интеграционных тестов, поднимающих реальное приложение на временной SQLite
- Dockerfile и GitHub Actions

## Технологии

| Слой | Технология |
|---|---|
| Backend | C#, ASP.NET Core 8 MVC, Razor |
| ORM | Entity Framework Core 8 |
| База данных | SQLite (по умолчанию) / SQL Server |
| Аутентификация | ASP.NET Core Identity (cookie), фильтр ролей |
| Frontend | Собственная дизайн-система на CSS, vanilla JS, jQuery Validation |
| Тесты | xUnit, `WebApplicationFactory` |
| DevOps | Docker, GitHub Actions |

## Конфигурация

`appsettings.json`:

| Ключ | Описание | По умолчанию |
|---|---|---|
| `Database:Provider` | `Sqlite` или `SqlServer` | `Sqlite` |
| `ConnectionStrings:DefaultConnection` | Строка подключения выбранного провайдера | `Data Source=App_Data/clinic.db` |
| `Seed:DemoData` | Заполнять пустую БД демо-данными | `true` |
| `Hosting:HttpsRedirection` | Перенаправление HTTP → HTTPS | `false` |

Запуск на SQL Server:

```bash
dotnet run --Database:Provider=SqlServer \
  --ConnectionStrings:DefaultConnection="Server=.\SQLEXPRESS;Database=dental_clinic;Trusted_Connection=True;Encrypt=False;"
```

Для боевого запуска задайте `Seed__DemoData=false` и создайте первого администратора в БД (роли создаются автоматически: 1 — клиент, 2 — администратор, 3 — менеджер, 4 — доктор).

## Docker

```bash
docker build -t aurora-dent .
docker run -p 8080:8080 -v aurora-data:/data aurora-dent
```

## Тесты

```bash
dotnet test
```

Тесты запускают всё приложение на временном файле SQLite — внешние сервисы не нужны.

## Структура проекта

```
dental-clinic/
├── Controllers/            # Home (сайт + запись), Auth, Client, Doctor, Manager, Admin
├── Data/DemoDataSeeder.cs  # Демо-клиника: услуги, врачи, расписание, пациенты, отзывы
├── Infrastructure/         # Фильтр [RoleRequired], хелперы форматирования
├── Models/                 # Сущности EF, DTO и view-модели
├── Views/                  # Razor-представления (сайт + кабинеты с общим layout)
├── wwwroot/                # CSS, JS, шрифты, изображения
├── DentalClinic.Tests/     # Интеграционные и unit-тесты xUnit
├── Dockerfile
└── Program.cs              # Конфигурация приложения и DI
```

## Благодарности

Фотографии — [Unsplash](https://unsplash.com) (лицензия Unsplash): интерьер — Benyamin Bohlouli, снимки КТ — Jonathan Borba, улыбка — Dr Farid Sharifi, портреты врачей — Siednji Leon, Bruno Rodrigues, Usman Yousaf. Шрифты: Manrope и Playfair Display (SIL OFL), через Fontsource.

## Лицензия

[MIT](LICENSE)
