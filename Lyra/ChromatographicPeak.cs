using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra
{
    class ChromatographicPeak
    {
        public readonly List<IsotopicEnvelope> isotopicEnvelopes;
        public readonly double mass;
        public readonly double apexRt;
        public double signaltobaseline;

        public ChromatographicPeak(List<IsotopicEnvelope> isotopicEnvelopes, double mass, double apexRt)
        {
            this.isotopicEnvelopes = isotopicEnvelopes;
            this.mass = mass;
            this.apexRt = apexRt;
        }

        public double GetSignalToBaseline()
        {
            if (signaltobaseline == 0)
            {
                var envelopesGroupedByRt = isotopicEnvelopes.GroupBy(p => p.retentionTime).OrderBy(p => p.Key);
                List<Tuple<double,double>> summedIntensities = new List<Tuple<double,double>>();

                foreach (var timepoint in envelopesGroupedByRt)
                {
                    summedIntensities.Add(new Tuple<double,double>(timepoint.Key, timepoint.Sum(p => p.intensity)));
                }

                var slope = (summedIntensities.Last().Item2 - summedIntensities.First().Item2) / (summedIntensities.Last().Item1 - summedIntensities.First().Item1);
                var yint = summedIntensities.First().Item2 - slope * summedIntensities.First().Item1;

                foreach(var timepoint in summedIntensities)
                {
                    double sbr = (timepoint.Item2 / (slope * timepoint.Item1 + yint)) - 1;
                    signaltobaseline += sbr;
                }
            }
            
            return signaltobaseline;
        }

        public override string ToString()
        {
            return Math.Round(mass, 3) + " | " + Math.Round(apexRt, 3);
        }
    }
}
