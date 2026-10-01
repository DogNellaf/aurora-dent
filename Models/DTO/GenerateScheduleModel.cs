using System.ComponentModel.DataAnnotations;

namespace DentalClinic.Models.DTO
{
    /// <summary>Parameters for creating many free slots at once.</summary>
    public class GenerateScheduleModel : IValidatableObject
    {
        public const int MaxDays = 62;

        /// <summary>The doctor to create slots for; null means every doctor (administrators only).</summary>
        public long? StaffId { get; set; }

        [Required(ErrorMessage = "Укажите первый день")]
        [DataType(DataType.Date)]
        public DateTime? From { get; set; }

        [Required(ErrorMessage = "Укажите последний день")]
        [DataType(DataType.Date)]
        public DateTime? To { get; set; }

        [Required(ErrorMessage = "Укажите начало работы")]
        public TimeSpan? StartTime { get; set; } = new TimeSpan(9, 0, 0);

        [Required(ErrorMessage = "Укажите конец работы")]
        public TimeSpan? EndTime { get; set; } = new TimeSpan(18, 0, 0);

        [Range(15, 240, ErrorMessage = "Длительность окна от 15 до 240 минут")]
        public int SlotMinutes { get; set; } = 60;

        /// <summary>Minutes between slots (a break for the doctor).</summary>
        [Range(0, 120, ErrorMessage = "Перерыв от 0 до 120 минут")]
        public int BreakMinutes { get; set; }

        public bool SkipWeekends { get; set; } = true;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (From is { } from && To is { } to)
            {
                if (to < from)
                    yield return new ValidationResult("Последний день раньше первого", new[] { nameof(To) });
                else if ((to - from).TotalDays + 1 > MaxDays)
                    yield return new ValidationResult($"Не более {MaxDays} дней за один раз", new[] { nameof(To) });
            }

            if (StartTime is { } start && EndTime is { } end && end <= start)
                yield return new ValidationResult("Конец работы должен быть позже начала", new[] { nameof(EndTime) });
        }
    }
}
