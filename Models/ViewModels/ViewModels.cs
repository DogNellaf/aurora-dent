namespace DentalClinic.Models.ViewModels
{
    public record ReviewCard(long Id, string Author, string Text, int Rating, DateTime CreatedAt, bool IsVisible, long ProfileId);

    public class HomeViewModel
    {
        public List<ReviewCard> Reviews { get; set; } = new();
        public List<Service> Services { get; set; } = new();
        public List<Staff> Doctors { get; set; } = new();
        public int PatientsCount { get; set; }
        public double AverageRating { get; set; }
    }

    public class ServiceDetailViewModel
    {
        public Service Service { get; set; } = null!;
        public List<Staff> Doctors { get; set; } = new();
        public List<Service> Related { get; set; } = new();
    }

    public class DoctorSlots
    {
        public Staff Doctor { get; set; } = null!;
        public Appointment? NextSlot { get; set; }
        public int FreeCount { get; set; }
    }

    public class ScheduleViewModel
    {
        public List<Service> Services { get; set; } = new();
        public long? ServiceId { get; set; }
        public Service? SelectedService { get; set; }
        public List<DoctorSlots> Doctors { get; set; } = new();
        public Staff? SelectedDoctor { get; set; }
        public List<IGrouping<DateTime, Appointment>> Days { get; set; } = new();
        public bool IsAuthenticated { get; set; }
        public bool CanBook { get; set; }
    }

    /// <summary>An appointment together with display names, for the cabinet lists.</summary>
    public class AppointmentRow
    {
        public Appointment Appointment { get; set; } = null!;
        public string ClientName { get; set; } = string.Empty;
    }

    public class AdminOverview
    {
        public int Upcoming { get; set; }
        public int FreeSlots { get; set; }
        public int Clients { get; set; }
        public int Doctors { get; set; }
        public int PendingReviews { get; set; }
        public List<AppointmentRow> NextAppointments { get; set; } = new();
    }

    public class AppointmentEditViewModel
    {
        public Appointment Appointment { get; set; } = new();
        public List<Staff> Staff { get; set; } = new();
        public List<Profile> Clients { get; set; } = new();
        public bool IsNew { get; set; }
    }

    public class DoctorDashboard
    {
        public Staff Staff { get; set; } = null!;
        public AppointmentRow? Current { get; set; }
        public AppointmentRow? Next { get; set; }
        public List<AppointmentRow> Today { get; set; } = new();
        public int UpcomingCount { get; set; }
        public int PatientsCount { get; set; }
    }
}
