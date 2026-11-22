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
        /// <param name="settleTick">결과 값에 다 닿는 틱. revealTick 이하면 한 번에 뛴다.</param>
        /// <param name="steps">1 = 한 번에, 2 이상 = 그 횟수만큼 계단식, 0 = 차근차근(매 틱 조금씩).</param>
        public static float[] GenerateWithReveal(float prevClose, float limit, int revealTick, float targetChange, MarketSettings settings, System.Random rng, int settleTick = -1, int steps = 1)
        {
            var path = Generate(prevClose, limit, settings, rng);
            int ticks = path.Length - 1;
            revealTick = Mathf.Clamp(revealTick, 1, ticks);
            settleTick = Mathf.Clamp(settleTick, revealTick, ticks);
            float target = prevClose * (1f + targetChange);
            float wobble = prevClose * limit * settings.noiseRatio * 0.5f;
            float from = path[revealTick - 1];
            int len = settleTick - revealTick;

            if (len > 0 && steps != 1)
            {
                if (steps == 0)
                {
                    // 차근차근: 매 틱 조금씩 (부드러운 S자 + 흔들림), 마지막 틱에 정확히 결과 값
                    for (int i = revealTick; i < settleTick; i++)
                    {
                        float t = (i - revealTick + 1f) / (len + 1f);
                        // 거의 일정한 속도 (S자를 약하게): 시작부터 눈에 보이게 꾸준히 오르거나 내림
                        float eased = Mathf.Lerp(t, t * t * (3f - 2f * t), 0.3f);
                        path[i] = Mathf.Max(prevClose * 0.01f, Mathf.Lerp(from, target, eased) + (float)(rng.NextDouble() - 0.5) * wobble);
                    }
                }
                else
                {
                    // 계단식: steps번 크게 뛰고, 사이에는 그 높이 근처에서 흔들림. 첫 계단은 움직이기 시작하는 틱, 마지막은 settleTick
                    var cut = new float[steps];
                    float sum = 0f;
                    for (int k = 0; k < steps; k++) { cut[k] = 0.8f + (float)rng.NextDouble() * 0.4f; sum += cut[k]; } // 계단 크기를 비슷하게 (첫 계단이 너무 커서 한 번에 뛴 것처럼 보이지 않게)
                    float level = from, acc = 0f;
                    int next = 0;
                    for (int i = revealTick; i < settleTick; i++)
                    {
                        int stepTick = revealTick + Mathf.RoundToInt(len * next / (float)(steps - 1));
                        if (next < steps - 1 && i >= stepTick)
                        {
                            acc += cut[next] / sum;
                            level = Mathf.Lerp(from, target, acc);
                            next++;
                        }
                        level += (float)(rng.NextDouble() - 0.5) * wobble * 0.5f;
                        path[i] = Mathf.Max(prevClose * 0.01f, level);
                    }
                }
            }

            float price = target;
            path[settleTick] = target;
            for (int i = settleTick + 1; i <= ticks; i++)
            {
                price += (target - price) * settings.pullStrength + (float)(rng.NextDouble() - 0.5) * wobble;
                path[i] = Mathf.Max(prevClose * 0.01f, price);
            }
            return path;
        }

        static float Signed(System.Random rng) => (float)(rng.NextDouble() * 2.0 - 1.0);
    }
}
