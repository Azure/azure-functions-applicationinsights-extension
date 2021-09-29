// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.ApplicationInsights.WindowsServer.Channel.Implementation;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.ApplicationInsights;
using Microsoft.Azure.WebJobs.Logging.ApplicationInsights;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Microsoft.Extensions.Hosting
{
    /// <summary>
    /// Extension methods for ApplicationInsights integration.
    /// </summary>
    internal static class ApplicationInsightsWebJobsBuilderExtensions
    {
        private const string ApplicationInsightsConnectionString = "APPLICATIONINSIGHTS_CONNECTION_STRING";
        private const string ApplicationInsightsInstrumentationKey = "APPINSIGHTS_INSTRUMENTATIONKEY";
        private static IConfiguration _configuration;

        /// <summary>
        /// Adds the ApplicationInsights extension to the provided <see cref="IWebJobsBuilder"/>.
        /// </summary>
        /// <param name="builder">The <see cref="IWebJobsBuilder"/> to configure.</param>
        public static IWebJobsBuilder AddApplicationInsights(this IWebJobsBuilder builder, WebJobsBuilderContext context)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            _configuration = context.Configuration;

            string connectionString = _configuration[ApplicationInsightsConnectionString];
            string instrumentationKey = _configuration[ApplicationInsightsInstrumentationKey];

            if (string.IsNullOrEmpty(connectionString) && string.IsNullOrEmpty(instrumentationKey))
            {
                return builder;
            }

            builder.Services.AddLogging((loggingBuilder) =>
            {
                loggingBuilder.AddApplicationInsightsWebJobs(o =>
                {
                    o.ConnectionString = connectionString;
                    o.InstrumentationKey = instrumentationKey;
                });
            });

            builder.AddExtension<ApplicationInsightsExtensionConfigProvider>()
                .ConfigureOptions<ApplicationInsightsLoggerOptions>((config, path, options) =>
                {
                    options.SamplingSettings = new SamplingPercentageEstimatorSettings
                    {
                        MaxTelemetryItemsPerSecond = 20
                    };

                    IConfigurationSection section = config.GetSection(path);
                    section.Bind(options);

                    // Sampling settings do not have a built-in "IsEnabled" value, so we are making our own.
                    string samplingPath = path + ":" + nameof(ApplicationInsightsLoggerOptions.SamplingSettings);
                    bool samplingEnabled = config.GetSection(samplingPath).GetValue("IsEnabled", true);

                    if (!samplingEnabled)
                    {
                        options.SamplingSettings = null;
                        return;
                    }

                    // Excluded/Included types must be moved from SamplingSettings to their respective properties in logger options
                    options.SamplingExcludedTypes = config.GetSection(samplingPath).GetValue<string>("ExcludedTypes", null);
                    options.SamplingIncludedTypes = config.GetSection(samplingPath).GetValue<string>("IncludedTypes", null);
                });

            return builder;
        }
    }
}
