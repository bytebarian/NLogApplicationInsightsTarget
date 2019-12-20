using Microsoft.ApplicationInsights;
using NLog;
using NLog.Common;
using NLog.Layouts;
using NLog.Targets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NLogApplicationInsightsTarget
{
    [Target("ApplicationInsightsTarget")]
    public class NLogApplicationInsightsTarget : TargetWithLayout
    {
        private DateTime lastLogEventTime;
        private Layout instrumentationKeyLayout = string.Empty;

        internal TelemetryClient TelemetryClient { get; private set; }

        public NLogApplicationInsightsTarget()
        {
            Layout = @"${message}";
        }

        public string InstrumentationKey
        {
            get => (instrumentationKeyLayout as SimpleLayout)?.Text ?? null;
            set => instrumentationKeyLayout = value ?? string.Empty;
        }

        protected override void FlushAsync(AsyncContinuation asyncContinuation)
        {
            try
            {
                TelemetryClient.Flush();
                if (DateTime.UtcNow.AddSeconds(-30) > lastLogEventTime)
                {
                    // Nothing has been written, so nothing to wait for
                    asyncContinuation(null);
                }
                else
                {
                    // Documentation says it is important to wait after flush, else nothing will happen
                    // https://docs.microsoft.com/azure/application-insights/app-insights-api-custom-events-metrics#flushing-data
                    Task.Delay(TimeSpan.FromMilliseconds(500)).ContinueWith((task) => asyncContinuation(null));
                }
            }
            catch (Exception ex)
            {
                asyncContinuation(ex);
            }
        }

        protected override void InitializeTarget()
        {
            base.InitializeTarget();
#pragma warning disable CS0618 // Type or member is obsolete: TelemtryConfiguration.Active is used in TelemetryClient constructor.
            TelemetryClient = new TelemetryClient();
#pragma warning restore CS0618 // Type or member is obsolete

            string instrumentationKey = this.instrumentationKeyLayout.Render(LogEventInfo.CreateNullEvent());
            if (!string.IsNullOrWhiteSpace(instrumentationKey))
            {
                TelemetryClient.Context.InstrumentationKey = instrumentationKey;
            }
        }

        protected override void Write(LogEventInfo logEvent)
        {
            lastLogEventTime = DateTime.UtcNow;

            Track(logEvent);
        }

        private void Track(LogEventInfo logEvent)
        {
            string logMessage = Layout.Render(logEvent);
            var props = logEvent.Properties?.ToDictionary(k => k.Key is string ? (string)k.Key : k.Key.ToString(), k => k.Value is string ? (string)k.Value : k.Value.ToString());

            TelemetryClient.TrackEvent(logMessage, props ?? new Dictionary<string, string>());
        }
    }
}
