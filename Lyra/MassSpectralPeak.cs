using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra
{
    class MassSpectralPeak
    {
        public readonly double intensity;
        public readonly double mz;

        public MassSpectralPeak(double mz, double intensity)
        {
            this.intensity = intensity;
            this.mz = mz;
        }
    }
}
