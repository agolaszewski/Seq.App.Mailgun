using System;
using System.Linq;
using FluentEmail.Core;
using FluentEmail.Core.Models;
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

        [SeqAppSetting(DisplayName = "To Emails", InputType = SettingInputType.LongText)]
        public string ToEmail { get; set; }

        [SeqAppSetting(DisplayName = "Subject")]
        public string Subject { get; set; }

        [SeqAppSetting(DisplayName = "Body", InputType = SettingInputType.LongText, IsOptional = true)]
        public string Body { get; set; }

        public void On(Event<LogEventData> evt)
        {
            SendEmail(evt.Data.RenderedMessage);
        }

        public void SendEmail(string renderedMessage)
        {
            var recipients = ToEmail.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(emailAddress => new Address(emailAddress)).ToList();

            var sender = new MailgunSender(Domain, ApiKey);
            var email = Email.From(FromEmail).To(recipients).Subject(Subject).Body(renderedMessage + Environment.NewLine + Body);
            var response = sender.Send(email);

            if(!response.Successful)
            {
                throw new Exception(string.Join(" ; ", response.ErrorMessages));
            }
        }
    }
}