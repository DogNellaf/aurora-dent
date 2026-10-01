namespace DentalClinic.Services
{
    /// <summary>Static facts about the clinic, bound from the "Clinic" configuration section.</summary>
    public class ClinicOptions
    {
        public const string Section = "Clinic";

        public string Name { get; set; } = "Аврора Дент";
        public string Address { get; set; } = "г. Москва, ул. Примерная, д. 1";
        public string Phone { get; set; } = "+7 (495) 000-00-00";

        /// <summary>
        /// Public address of the site, for example https://clinic.example. Links in emails are built from it,
        /// never from the Host header of a request. Empty means "use the request", which is only safe when
        /// AllowedHosts is restricted.
        /// </summary>
        public string PublicUrl { get; set; } = string.Empty;

        /// <summary>IANA time zone of the clinic. Appointment times are stored as the clinic's local time.</summary>
        public string TimeZone { get; set; } = "Europe/Moscow";
    }
}
