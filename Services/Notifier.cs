using System.Net;
using System.Text;
using DentalClinic.Infrastructure;
using DentalClinic.Localization;
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
        private readonly IEmailDispatcher _dispatcher;
        private readonly ClinicOptions _clinic;
        private readonly ICalendarExporter _calendar;

        public Notifier(IEmailDispatcher dispatcher, IOptions<ClinicOptions> clinic, ICalendarExporter calendar)
        {
            _dispatcher = dispatcher;
            _clinic = clinic.Value;
            _calendar = calendar;
        }

        public Task BookingConfirmedAsync(Appointment appointment, Profile client)
        {
            var ics = _calendar.Export(appointment);
            var html = Layout(T("Вы записаны на приём"),
                $"<p>{Enc(T("{0}, ждём вас в клинике.", client.PublicName))}</p>" + Details(appointment) +
                $"<p>{Enc(T("Файл календаря во вложении. Отменить запись можно в личном кабинете не позднее чем за 2 часа до приёма."))}</p>");

            return SafeSend(new EmailMessage(client.Email!, client.DisplayName,
                T("Запись на {0} в {1}", Fmt.DayMonth(appointment.StartAt), Fmt.Time(appointment.StartAt)), html,
                new EmailAttachment("appointment.ics", "text/calendar", Encoding.UTF8.GetBytes(ics))));
        }

        public Task BookingCancelledAsync(Appointment appointment, Profile client)
        {
            var html = Layout(T("Запись отменена"),
                $"<p>{Enc(T("{0}, запись отменена.", client.PublicName))}</p>" + Details(appointment) +
                $"<p>{Enc(T("Выбрать новое время можно на сайте в разделе «Запись на приём»."))}</p>");

            return SafeSend(new EmailMessage(client.Email!, client.DisplayName,
                T("Отмена записи на {0}", Fmt.DayMonth(appointment.StartAt)), html));
        }

        public Task PasswordResetAsync(Profile profile, string link)
        {
            var html = Layout(T("Сброс пароля"),
                $"<p>{Enc(T("Для создания нового пароля перейдите по ссылке."))}</p><p><a href=\"{Enc(link)}\">{Enc(T("Сбросить пароль"))}</a></p>" +
                $"<p>{Enc(T("Если сброс не запрашивался, письмо можно проигнорировать."))}</p>");

            return SafeSend(new EmailMessage(profile.Email!, profile.DisplayName, T("Сброс пароля"), html));
        }

        private string Details(Appointment a)
        {
            var services = a.Services.Count > 0 ? string.Join(", ", a.Services.Select(s => T(s.Title))) : T("Приём врача");
            return "<table cellpadding=\"6\">" +
                   Row(T("Дата и время"), $"{Fmt.Date(a.StartAt)}, {Fmt.Time(a.StartAt)}") +
                   Row(T("Врач"), T(a.Staff?.DisplayName ?? "")) +
                   Row(T("Услуга"), services) +
                   Row(T("Адрес"), T(_clinic.Address)) +
                   "</table>";
        }

        private static string T(string key) => Translations.Get(key);
        private static string T(string key, params object[] args) => string.Format(System.Globalization.CultureInfo.CurrentCulture, Translations.Get(key), args);

        private static string Row(string name, string value) => $"<tr><td><b>{Enc(name)}</b></td><td>{Enc(value)}</td></tr>";

        private string Layout(string title, string body) =>
            $"<div style=\"font-family:Arial,sans-serif;max-width:560px\"><h2 style=\"color:#0f7a77\">{Enc(title)}</h2>{body}" +
            $"<hr><p style=\"color:#777;font-size:12px\">{Enc(T(_clinic.Name))}, {Enc(T(_clinic.Address))}, {Enc(_clinic.Phone)}</p></div>";

        private static string Enc(string text) => WebUtility.HtmlEncode(text);

        private Task SafeSend(EmailMessage message)
        {
            _dispatcher.Enqueue(message);
            return Task.CompletedTask;
        }
    }
}
