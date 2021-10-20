// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Net.NetworkInformation;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.ApplicationInsights;
using Microsoft.Azure.WebJobs.Hosting;
using Microsoft.Extensions.Hosting;

[assembly: WebJobsStartup(typeof(ApplicationInsightsWebJobsStartup))]

namespace Microsoft.Azure.WebJobs.Extensions.ApplicationInsights
{
    internal class ApplicationInsightsWebJobsStartup : IWebJobsStartup2
    {
        public ApplicationInsightsWebJobsStartup()
        {
            Task.Run(() => NetworkInterface.GetIsNetworkAvailable());
        }

        public void Configure(IWebJobsBuilder builder)
        {
            throw new System.NotImplementedException();
        }

        public void Configure(WebJobsBuilderContext context, IWebJobsBuilder builder)
        {
            builder.AddApplicationInsights(context);
        }
    }
}
