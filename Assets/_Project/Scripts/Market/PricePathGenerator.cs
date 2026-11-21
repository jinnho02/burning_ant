using System;
using UnityEngine;

namespace BurningAnt.Market
{
    /// <summary>하루 가격 경로(틱 수 + 1개, [0]이 시가)를 만든다.</summary>
    public static class PricePathGenerator
    {
        /// <summary>뉴스·예지가 없는 날: 전날 종가 ± 기본 구간(limit) 안에서만 무작위로 움직인다.</summary>
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

        /// <summary>
        /// 뉴스·예지 템플릿이 걸린 날: 결과가 뜨는 틱(revealTick)까지는 기본 구간 안에서 움직이다가,
        /// 그 틱에 전날 종가 × (1 + targetChange)로 뛰고, 이후에는 그 값 근처에서만 흔들린다. 장 마감 공개면 revealTick = 틱 수.
        /// </summary>
        public static float[] GenerateWithReveal(float prevClose, float limit, int revealTick, float targetChange, MarketSettings settings, System.Random rng)
        {
            var path = Generate(prevClose, limit, settings, rng);
            int ticks = path.Length - 1;
            revealTick = Mathf.Clamp(revealTick, 1, ticks);
            float target = prevClose * (1f + targetChange);
            float wobble = prevClose * limit * settings.noiseRatio * 0.5f;
            float price = target;
            path[revealTick] = target;
            for (int i = revealTick + 1; i <= ticks; i++)
            {
                price += (target - price) * settings.pullStrength + (float)(rng.NextDouble() - 0.5) * wobble;
                path[i] = Mathf.Max(prevClose * 0.01f, price);
            }
            return path;
        }

        static float Signed(System.Random rng) => (float)(rng.NextDouble() * 2.0 - 1.0);
    }
}
