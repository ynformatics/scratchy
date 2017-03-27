using System;
using System.Collections.Generic;

namespace Scratchy
{
    class Spectrum
    {
        List<SpectrumPoint> spectrum = new List<SpectrumPoint>();
        static double[] hammingWindowCoeffs;
        static int N = 1000;

        public Spectrum()
        {
            hammingWindowCoeffs = new double[N];
            for (int n = 0; n < N; n++)
                hammingWindowCoeffs[n] = 0.54 - 0.46 * Math.Cos((2 * Math.PI * n) / (N - 1));
        }

        public void AppendSample(byte[] data, uint time)
        {
            Complex[] fft = new Complex[N];
            for (int i = 0; i < N; i++)
            {
                int byteOffset = 2 * i;
                double val = hammingWindowCoeffs[i] * (short)(data[byteOffset] | data[byteOffset + 1] << 8);

                fft[i] = new Complex(val, 0);
            }
            Fourier.FFT(N, fft);

            var magnitudes = new double[6];
            var frequencies = new uint[6];

            for (uint freq = MinFrequency; freq < MaxFrequency - 1; freq++)
            {
                // Get the magnitude:
                double mag = fft[freq].Magnitude;

                // Find out which range we are in:
                int range = getRange(freq);

                // Save the highest magnitude and corresponding frequency:
                if (mag > magnitudes[range])
                {
                    magnitudes[range] = mag;

                    frequencies[range] = freq;
                }
            }

            var threshold = 0.9 * (magnitudes[0] + magnitudes[1] + magnitudes[2] +
                magnitudes[3] + magnitudes[4] + magnitudes[5]) / 6;

            for (int i = 0; i < 6; i++)
            {
                if (magnitudes[i] > threshold)
                {
                    spectrum.Add(new SpectrumPoint() { Freq = frequencies[i], Time = time });
                }
            }

        }


        public List<Fingerprint> GetFingerprints(uint learningAlbumId)
        {
            List<Fingerprint> fingerprints = new List<Fingerprint>();

            for (int i = 0; i < spectrum.Count - 7; i++)
            {
                var anchor = spectrum[i];
                for (int target = i + 3; target < i + 8; target++)
                {
                     fingerprints.Add(new Fingerprint(learningAlbumId, anchor.Time, anchor.Freq, spectrum[target].Time, spectrum[target].Freq));
                }
            }

            return fingerprints;
        }

        static uint MaxFrequency = 511;
        static uint MinFrequency = 1;

        uint[] RangeBoundaries = new uint[] { 10, 20, 40, 80, 160, MaxFrequency + 1 };
        int getRange(uint freq)
        {
            int i = 0;
            while (RangeBoundaries[i] < freq)
                i++;
            return i;
        }

        public void Clear()
        {
            spectrum.Clear();
        }
    }

    class SpectrumPoint
    {
        public uint Freq { get; set; }
        public uint Time { get; set; }
    }
}