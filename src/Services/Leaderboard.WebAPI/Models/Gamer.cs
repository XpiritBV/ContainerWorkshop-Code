using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Leaderboard.WebAPI.Models
{
    public class Gamer
    {
        private string _name;

        public int Id { get; set; }
        public Guid GamerGuid { get; set; }
        public string Nickname 
        {
            get { return _name ?? "anonymous"; }
            set { _name = value; }
        }
        public virtual ICollection<Score> Scores { get; set; }
    }
}
