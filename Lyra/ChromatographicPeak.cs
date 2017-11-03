using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra
{
    class ChromatographicPeak
    {
        public List<IsotopicEnvelope> isotopicEnvelopes;
        public double mass;
        public double apexRt;

        public ChromatographicPeak()
        {
            isotopicEnvelopes = new List<IsotopicEnvelope>();
        }

        public override string ToString()
        {
            return Math.Round(mass, 3) + " | " + Math.Round(apexRt, 3);
        }
    }
}
