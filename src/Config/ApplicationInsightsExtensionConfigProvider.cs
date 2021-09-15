// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Description;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Azure.WebJobs.Logging.ApplicationInsights;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.Extensions.ApplicationInsights
{
    [Extension("ApplicationInsights")]
    internal class ApplicationInsightsExtensionConfigProvider : IExtensionConfigProvider
    {
        private readonly IConfiguration _configuration;
        private readonly INameResolver _nameResolver;
        private readonly ApplicationInsightsLoggerOptions _options;
        private readonly ILoggerFactory _loggerFactory;

        public ApplicationInsightsExtensionConfigProvider(
            IOptions<ApplicationInsightsLoggerOptions> options,
            IConfiguration configuration,
            INameResolver nameResolver,
            ILoggerFactory loggerFactory)
        {
            _configuration = configuration;
            _nameResolver = nameResolver;
            _options = options.Value;
            _loggerFactory = loggerFactory;
        }

        /// <inheritdoc />
        public void Initialize(ExtensionConfigContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            return;
        }
    }
}
