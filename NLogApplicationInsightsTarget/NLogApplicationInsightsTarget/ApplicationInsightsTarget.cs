using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using NLog;
using NLog.Common;
using NLog.Config;
using NLog.Targets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NLogApplicationInsightsTarget
{
    [Target("ApplicationInsightsTarget")]
    public class ApplicationInsightsTarget : TargetWithLayout
    {
        private DateTime lastLogEventTime;

        internal TelemetryClient TelemetryClient { get; private set; }

        [RequiredParameter]
        public string InstrumentationKey { get; set; }

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
            TelemetryClient = new TelemetryClient(new TelemetryConfiguration(InstrumentationKey));

        }

        protected override void Write(LogEventInfo logEvent)
        {
            lastLogEventTime = DateTime.UtcNow;

            Track(logEvent);
        }

        protected override void Write(IList<AsyncLogEventInfo> logEvents)
        {
            lastLogEventTime = DateTime.UtcNow;

            foreach (var logEvent in logEvents)
            {
                Exception exception = null;

                try
                {
                    Track(logEvent.LogEvent);
                }
                catch (Exception ex)
                {
                    exception = ex;
                }

                logEvent.Continuation(exception);
            }
        }

        private void Track(LogEventInfo logEvent)
        {
            var props = logEvent.Properties?.ToDictionary(k => k.Key is string ? (string)k.Key : k.Key.ToString(), k => k.Value is string ? (string)k.Value : k.Value.ToString());

            TelemetryClient.TrackEvent(props["event"] ?? string.Empty, props ?? new Dictionary<string, string>());
        }
    }
}
