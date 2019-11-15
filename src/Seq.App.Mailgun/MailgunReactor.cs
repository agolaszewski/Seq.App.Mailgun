using System;
using FluentEmail.Core;
using FluentEmail.Mailgun;
using Seq.Apps;
using Seq.Apps.LogEvents;

namespace Seq.App.Mailgun
{
    [SeqApp("Mailgun",
    Description = "Uses a mailgun to send events as email.")]
    public class MailgunReactor : SeqApp, ISubscribeTo<LogEventData>
    {
        [SeqAppSetting(DisplayName = "Domain")]
        public string Domain { get; set; }

        [SeqAppSetting(DisplayName = "Api Key")]
        public string ApiKey { get; set; }

        [SeqAppSetting(DisplayName = "From Email")]
        public string FromEmail { get; set; }

        [SeqAppSetting(DisplayName = "To Email")]
        public string ToEmail { get; set; }

        [SeqAppSetting(DisplayName = "Subject")]
        public string Subject { get; set; }

        public void On(Event<LogEventData> evt)
        {
            SendEmail(evt.Data.RenderedMessage);
        }

        public void SendEmail(string body)
        {
            var sender = new MailgunSender(Domain, ApiKey);
            var email = Email.From(FromEmail).To(ToEmail).Subject(Subject).Body(body);
            var response = sender.Send(email);

            if(!response.Successful)
            {
                throw new Exception(string.Join(" ; ", response.ErrorMessages));
            }
        }
    }
}