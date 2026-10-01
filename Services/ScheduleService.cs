using DentalClinic.Models;
using DentalClinic.Models.DTO;
using DentalClinic.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace DentalClinic.Services
{
    public record GenerateResult(int Created, int SkippedExisting);

    public interface IScheduleService
    {
        /// <summary>Data for the booking page: services, doctors with their next free time, and the free times of one doctor.</summary>
        Task<ScheduleViewModel> BrowseAsync(long? serviceId, long? staffId, Profile? viewer);

        /// <summary>Creates free slots, leaving times that already exist untouched.</summary>
        Task<GenerateResult> GenerateAsync(GenerateScheduleModel model);
    }

    public sealed class ScheduleService : IScheduleService
    {
        private const int MaxSlotsPerDay = 100;

        private readonly DatabaseContext _db;
        private readonly IClinicClock _clock;
        private readonly ILogger<ScheduleService> _logger;

        public ScheduleService(DatabaseContext db, IClinicClock clock, ILogger<ScheduleService> logger)
        {
            _db = db;
            _clock = clock;
            _logger = logger;
        }

        public async Task<ScheduleViewModel> BrowseAsync(long? serviceId, long? staffId, Profile? viewer)
        {
            var now = _clock.Now;
            var model = new ScheduleViewModel
            {
                Services = await _db.Services.OrderBy(s => s.Category).ThenBy(s => s.Title).ToListAsync(),
                ServiceId = serviceId,
                IsAuthenticated = viewer != null,
                CanBook = viewer?.IsClient == true
            };
            model.SelectedService = serviceId == null ? null : model.Services.FirstOrDefault(s => s.Id == serviceId);

            var doctorsQuery = _db.Staffs.Include(s => s.Services).AsQueryable();
            if (model.SelectedService != null)
            {
                var id = model.SelectedService.Id;
                doctorsQuery = doctorsQuery.Where(s => s.Services.Any(x => x.Id == id));
            }
            var doctors = await doctorsQuery.OrderBy(s => s.Id).ToListAsync();

            var ids = doctors.Select(d => d.Id).ToList();
            var free = await _db.Appointments
                .Where(a => ids.Contains(a.StaffId) && a.ClientId == null && a.StartAt > now)
                .OrderBy(a => a.StartAt)
                .ToListAsync();

            model.Doctors = doctors.Select(d =>
            {
                var slots = free.Where(a => a.StaffId == d.Id).ToList();
                return new DoctorSlots { Doctor = d, NextSlot = slots.FirstOrDefault(), FreeCount = slots.Count };
            }).ToList();

            if (staffId != null)
            {
                model.SelectedDoctor = doctors.FirstOrDefault(d => d.Id == staffId);
                if (model.SelectedDoctor != null)
                    model.Days = free.Where(a => a.StaffId == staffId).GroupBy(a => a.StartAt.Date).ToList();
            }

            return model;
        }

        public async Task<GenerateResult> GenerateAsync(GenerateScheduleModel model)
        {
            var from = model.From!.Value.Date;
            var to = model.To!.Value.Date;
            var start = model.StartTime!.Value;
            var end = model.EndTime!.Value;
            var step = TimeSpan.FromMinutes(model.SlotMinutes + model.BreakMinutes);
            var length = TimeSpan.FromMinutes(model.SlotMinutes);
            var now = _clock.Now;

            var staffIds = model.StaffId is { } one
                ? new List<long> { one }
                : await _db.Staffs.Select(s => s.Id).ToListAsync();

            var created = 0;
            var skipped = 0;

            foreach (var staffId in staffIds)
            {
                var existing = (await _db.Appointments
                    .Where(a => a.StaffId == staffId && a.StartAt >= from && a.StartAt < to.AddDays(1))
                    .Select(a => a.StartAt).ToListAsync()).ToHashSet();

                for (var day = from; day <= to; day = day.AddDays(1))
                {
                    if (model.SkipWeekends && day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;

                    var perDay = 0;
                    for (var t = start; t + length <= end && perDay < MaxSlotsPerDay; t += step, perDay++)
                    {
                        var startAt = day + t;
                        if (startAt <= now) continue;
                        if (existing.Contains(startAt)) { skipped++; continue; }

                        _db.Appointments.Add(new Appointment { StaffId = staffId, StartAt = startAt, Duration = (short)model.SlotMinutes });
                        created++;
                    }
                }
            }

            await _db.SaveChangesAsync();
            _logger.LogInformation("Generated {Created} slots ({Skipped} already existed) for {Staff} from {From:d} to {To:d}",
                created, skipped, model.StaffId?.ToString() ?? "all doctors", from, to);
            return new GenerateResult(created, skipped);
        }
    }
}
