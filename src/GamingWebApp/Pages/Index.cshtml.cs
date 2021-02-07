using GamingWebApp.Proxy;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly.Timeout;
using Refit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace GamingWebApp.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> logger;
        private readonly TelemetryClient telemetryClient;
        private readonly IOptionsSnapshot<LeaderboardApiOptions> options;
        private readonly ILeaderboardClient proxy;

        public IndexModel(TelemetryClient telemetryClient, IOptionsSnapshot<LeaderboardApiOptions> options,
            ILeaderboardClient proxy, ILoggerFactory loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger<IndexModel>();
            this.telemetryClient = telemetryClient;
            this.options = options;
            this.proxy = proxy;
        }

        public IEnumerable<HighScore> Scores { get; private set; }

        public async Task OnGetAsync()
        {
            // Create a proxy to web API 
            ILeaderboardClient client = RestService.For<ILeaderboardClient>(options.Value.BaseUrl);

            Scores = new List<HighScore>();
            try
            {
                using (var operation = telemetryClient.StartOperation<RequestTelemetry>("LeaderboardWebAPICall"))
                {
                    try
                    {
                        // Using injected typed HTTP client instead of locally created proxy
                        Scores = await proxy.GetHighScores();
                    }
                    catch
                    {
                        operation.Telemetry.Success = false;
                        throw;
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                logger.LogWarning(ex, "Http request failed.");
            }
            catch (TimeoutRejectedException ex)
            {
                logger.LogWarning(ex, "Timeout occurred when retrieving high score list.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unknown exception occurred while retrieving high score list");
            }
        }
    }
}
