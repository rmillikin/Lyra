using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra
{
    class IsotopicEnvelope
    {
        public readonly int charge;
        public readonly double intensity;
        public readonly double retentionTime;
        public readonly List<MassSpectralPeak> peaks;
        
        public IsotopicEnvelope(double retentionTime, int charge, List<MassSpectralPeak> peaks)
        {
            this.charge = charge;
            this.retentionTime = retentionTime;
            this.peaks = peaks;
            this.intensity = this.peaks.Sum(p => p.intensity / charge);
        }

        public override string ToString()
        {
            return "" + charge + "|" + intensity + "|" + Math.Round(retentionTime,2);
        }
    }
}
