using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Soccer
{
    public class MatchedMatches
    {
        public string Name { get; set; }
        public string Link { get; set; }
        public float ScoredAndConceded_A { get; set; }
        public float ScoredAndConceded_B { get; set; }
        public float ScoredAndConceded_C { get; set; }
        public float ScoredAndConceded_D { get; set; }
        public float Average { get; set; }
        public float CleanSheets_Home { get; set; }
        public float CleanSheets_Away { get; set; }
    }
}
