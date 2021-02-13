using GamingWebApp.Proxy;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
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
        private readonly IOptionsSnapshot<LeaderboardApiOptions> options;

        public IndexModel(IOptionsSnapshot<LeaderboardApiOptions> options, ILoggerFactory loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger<IndexModel>();
            this.options = options;
        }

        public IEnumerable<HighScore> Scores { get; private set; }

        public async Task OnGetAsync()
        {
            ILeaderboardClient proxy = RestService.For<ILeaderboardClient>(options.Value.BaseUrl);
            Scores = new List<HighScore>();
            try
            {
                Scores = await proxy.GetHighScores();
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