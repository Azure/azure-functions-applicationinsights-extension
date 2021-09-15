﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.ApplicationInsights.WindowsServer.Channel.Implementation;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.ApplicationInsights;
using Microsoft.Azure.WebJobs.Logging.ApplicationInsights;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;

namespace Microsoft.Extensions.Hosting
{
    /// <summary>
    /// Extension methods for ApplicationInsights integration.
    /// </summary>
    public static class ApplicationInsightsWebJobsBuilderExtensions
    {
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

            // V3 has App Insights built into the host. We only want this configured for V4
            if (_configuration["FUNCTIONS_RUNTIME_VERSION"] != "~4")
            {
                return builder;
            }

            string appInsightsInstrumentationKey = _configuration["APPINSIGHTS_INSTRUMENTATIONKEY"];
            string appInsightsConnectionString = _configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];

            builder.Services.AddLogging((loggingBuilder) =>
            {
                loggingBuilder.AddApplicationInsightsWebJobs(o =>
                {
                    o.InstrumentationKey = appInsightsInstrumentationKey;
                    o.ConnectionString = appInsightsConnectionString;
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
