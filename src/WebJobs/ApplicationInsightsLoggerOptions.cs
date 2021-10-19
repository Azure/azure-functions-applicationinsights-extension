// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.ApplicationInsights.WindowsServer.Channel.Implementation;
using Microsoft.Azure.WebJobs.Hosting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Logging.ApplicationInsights
{
    public class ApplicationInsightsLoggerOptions : IOptionsFormatter
    {
        /// <summary>
        /// Gets or sets Application Insights instrumentation key.
        /// </summary>
        public string InstrumentationKey { get; set; }

        /// <summary>
        /// Gets or sets Application Insights connection string. If set, this will
        /// take precedence over the InstrumentationKey and overwrite it.
        /// </summary>
        public string ConnectionString { get; set; }

        /// <summary>
        /// Gets or sets sampling settings.
        /// </summary>
        public SamplingPercentageEstimatorSettings SamplingSettings { get; set; }

        /// <summary>
        /// Gets or sets excluded types for sampling.
        /// </summary>
        public string SamplingExcludedTypes { get; set; }

        /// <summary>
        /// Gets or sets included types for sampling.
        /// </summary>
        public string SamplingIncludedTypes { get; set; }

        /// <summary>
        /// Gets or sets authentication key for Quick Pulse (Live Metrics).
        /// </summary>
        [Obsolete("Use LiveMetricsAuthenticationApiKey instead.")]
        public string QuickPulseAuthenticationApiKey
        {
            get
            {
                return LiveMetricsAuthenticationApiKey;
            }
            set
            {
                LiveMetricsAuthenticationApiKey = value;
            }
        }

        /// <summary>
        /// Gets or sets authentication key for Live Metrics.
        /// </summary>
        public string LiveMetricsAuthenticationApiKey { get; set; }

        /// <summary>
        /// Gets or sets the time to delay Quick Pulse (Live Metrics) initialization. Delaying this initialization
        /// can result in decreased application startup time. Default value is 15 seconds.
        /// </summary>
        [Obsolete("Use LiveMetricsInitializationDelay instead.")]
        public TimeSpan QuickPulseInitializationDelay
        {
            get
            {
                return LiveMetricsInitializationDelay;
            }
            set
            {
                LiveMetricsInitializationDelay = value;
            }
        }

        /// <summary>
        /// Gets or sets the time to delay Live Metrics initialization. Delaying this initialization
        /// can result in decreased application startup time. Default value is 15 seconds.
        /// </summary>
        public TimeSpan LiveMetricsInitializationDelay { get; set; } = TimeSpan.FromSeconds(15);

        /// <summary>
        /// Gets or sets flag that enables Kudu performance counters collection.
        /// https://github.com/projectkudu/kudu/wiki/Perf-Counters-exposed-as-environment-variables.
        /// Enabled by default.
        /// </summary>
        public bool EnablePerformanceCountersCollection { get; set; } = true;

        /// <summary>
        /// Gets or sets the flag that enables live metrics collection.
        /// Enabled by default.
        /// </summary>
        public bool EnableLiveMetrics { get; set; } = true;

        /// <summary>
        /// Gets or sets the flag that enables dependency tracking.
        /// Enabled by default.
        /// </summary>
        public bool EnableDependencyTracking { get; set; } = true;

        /// <summary>
        /// Configuration for dependency tracking. The dependecny tracking configuration only takes effect if EnableDependencyTracking is set to true
        /// </summary>
        public DependencyTrackingOptions DependencyTrackingOptions { get; set; }

        /// <summary>
        /// Gets or sets HTTP request collection options. 
        /// </summary>
        public HttpAutoCollectionOptions HttpAutoCollectionOptions { get; set; } = new HttpAutoCollectionOptions();

        public string Format()
        {
            JObject sampling = null;
            if (SamplingSettings != null)
            {
                sampling = new JObject
                {
                    { nameof(SamplingPercentageEstimatorSettings.EvaluationInterval), SamplingSettings.EvaluationInterval },
                    { nameof(SamplingPercentageEstimatorSettings.InitialSamplingPercentage), SamplingSettings.InitialSamplingPercentage },
                    { nameof(SamplingPercentageEstimatorSettings.MaxSamplingPercentage), SamplingSettings.MaxSamplingPercentage },
                    { nameof(SamplingPercentageEstimatorSettings.MaxTelemetryItemsPerSecond), SamplingSettings.MaxTelemetryItemsPerSecond },
                    { nameof(SamplingPercentageEstimatorSettings.MinSamplingPercentage), SamplingSettings.MinSamplingPercentage },
                    { nameof(SamplingPercentageEstimatorSettings.MovingAverageRatio), SamplingSettings.MovingAverageRatio },
                    { nameof(SamplingPercentageEstimatorSettings.SamplingPercentageDecreaseTimeout), SamplingSettings.SamplingPercentageDecreaseTimeout },
                    { nameof(SamplingPercentageEstimatorSettings.SamplingPercentageIncreaseTimeout), SamplingSettings.SamplingPercentageIncreaseTimeout },
                };
            }

            JObject httpOptions = new JObject
            {
                { nameof(HttpAutoCollectionOptions.EnableHttpTriggerExtendedInfoCollection), HttpAutoCollectionOptions.EnableHttpTriggerExtendedInfoCollection },
                { nameof(HttpAutoCollectionOptions.EnableW3CDistributedTracing), HttpAutoCollectionOptions.EnableW3CDistributedTracing },
                { nameof(HttpAutoCollectionOptions.EnableResponseHeaderInjection), HttpAutoCollectionOptions.EnableResponseHeaderInjection }
            };

            JObject dependencyTrackingOptions = null;
            if (DependencyTrackingOptions != null)
            {
                dependencyTrackingOptions = new JObject
                {
                    { nameof(DependencyTrackingOptions.DisableRuntimeInstrumentation), DependencyTrackingOptions.DisableRuntimeInstrumentation },
                    { nameof(DependencyTrackingOptions.DisableDiagnosticSourceInstrumentation), DependencyTrackingOptions.DisableDiagnosticSourceInstrumentation},
                    { nameof(DependencyTrackingOptions.EnableLegacyCorrelationHeadersInjection), DependencyTrackingOptions.EnableLegacyCorrelationHeadersInjection},
                    { nameof(DependencyTrackingOptions.EnableRequestIdHeaderInjectionInW3CMode), DependencyTrackingOptions.EnableRequestIdHeaderInjectionInW3CMode},
                    { nameof(DependencyTrackingOptions.EnableSqlCommandTextInstrumentation), DependencyTrackingOptions.EnableSqlCommandTextInstrumentation},
                    { nameof(DependencyTrackingOptions.SetComponentCorrelationHttpHeaders), DependencyTrackingOptions.SetComponentCorrelationHttpHeaders},
                    { nameof(DependencyTrackingOptions.EnableAzureSdkTelemetryListener), DependencyTrackingOptions.EnableAzureSdkTelemetryListener}
                };
            }



            JObject options = new JObject
            {
                { nameof(SamplingSettings), sampling },
                { nameof(SamplingExcludedTypes), SamplingExcludedTypes },
                { nameof(SamplingIncludedTypes), SamplingIncludedTypes },
                { nameof(EnablePerformanceCountersCollection), EnablePerformanceCountersCollection },
                { nameof(HttpAutoCollectionOptions), httpOptions },
                { nameof(LiveMetricsInitializationDelay), LiveMetricsInitializationDelay },
                { nameof(EnableLiveMetrics), EnableLiveMetrics },
                { nameof(EnableDependencyTracking), EnableDependencyTracking },
                { nameof(DependencyTrackingOptions), dependencyTrackingOptions }
            };

            return options.ToString(Formatting.Indented);
        }
    }
}
