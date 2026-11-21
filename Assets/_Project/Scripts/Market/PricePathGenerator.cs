using System;
using UnityEngine;

namespace BurningAnt.Market
{
    /// <summary>하루 가격 경로(틱 수 + 1개, [0]이 시가)를 만든다.</summary>
    public static class PricePathGenerator
    {
        // shortcut: 시가 갭과 하루 추세를 무작위로 뽑음. 뉴스 시스템이 생기면 gap/trend를 뉴스에서 받도록 바꾼다.
        public static float[] Generate(float prevClose, float limit, MarketSettings settings, System.Random rng)
        {
            float gap = Signed(rng) * limit * settings.maxGapRatio;
            float trend = Signed(rng) * limit * settings.maxTrendRatio;
            return Generate(prevClose, limit, gap, trend, settings, rng);
        }

        public static float[] Generate(float prevClose, float limit, float gap, float trend, MarketSettings settings, System.Random rng)
        {
            int ticks = Math.Max(1, settings.ticksPerDay);
            float lower = prevClose * (1f - limit);
            float upper = prevClose * (1f + limit);
            var path = new float[ticks + 1];

            float price = prevClose * (1f + gap);
            for (int i = 0; i <= ticks; i++)
            {
                if (i > 0)
                {
                    float anchor = prevClose * (1f + Mathf.Lerp(gap, trend, i / (float)ticks));
                    float noise = (float)(rng.NextDouble() - 0.5) * prevClose * limit * settings.noiseRatio;
                    price += (anchor - price) * settings.pullStrength + noise;
                }
                price = Mathf.Clamp(price, lower, upper);
                path[i] = price;
            }
            return path;
        }

        static float Signed(System.Random rng) => (float)(rng.NextDouble() * 2.0 - 1.0);
    }
}
