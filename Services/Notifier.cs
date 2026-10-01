using System.Net;
using System.Text;
using DentalClinic.Models;
using Microsoft.Extensions.Options;

namespace DentalClinic.Services
{
    /// <summary>Composes the emails the clinic sends and hands them to the <see cref="IEmailSender"/>.</summary>
    public interface INotifier
    {
        Task BookingConfirmedAsync(Appointment appointment, Profile client);
        Task BookingCancelledAsync(Appointment appointment, Profile client);
        Task PasswordResetAsync(Profile profile, string link);
    }

    public sealed class Notifier : INotifier
    {
        private readonly IEmailSender _sender;
        private readonly ClinicOptions _clinic;
        private readonly ICalendarExporter _calendar;
        private readonly ILogger<Notifier> _logger;

        public Notifier(IEmailSender sender, IOptions<ClinicOptions> clinic, ICalendarExporter calendar, ILogger<Notifier> logger)
        {
            _sender = sender;
            _clinic = clinic.Value;
            _calendar = calendar;
            _logger = logger;
        }

        public Task BookingConfirmedAsync(Appointment appointment, Profile client)
        {
            var ics = _calendar.Export(appointment);
            var html = Layout("Вы записаны на приём",
                $"<p>{Enc(client.PublicName)}, ждём вас в клинике.</p>" + Details(appointment) +
                "<p>Файл календаря во вложении. Отменить запись можно в личном кабинете не позднее чем за 2 часа до приёма.</p>");

            return SafeSend(new EmailMessage(client.Email!, client.DisplayName,
                $"Запись на {appointment.StartAt:d MMMM} в {appointment.StartAt:HH:mm}", html,
                new EmailAttachment("appointment.ics", "text/calendar", Encoding.UTF8.GetBytes(ics))));
        }

        public Task BookingCancelledAsync(Appointment appointment, Profile client)
        {
            var html = Layout("Запись отменена",
                $"<p>{Enc(client.PublicName)}, запись отменена.</p>" + Details(appointment) +
                "<p>Выбрать новое время можно на сайте в разделе «Запись на приём».</p>");

            return SafeSend(new EmailMessage(client.Email!, client.DisplayName,
                $"Отмена записи на {appointment.StartAt:d MMMM}", html));
        }

        public Task PasswordResetAsync(Profile profile, string link)
        {
            var html = Layout("Сброс пароля",
                $"<p>Для создания нового пароля перейдите по ссылке.</p><p><a href=\"{Enc(link)}\">Сбросить пароль</a></p>" +
                "<p>Если сброс не запрашивался, письмо можно проигнорировать.</p>");

            return SafeSend(new EmailMessage(profile.Email!, profile.DisplayName, "Сброс пароля", html));
        }

        private string Details(Appointment a)
        {
            var services = a.Services.Count > 0 ? string.Join(", ", a.Services.Select(s => s.Title)) : "Приём врача";
            return "<table cellpadding=\"6\">" +
                   Row("Дата и время", $"{a.StartAt:d MMMM yyyy}, {a.StartAt:HH:mm}") +
                   Row("Врач", a.Staff?.DisplayName ?? "") +
                   Row("Услуга", services) +
                   Row("Адрес", _clinic.Address) +
                   "</table>";
        }

        private static string Row(string name, string value) => $"<tr><td><b>{Enc(name)}</b></td><td>{Enc(value)}</td></tr>";

        private string Layout(string title, string body) =>
            $"<div style=\"font-family:Arial,sans-serif;max-width:560px\"><h2 style=\"color:#0f7a77\">{Enc(title)}</h2>{body}" +
            $"<hr><p style=\"color:#777;font-size:12px\">{Enc(_clinic.Name)}, {Enc(_clinic.Address)}, {Enc(_clinic.Phone)}</p></div>";

        private static string Enc(string text) => WebUtility.HtmlEncode(text);

        /// <summary>A failing mail server must never break booking, cancelling or password reset.</summary>
        private async Task SafeSend(EmailMessage message)
        {
            try
            {
                await _sender.SendAsync(message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not send email '{Subject}' to {Recipient}", message.Subject, message.To);
            }
        }
    }
}
