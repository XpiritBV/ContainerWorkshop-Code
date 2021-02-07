using LeaderboardWebAPI.Infrastructure;
using LeaderboardWebAPI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LeaderboardWebAPI.Controllers
{
    public class ScoresController : Controller
    {
        private readonly LeaderboardContext context;
        
        public ScoresController(LeaderboardContext context)
        {
            this.context = context;
        }

        [HttpPost("{nickname}/{game}")]
        public async Task PostScore(string nickname, string game, [FromBody] int points)
        {
            // Lookup gamer based on nickname
            Gamer gamer = await context.Gamers
                  .FirstOrDefaultAsync(g => g.Nickname.ToLower() == nickname.ToLower())
                  .ConfigureAwait(false);

            if (gamer == null) return;

            // Find highest score for game
            var score = await context.Scores
                  .Where(s => s.Game == game && s.Gamer == gamer)
                  .OrderByDescending(s => s.Points)
                  .FirstOrDefaultAsync()
                  .ConfigureAwait(false);

            if (score == null)
            {
                score = new Score() { Gamer = gamer, Points = points, Game = game };
                await context.Scores.AddAsync(score);
            }
            else
            {
                if (score.Points > points) return;
                score.Points = points;
            }
            await context.SaveChangesAsync().ConfigureAwait(false);
        }
    }
}
