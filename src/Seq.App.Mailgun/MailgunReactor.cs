﻿using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Text;
using FluentEmail.Core;
using FluentEmail.Core.Models;
using FluentEmail.Mailgun;
using HandlebarsDotNet;
using Seq.Apps;
using Seq.Apps.LogEvents;

namespace Seq.App.Mailgun
{
    [SeqApp("Mailgun",
    Description = "Uses a mailgun to send events as email.")]
    public class MailgunReactor : SeqApp, ISubscribeTo<LogEventData>
    {
        private const string DEFAULT_SUBJECT_TEMPLATE = @"[{{$Level}}] {{{$Message}}} (via Seq)";
        private const int MAX_SUBJECT_LENGTH = 130;

        private Lazy<Func<object, string>> _subjectTemplate;
        private Lazy<Func<object, string>> _bodyTemplate;

        [SeqAppSetting(DisplayName = "Domain")]
        public string Domain { get; set; }

        [SeqAppSetting(DisplayName = "Api Key")]
        public string ApiKey { get; set; }

        [SeqAppSetting(DisplayName = "From")]
        public string From { get; set; }

        [SeqAppSetting(DisplayName = "To", InputType = SettingInputType.LongText)]
        public string To { get; set; }

        [SeqAppSetting(DisplayName = "Subject Template", IsOptional = true)]
        public string SubjectTemplate { get; set; }

        [SeqAppSetting(DisplayName = "Body", InputType = SettingInputType.LongText, IsOptional = true)]
        public string BodyTemplate { get; set; }

        [SeqAppSetting(DisplayName = "Additional Info", InputType = SettingInputType.LongText, IsOptional = true)]
        public string AdditionalInfo { get; set; }

        static MailgunReactor()
        {
            HandlebarsHelpers.Register();
        }

        public MailgunReactor()
        {
            _subjectTemplate = new Lazy<Func<object, string>>(() =>
            {
                var subjectTemplate = SubjectTemplate;
                if (string.IsNullOrEmpty(subjectTemplate))
                {
                    subjectTemplate = DEFAULT_SUBJECT_TEMPLATE;
                }

                return Handlebars.Compile(subjectTemplate);
            });

            _bodyTemplate = new Lazy<Func<object, string>>(() =>
            {
                var bodyTemplate = BodyTemplate;
                if (string.IsNullOrEmpty(bodyTemplate))
                {
                    bodyTemplate = File.ReadAllText(@"DefaultBody.hbs", Encoding.UTF8);
                }

                return Handlebars.Compile(bodyTemplate);
            });
        }

        public void On(Event<LogEventData> evt)
        {
            var recipients = To.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                 .Select(emailAddress => new Address(emailAddress)).ToList();

            var body = FormatTemplate(_bodyTemplate.Value, evt, Host);
            var subject = FormatTemplate(_subjectTemplate.Value, evt, Host).Trim().Replace("\r", "").Replace("\n", "");

            if (subject.Length > MAX_SUBJECT_LENGTH)
            {
                subject = subject.Substring(0, MAX_SUBJECT_LENGTH);
            }

            SendEmail(recipients, subject, body);
        }

        private void SendEmail(List<Address> recipients, string subject, string body)
        {
            var email = Email.From(From).To(recipients).Subject(subject).Body(body, isHtml: true);

            var sender = new MailgunSender(Domain, ApiKey);
            var response = sender.Send(email);

            if (!response.Successful)
            {
                throw new Exception(string.Join(" ; ", response.ErrorMessages));
            }
        }

        public string FormatTemplate(Func<object, string> template, Event<LogEventData> evt, Host host)
        {
            var properties = (IDictionary<string, object>)ToDynamic(evt.Data.Properties ?? new Dictionary<string, object>());
            properties.Add("Info", AdditionalInfo);

            var payload = (IDictionary<string, object>)ToDynamic(new Dictionary<string, object>
            {
                { "$Id",                  evt.Id },
                { "$UtcTimestamp",        evt.TimestampUtc },
                { "$LocalTimestamp",      evt.Data.LocalTimestamp },
                { "$Level",               evt.Data.Level },
                { "$MessageTemplate",     evt.Data.MessageTemplate },
                { "$Message",             evt.Data.RenderedMessage },
                { "$Exception",           evt.Data.Exception },
                { "$Properties",          properties },
                { "$EventType",           "$" + evt.EventType.ToString("X8") },
                { "$Instance",            host.InstanceName },
                { "$ServerUri",           host.BaseUri?.FirstOrDefault() }
            });

            foreach (var property in properties)
            {
                payload[property.Key] = property.Value;
            }

            return template(payload);
        }

        private static object ToDynamic(object o)
        {
            if (o is IEnumerable<KeyValuePair<string, object>> dictionary)
            {
                var result = new ExpandoObject();
                var asDict = (IDictionary<string, object>)result;
                foreach (var kvp in dictionary)
                    asDict.Add(kvp.Key, ToDynamic(kvp.Value));
                return result;
            }

            if (o is IEnumerable<object> enumerable)
            {
                return enumerable.Select(ToDynamic).ToArray();
            }

            return o;
        }
    }
}