using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using DentalClinic.Models;

namespace DentalClinic.Data
{
    /// <summary>
    /// Fills an empty database with a believable clinic: services, doctors, a schedule for the next two
    /// weeks, a few patients with visit history and reviews. All people and data are fictional.
    /// </summary>
    public static class DemoDataSeeder
    {
        public const string DemoPassword = "Demo123!";

        public static async Task SeedAsync(IServiceProvider services)
        {
            var db = services.GetRequiredService<DatabaseContext>();
            if (await db.Services.AnyAsync() || await db.Profiles.AnyAsync())
                return;

            var users = services.GetRequiredService<UserManager<Profile>>();

            // ---- services -------------------------------------------------------------------------------
            var consult   = Svc("Первичная консультация", "Диагностика", "scan", 0, 30,
                "Осмотр, панорамный снимок и план лечения с понятной сметой. Первый приём — бесплатно, без обязательств.");
            var hygiene   = Svc("Профессиональная гигиена", "Профилактика", "sparkles", 5500, 60,
                "Ультразвуковая чистка, Air Flow, полировка и фторирование. Удаляем налёт и камень, возвращаем зубам естественную белизну.");
            var caries    = Svc("Лечение кариеса", "Терапия", "tooth", 6500, 60,
                "Безболезненное лечение под современной анестезией, работа под микроскопом, световая пломба с идеальной цветопередачей.");
            var canals    = Svc("Лечение каналов", "Терапия", "shield", 12000, 90,
                "Эндодонтическое лечение под микроскопом за один визит: сохраняем зуб там, где другие предлагают удаление.");
            var whitening = Svc("Отбеливание зубов", "Эстетика", "smile", 18000, 90,
                "Фотоотбеливание ZOOM: на 6–8 тонов светлее за один приём, с защитой дёсен и без чувствительности.");
            var veneers   = Svc("Керамические виниры", "Эстетика", "crown", 28000, 90,
                "Тонкие виниры на основе керамики: исправляют форму, цвет и положение зубов. Цена указана за один зуб.");
            var implant   = Svc("Имплантация зуба", "Хирургия", "implant", 45000, 120,
                "Установка импланта Osstem (Корея) под контролем 3D-томографии. Гарантия на имплант — 10 лет.");
            var extraction = Svc("Удаление зуба", "Хирургия", "syringe", 4500, 30,
                "Атравматичное удаление, в том числе зубов мудрости. Обезболивание по принципу «вы ничего не почувствуете».");
            var braces    = Svc("Брекеты и элайнеры", "Ортодонтия", "align", 95000, 60,
                "Исправление прикуса брекетами или прозрачными элайнерами. Цена указана за курс лечения; рассрочка 0%.");
            var kids      = Svc("Детская стоматология", "Дети", "baby", 3500, 45,
                "Принимаем с 4 лет. Игровой формат, «серебрение» без бормашины, лечение без слёз и страха.");
            var zirconia  = Svc("Циркониевая коронка", "Ортопедия", "crown", 32000, 90,
                "Прочные и неотличимые от настоящих зубов коронки на цифровом оборудовании за 3 дня.");

            var allServices = new[] { consult, hygiene, caries, canals, whitening, veneers, implant, extraction, braces, kids, zirconia };
            db.Services.AddRange(allServices);
            await db.SaveChangesAsync();

            // ---- staff accounts -------------------------------------------------------------------------
            var admin   = await CreateUser(users, "admin@clinic.demo", "Ирина Громова", "+7 (495) 000-00-01", RoleIds.Admin);
            var manager = await CreateUser(users, "manager@clinic.demo", "Павел Захаров", "+7 (495) 000-00-02", RoleIds.Manager);

            var doctorA = await CreateDoctor(users, db, "doctor@clinic.demo", "Елена Сорокина", "Терапевт-эндодонтист", 12,
                "Лечит сложные случаи под микроскопом и возвращает уверенность пациентам, которые годами откладывали визит к стоматологу. Лауреат премии «Дантист года».",
                "/img/doc1.jpg", new[] { consult, hygiene, caries, canals, kids });
            var doctorB = await CreateDoctor(users, db, "doctor2@clinic.demo", "Максим Беляев", "Хирург-имплантолог", 9,
                "Более 1500 установленных имплантов. Работает с 3D-навигацией и добивается минимальной травматичности — отёка почти нет.",
                "/img/doc3.jpg", new[] { consult, implant, extraction, zirconia });
            var doctorC = await CreateDoctor(users, db, "doctor3@clinic.demo", "Тимур Хакимов", "Ортодонт, эстетическая стоматология", 15,
                "Сертифицированный врач Invisalign. Создаёт улыбки, которыми гордятся: брекеты, элайнеры, виниры и отбеливание.",
                "/img/doc4.jpg", new[] { consult, braces, veneers, whitening, hygiene });
            await db.SaveChangesAsync();

            // ---- patients -------------------------------------------------------------------------------
            var anna  = await CreateUser(users, "client@clinic.demo", "Анна Кузнецова", "+7 (916) 111-22-33", RoleIds.Client);
            var igor  = await CreateUser(users, "igor@clinic.demo", "Игорь Васильев", "+7 (903) 222-33-44", RoleIds.Client);
            var maria = await CreateUser(users, "maria@clinic.demo", "Мария Орлова", "+7 (925) 333-44-55", RoleIds.Client);
            var oleg  = await CreateUser(users, "oleg@clinic.demo", "Олег Петров", "+7 (999) 444-55-66", RoleIds.Client);
            var sveta = await CreateUser(users, "sveta@clinic.demo", "Светлана Ким", "+7 (977) 555-66-77", RoleIds.Client);
            // Random bookings go to the other patients, so the demo patient's cabinet stays tidy.
            var clients = new[] { igor, maria, oleg, sveta };

            // ---- schedule -------------------------------------------------------------------------------
            var staff = await db.Staffs.Include(s => s.Services).ToListAsync();
            var rnd = new Random(42);
            var today = DateTime.Today;
            int[] hours = { 9, 10, 11, 12, 14, 15, 16, 17 };

            foreach (var doc in staff)
            {
                for (int d = 0; d < 14; d++)
                {
                    var day = today.AddDays(d);
                    if (day.DayOfWeek == DayOfWeek.Sunday) continue;

                    foreach (var h in hours)
                    {
                        var start = day.AddHours(h);
                        if (start <= DateTime.Now.AddMinutes(30)) continue;

                        var slot = new Appointment { StaffId = doc.Id, StartAt = start, Duration = 60 };
                        if (rnd.NextDouble() < 0.3)
                        {
                            slot.ClientId = clients[rnd.Next(clients.Length)].Id;
                            slot.Services.Add(doc.Services[rnd.Next(doc.Services.Count)]);
                        }
                        db.Appointments.Add(slot);
                    }
                }
            }

            // Demo patient: one upcoming visit, two finished visits with recommendations.
            var annaNext = new Appointment
            {
                StaffId = doctorA.Id, ClientId = anna.Id, Duration = 60,
                StartAt = today.AddDays(1).AddHours(13), Services = { hygiene }
            };
            db.Appointments.Add(annaNext);
            db.Appointments.Add(new Appointment
            {
                StaffId = doctorA.Id, ClientId = anna.Id, Duration = 60,
                StartAt = today.AddDays(-21).AddHours(11), Services = { caries },
                Recommendation = "Не есть и не пить 2 часа после лечения. Через две недели — контрольный осмотр. Использовать зубную пасту с фтором, ирригатор — по желанию."
            });
            db.Appointments.Add(new Appointment
            {
                StaffId = doctorC.Id, ClientId = anna.Id, Duration = 45,
                StartAt = today.AddDays(-60).AddHours(15), Services = { consult },
                Recommendation = "Показана профессиональная гигиена каждые 6 месяцев. Ортодонтическое лечение не требуется.",
                DurationChangeReason = "Приём завершён раньше: проблем не выявлено"
            });
            db.Appointments.Add(new Appointment
            {
                StaffId = doctorB.Id, ClientId = oleg.Id, Duration = 90,
                StartAt = today.AddDays(-14).AddHours(10), Services = { implant },
                Recommendation = "Холод на область операции в первые сутки, мягкая пища 3 дня, полоскания хлоргексидином. Снятие швов — через 10 дней."
            });

            // An appointment that is happening right now, so the doctor dashboard has something to show.
            var now = DateTime.Now;
            db.Appointments.Add(new Appointment
            {
                StaffId = doctorA.Id, ClientId = maria.Id, Duration = 60,
                StartAt = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0).AddMinutes(-15),
                Services = { caries }
            });

            // ---- reviews --------------------------------------------------------------------------------
            db.Reviews.AddRange(
                new Review { ProfileId = anna.Id, Rating = 5, IsVisible = true, CreatedAt = today.AddDays(-18),
                    Text = "Я боялась стоматологов с детства, но здесь впервые лечила зуб без страха. Елена Сергеевна всё объясняла, анестезия подействовала мгновенно. Спасибо за заботу!" },
                new Review { ProfileId = igor.Id, Rating = 5, IsVisible = true, CreatedAt = today.AddDays(-30),
                    Text = "Ставил имплант у Максима. Операция заняла около часа, на следующий день уже был на работе. Цена из сметы не изменилась ни на рубль." },
                new Review { ProfileId = maria.Id, Rating = 5, IsVisible = true, CreatedAt = today.AddDays(-9),
                    Text = "Очень удобная онлайн-запись: выбрала врача и время за минуту. В клинике чисто, никаких очередей, приняли ровно в назначенное время." },
                new Review { ProfileId = oleg.Id, Rating = 4, IsVisible = true, CreatedAt = today.AddDays(-4),
                    Text = "Профессиональная гигиена прошла комфортно. Единственное пожелание — больше вечерних окон в расписании. Но врачи отличные." },
                new Review { ProfileId = sveta.Id, Rating = 5, IsVisible = false, CreatedAt = today.AddDays(-1),
                    Text = "Хорошая клиника и очень внимательный врач, всё объяснил по снимку. Единственное — пришлось немного подождать в холле." });

            await db.SaveChangesAsync();
        }

        private static Service Svc(string title, string category, string icon, double price, int minutes, string description) =>
            new() { Title = title, Category = category, Icon = icon, Price = price, DurationMinutes = minutes, Description = description };

        private static async Task<Profile> CreateUser(UserManager<Profile> users, string email, string fullName, string phone, long roleId)
        {
            var profile = new Profile
            {
                UserName = email, Email = email, EmailConfirmed = true,
                PhoneNumber = phone, FullName = fullName, RoleId = roleId
            };
            var result = await users.CreateAsync(profile, DemoPassword);
            if (!result.Succeeded)
                throw new InvalidOperationException($"Cannot seed user {email}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            return profile;
        }

        private static async Task<Staff> CreateDoctor(UserManager<Profile> users, DatabaseContext db, string email, string fullName,
            string specialty, int experience, string bio, string photo, IEnumerable<Service> provided)
        {
            var profile = await CreateUser(users, email, fullName, "+7 (495) 000-00-10", RoleIds.Doctor);
            var staff = new Staff
            {
                Profile = profile, ExternalLogin = email, FullName = fullName, Specialty = specialty,
                ExperienceYears = experience, Bio = bio, PhotoUrl = photo, Services = provided.ToList()
            };
            db.Staffs.Add(staff);
            await db.SaveChangesAsync();
            return staff;
        }
    }
}
