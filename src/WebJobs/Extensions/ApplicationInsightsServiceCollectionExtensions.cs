// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.AspNetCore;
using Microsoft.ApplicationInsights.AspNetCore.Extensions;
using Microsoft.ApplicationInsights.AspNetCore.TelemetryInitializers;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DependencyCollector;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.Extensibility.Implementation;
using Microsoft.ApplicationInsights.Extensibility.Implementation.ApplicationId;
using Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector;
using Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector.QuickPulse;
using Microsoft.ApplicationInsights.WindowsServer;
using Microsoft.ApplicationInsights.WindowsServer.TelemetryChannel;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Logging.ApplicationInsights;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection
{
    internal static class ApplicationInsightsServiceCollectionExtensions
    {
        public static IServiceCollection AddApplicationInsights(this IServiceCollection services, Action<ApplicationInsightsLoggerOptions> configure)
        {
            services.AddApplicationInsights();
            if (configure != null)
            {
                services.Configure<ApplicationInsightsLoggerOptions>(configure);
            }
            return services;
        }

        public static IServiceCollection AddApplicationInsights(this IServiceCollection services)
        {
            services.TryAddSingleton<ISdkVersionProvider, WebJobsSdkVersionProvider>();
            services.TryAddSingleton<IRoleInstanceProvider, WebJobsRoleInstanceProvider>();

            // Bind to the configuration section registered with 
            services.AddOptions<ApplicationInsightsLoggerOptions>()
                .Configure<ILoggerProviderConfiguration<ApplicationInsightsLoggerProvider>>((options, config) =>
                {
                    config.Configuration?.Bind(options);
                });

            services.AddSingleton<ITelemetryInitializer, HttpDependenciesParsingTelemetryInitializer>();
            services.AddSingleton<ITelemetryInitializer>(provider =>
            {
                ApplicationInsightsLoggerOptions options = provider.GetService<IOptions<ApplicationInsightsLoggerOptions>>().Value;
                if (options.HttpAutoCollectionOptions.EnableHttpTriggerExtendedInfoCollection)
                {
                    var httpContextAccessor = provider.GetService<IHttpContextAccessor>();
                    if (httpContextAccessor != null)
                    {
                        return new ClientIpHeaderTelemetryInitializer(httpContextAccessor);
                    }
                }

                return NullTelemetryInitializer.Instance;
            });

            services.AddSingleton<ITelemetryInitializer, WebJobsRoleEnvironmentTelemetryInitializer>();
            services.AddSingleton<ITelemetryInitializer, WebJobsTelemetryInitializer>();
            services.AddSingleton<ITelemetryInitializer, MetricSdkVersionTelemetryInitializer>();
            services.AddSingleton<QuickPulseInitializationScheduler>();
            services.AddSingleton<QuickPulseTelemetryModule>();

            services.AddSingleton<IApplicationIdProvider, ApplicationInsightsApplicationIdProvider>();

            services.AddSingleton<ITelemetryModule>(provider =>
            {
                var options = provider.GetService<IOptions<ApplicationInsightsLoggerOptions>>().Value;

                DependencyTrackingTelemetryModule dependencyCollector = null;
                if (options.EnableDependencyTracking)
                {
                    dependencyCollector = new DependencyTrackingTelemetryModule();
                    var excludedDomains = dependencyCollector.ExcludeComponentCorrelationHttpHeadersOnDomains;
                    excludedDomains.Add("core.windows.net");
                    excludedDomains.Add("core.chinacloudapi.cn");
                    excludedDomains.Add("core.cloudapi.de");
                    excludedDomains.Add("core.usgovcloudapi.net");
                    excludedDomains.Add("localhost");
                    excludedDomains.Add("127.0.0.1");

                    var includedActivities = dependencyCollector.IncludeDiagnosticSourceActivities;
                    includedActivities.Add("Microsoft.Azure.ServiceBus");
                    includedActivities.Add("Microsoft.Azure.EventHubs");

                    if (options.DependencyTrackingOptions != null)
                    {
                        dependencyCollector.DisableRuntimeInstrumentation = options.DependencyTrackingOptions.DisableRuntimeInstrumentation;
                        dependencyCollector.DisableDiagnosticSourceInstrumentation = options.DependencyTrackingOptions.DisableDiagnosticSourceInstrumentation;
                        dependencyCollector.EnableLegacyCorrelationHeadersInjection = options.DependencyTrackingOptions.EnableLegacyCorrelationHeadersInjection;
                        dependencyCollector.EnableRequestIdHeaderInjectionInW3CMode = options.DependencyTrackingOptions.EnableRequestIdHeaderInjectionInW3CMode;
                        dependencyCollector.EnableSqlCommandTextInstrumentation = options.DependencyTrackingOptions.EnableSqlCommandTextInstrumentation;
                        dependencyCollector.SetComponentCorrelationHttpHeaders = options.DependencyTrackingOptions.SetComponentCorrelationHttpHeaders;
                        dependencyCollector.EnableAzureSdkTelemetryListener = options.DependencyTrackingOptions.EnableAzureSdkTelemetryListener;
                    }

                    return dependencyCollector;
                }

                return NullTelemetryModule.Instance;
            });

            services.AddSingleton<ITelemetryModule, AppServicesHeartbeatTelemetryModule>();

            services.AddSingleton<TelemetryConfigurationFactory>();

            services.AddSingleton<TelemetryClient>(provider =>
            {
                TelemetryConfigurationFactory configurationFactory = provider.GetService<TelemetryConfigurationFactory>();
                TelemetryClient client = new TelemetryClient(configurationFactory.Create());

                ISdkVersionProvider versionProvider = provider.GetService<ISdkVersionProvider>();
                client.Context.GetInternalContext().SdkVersion = versionProvider?.GetSdkVersion();

                return client;
            });

            services.AddSingleton<ILoggerProvider, ApplicationInsightsLoggerProvider>();

            return services;
        }

        internal static LoggerFilterOptions CreateFilterOptions(LoggerFilterOptions registeredOptions)
        {
            // We want our own copy of the rules, excluding the 'allow-all' rule that we added for this provider.
            LoggerFilterOptions customFilterOptions = new LoggerFilterOptions
            {
                MinLevel = registeredOptions.MinLevel
            };

            ApplicationInsightsLoggerFilterRule allowAllRule = registeredOptions.Rules.OfType<ApplicationInsightsLoggerFilterRule>().Single();

            // Copy all existing rules
            foreach (LoggerFilterRule rule in registeredOptions.Rules)
            {
                if (rule != allowAllRule)
                {
                    customFilterOptions.Rules.Add(rule);
                }
            }

            // Copy 'hidden' rules
            foreach (LoggerFilterRule rule in allowAllRule.ChildRules)
            {
                customFilterOptions.Rules.Add(rule);
            }

            return customFilterOptions;
        }

        private class TelemetryConfigurationFactory
        {
            private readonly ApplicationInsightsLoggerOptions _options;
            private readonly LoggerFilterOptions _filterOptions;
            private readonly IApplicationIdProvider _applicationIdProvider;
            private readonly ISdkVersionProvider _sdkVersionProvider;
            private readonly IRoleInstanceProvider _roleInstanceProvider;
            private readonly IEnumerable<ITelemetryInitializer> _telemetryInitializers;
            private readonly QuickPulseInitializationScheduler _delayer;

            public TelemetryConfigurationFactory(IOptions<ApplicationInsightsLoggerOptions> options, IOptions<LoggerFilterOptions> loggerFilterOptions,
                IApplicationIdProvider applicationIdProvider, ISdkVersionProvider sdkVersionProvider, IRoleInstanceProvider roleInstance,
                IEnumerable<ITelemetryInitializer> telemetryInitializers, QuickPulseInitializationScheduler delayer)
            {
                _options = options.Value;
                _filterOptions = loggerFilterOptions.Value;
                _applicationIdProvider = applicationIdProvider;
                _sdkVersionProvider = sdkVersionProvider;
                _roleInstanceProvider = roleInstance;
                _telemetryInitializers = telemetryInitializers;
                _delayer = delayer;
            }

            public TelemetryConfiguration Create()
            {
                Activity.DefaultIdFormat = _options.HttpAutoCollectionOptions.EnableW3CDistributedTracing
                    ? ActivityIdFormat.W3C
                    : ActivityIdFormat.Hierarchical;
                Activity.ForceDefaultIdFormat = true;

                TelemetryConfiguration config = TelemetryConfiguration.CreateDefault();

                // Because of https://github.com/Microsoft/ApplicationInsights-dotnet-server/issues/943
                // we have to touch (and create) Active configuration before initializing telemetry modules
                // Active configuration is used to report AppInsights heartbeats
                // role environment telemetry initializer is needed to correlate heartbeats to particular host

                // Temporarily removing...
                _ = Task.Run(() =>
                {
                    var activeConfig = TelemetryConfiguration.Active;
                    if (!string.IsNullOrEmpty(_options.InstrumentationKey) &&
                    string.IsNullOrEmpty(activeConfig.InstrumentationKey))
                    {
                        activeConfig.InstrumentationKey = _options.InstrumentationKey;
                    }

                    // Set ConnectionString second because it takes precedence and
                    // we don't want InstrumentationKey to overwrite the value
                    // ConnectionString sets
                    if (!string.IsNullOrEmpty(_options.ConnectionString) &&
                     string.IsNullOrEmpty(activeConfig.ConnectionString))
                    {
                        activeConfig.ConnectionString = _options.ConnectionString;
                    }

                    if (!activeConfig.TelemetryInitializers.OfType<WebJobsRoleEnvironmentTelemetryInitializer>().Any())
                    {
                        activeConfig.TelemetryInitializers.Add(new WebJobsRoleEnvironmentTelemetryInitializer());
                        activeConfig.TelemetryInitializers.Add(new WebJobsTelemetryInitializer(_sdkVersionProvider, _roleInstanceProvider));
                    }
                });

                SetupTelemetryConfiguration(config, new ServerTelemetryChannel());

                return config;
            }

            private void SetupTelemetryConfiguration(TelemetryConfiguration configuration, ServerTelemetryChannel channel)
            {
                if (_options.ConnectionString != null)
                {
                    configuration.ConnectionString = _options.ConnectionString;
                }
                else if (_options.InstrumentationKey != null)
                {
                    configuration.InstrumentationKey = _options.InstrumentationKey;
                }

                configuration.TelemetryChannel = channel;

                foreach (ITelemetryInitializer initializer in _telemetryInitializers)
                {
                    if (initializer is not NullTelemetryInitializer)
                    {
                        configuration.TelemetryInitializers.Add(initializer);
                    }
                }

                _ = Task.Run(() => (channel as ServerTelemetryChannel)?.Initialize(configuration));

                CreateTelemetryModules(configuration, out QuickPulseTelemetryModule quickPulseModule);

                QuickPulseTelemetryProcessor quickPulseProcessor = null;
                configuration.TelemetryProcessorChainBuilder
                    .Use((next) => new OperationFilteringTelemetryProcessor(next))
                    .Use((next) =>
                    {
                        quickPulseProcessor = new QuickPulseTelemetryProcessor(next);
                        return quickPulseProcessor;
                    })
                    .Use((next) => new FilteringTelemetryProcessor(_filterOptions, next));

                if (_options.SamplingSettings != null)
                {
                    configuration.TelemetryProcessorChainBuilder.Use((next) =>
                    {
                        var processor = new AdaptiveSamplingTelemetryProcessor(_options.SamplingSettings, null, next);
                        if (_options.SamplingExcludedTypes != null)
                        {
                            processor.ExcludedTypes = _options.SamplingExcludedTypes;
                        }
                        if (_options.SamplingIncludedTypes != null)
                        {
                            processor.IncludedTypes = _options.SamplingIncludedTypes;
                        }
                        return processor;
                    });
                }

                configuration.TelemetryProcessorChainBuilder.Build();
                quickPulseModule?.RegisterTelemetryProcessor(quickPulseProcessor);

                foreach (ITelemetryProcessor processor in configuration.TelemetryProcessors)
                {
                    if (processor is ITelemetryModule module)
                    {
                        _ = Task.Run(() => module.Initialize(configuration));
                    }
                }

                configuration.ApplicationIdProvider = _applicationIdProvider;
            }

            private IEnumerable<ITelemetryModule> CreateTelemetryModules(TelemetryConfiguration configuration, out QuickPulseTelemetryModule quickPulseModule)
            {
                quickPulseModule = null;

                IList<ITelemetryModule> modules = new List<ITelemetryModule>();

                if (_options.HttpAutoCollectionOptions.EnableHttpTriggerExtendedInfoCollection)
                {
                    _ = Task.Run(() =>
                    {
                        var module = new RequestTrackingTelemetryModule(_applicationIdProvider)
                        {
                            CollectionOptions = new RequestCollectionOptions
                            {
                                TrackExceptions = false, // webjobs/functions track exceptions themselves
                                InjectResponseHeaders = _options.HttpAutoCollectionOptions.EnableResponseHeaderInjection
                            }
                        };

                        module.Initialize(configuration);
                    });
                }

                if (_options.EnableLiveMetrics)
                {
                    quickPulseModule = new QuickPulseTelemetryModule();
                    var module = quickPulseModule;

                    // QuickPulse can have a startup performance hit, so delay its initialization.
                    _delayer.ScheduleInitialization(() => 
                    {
                        if (_options.LiveMetricsAuthenticationApiKey != null)
                        {
                            module.AuthenticationApiKey = _options.LiveMetricsAuthenticationApiKey;
                        }

                        module.ServerId = _roleInstanceProvider?.GetRoleInstanceName();

                        module.Initialize(configuration);
                    }, _options.LiveMetricsInitializationDelay);

                    modules.Add(quickPulseModule);
                }

                if (_options.EnablePerformanceCountersCollection)
                {
                    _ = Task.Run(() =>
                    {
                        var module = new PerformanceCollectorModule
                        {
                            // Disabling this can improve cold start times
                            EnableIISExpressPerformanceCounters = false
                        };

                        module.Initialize(configuration);
                    });
                }

                return modules;
            }
        }
    }
}