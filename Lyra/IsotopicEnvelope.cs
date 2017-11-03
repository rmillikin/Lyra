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

        public IsotopicEnvelope(double intensity, double retentionTime, int charge)
        {
            this.charge = charge;
            this.intensity = intensity;
            this.retentionTime = retentionTime;
        }

        public override string ToString()
        {
            return "" + charge + "|" + intensity + "|" + Math.Round(retentionTime,2);
        }
    }
}
