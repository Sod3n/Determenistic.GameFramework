using System;
using Deterministic.GameFramework.Types;

namespace Deterministic.GameFramework.Detour.Crowd
{
    public class DtObstacleAvoidanceDebugData
    {
        private int m_nsamples;
        private int m_maxSamples;
        private Float[] m_vel;
        private Float[] m_ssize;
        private Float[] m_pen;
        private Float[] m_vpen;
        private Float[] m_vcpen;
        private Float[] m_spen;
        private Float[] m_tpen;

        public DtObstacleAvoidanceDebugData(int maxSamples)
        {
            m_maxSamples = maxSamples;
            m_vel = new Float[3 * m_maxSamples];
            m_pen = new Float[m_maxSamples];
            m_ssize = new Float[m_maxSamples];
            m_vpen = new Float[m_maxSamples];
            m_vcpen = new Float[m_maxSamples];
            m_spen = new Float[m_maxSamples];
            m_tpen = new Float[m_maxSamples];
        }

        public void Reset()
        {
            m_nsamples = 0;
        }

        void NormalizeArray(Float[] arr, int n)
        {
            // Normalize penaly range.
            Float minPen = Float.MaxValue;
            Float maxPen = Float.MinValue;
            for (int i = 0; i < n; ++i)
            {
                minPen = Float.Min(minPen, arr[i]);
                maxPen = Float.Max(maxPen, arr[i]);
            }

            Float penRange = maxPen - minPen;
            Float s = penRange > (Float)0.001f ? (1 / penRange) : 1;
            for (int i = 0; i < n; ++i)
                arr[i] = Float.Clamp((arr[i] - minPen) * s, 0, 1);
        }

        public void NormalizeSamples()
        {
            NormalizeArray(m_pen, m_nsamples);
            NormalizeArray(m_vpen, m_nsamples);
            NormalizeArray(m_vcpen, m_nsamples);
            NormalizeArray(m_spen, m_nsamples);
            NormalizeArray(m_tpen, m_nsamples);
        }

        public void AddSample(Vector3 vel, Float ssize, Float pen, Float vpen, Float vcpen, Float spen, Float tpen)
        {
            if (m_nsamples >= m_maxSamples)
                return;
            m_vel[m_nsamples * 3] = vel.X;
            m_vel[m_nsamples * 3 + 1] = vel.Y;
            m_vel[m_nsamples * 3 + 2] = vel.Z;
            m_ssize[m_nsamples] = ssize;
            m_pen[m_nsamples] = pen;
            m_vpen[m_nsamples] = vpen;
            m_vcpen[m_nsamples] = vcpen;
            m_spen[m_nsamples] = spen;
            m_tpen[m_nsamples] = tpen;
            m_nsamples++;
        }

        public int GetSampleCount()
        {
            return m_nsamples;
        }

        public Vector3 GetSampleVelocity(int i)
        {
            Vector3 vel;
            vel.X = m_vel[i * 3];
            vel.Y = m_vel[i * 3 + 1];
            vel.Z = m_vel[i * 3 + 2];
            return vel;
        }

        public Float GetSampleSize(int i)
        {
            return m_ssize[i];
        }

        public Float GetSamplePenalty(int i)
        {
            return m_pen[i];
        }

        public Float GetSampleDesiredVelocityPenalty(int i)
        {
            return m_vpen[i];
        }

        public Float GetSampleCurrentVelocityPenalty(int i)
        {
            return m_vcpen[i];
        }

        public Float GetSamplePreferredSidePenalty(int i)
        {
            return m_spen[i];
        }

        public Float GetSampleCollisionTimePenalty(int i)
        {
            return m_tpen[i];
        }
    }
}
