using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra
{
    class MassSpectrum
    {
        public readonly List<MassSpectralPeak> peaks;
        public readonly int scanNumber;

        public MassSpectrum(int scanNumber, double[] xArray, double[] yArray)
        {
            if (xArray.Length != yArray.Length)
                throw new Exception("Error reading in scan number " + scanNumber + "; number of m/z values does not match number of intensity values");
            
            for (int i = 0; i < xArray.Length; i++)
                peaks.Add(new MassSpectralPeak(xArray[i], yArray[i]));
        }
    }
}
