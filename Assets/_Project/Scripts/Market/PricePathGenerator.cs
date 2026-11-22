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
        /// <param name="steps">1 = 한 번에, 2 이상 = 그 수의 중간 목표를 거치는 오르락내리락(굴곡), 0 = 차근차근(매 틱 조금씩).</param>
        public static float[] GenerateWithReveal(float prevClose, float limit, int revealTick, float targetChange, MarketSettings settings, System.Random rng, int settleTick = -1, int steps = 1, float overshoot = 1f)
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
                    // 오르락내리락 + 초과 후 되돌림: 중간 목표 steps개 (전체 변동 대비 비율).
                    // 첫 반응은 작게(10~35%), 굴곡을 거치며 무작위 시점에 최고점(overshoot = 120~160%)에 닿고,
                    // 다시 굴곡을 거치며 내려와 마지막에 결과 값(1)에 닿는다. 폭락은 더 깊이 빠졌다가 회복.
                    int k = steps;
                    var f = new float[k];
                    var at = new int[k];
                    float peak = Mathf.Max(1f, overshoot);
                    int peakIdx = k >= 4 ? 1 + rng.Next(1, k - 2) : Mathf.Max(1, k - 2); // 처음·끝이 아닌 무작위 지점
                    f[0] = 0.1f + (float)rng.NextDouble() * 0.25f;
                    float amp = 0.12f + (float)rng.NextDouble() * 0.15f;
                    for (int j = 1; j < peakIdx; j++)
                    {
                        float baseF = f[0] + (peak - f[0]) * j / peakIdx;
                        f[j] = Mathf.Clamp(baseF + ((float)rng.NextDouble() * 2f - 1f) * amp * peak, -0.1f, peak * 0.95f);
                    }
                    f[peakIdx] = peak;
                    for (int j = peakIdx + 1; j < k - 1; j++)
                    {
                        float baseF = peak + (1f - peak) * (j - peakIdx) / (k - 1f - peakIdx);
                        f[j] = Mathf.Clamp(baseF + ((float)rng.NextDouble() * 2f - 1f) * amp, 0.6f, peak * 0.97f);
                    }
                    f[k - 1] = 1f;
                    // 목표 시점: 첫 목표 = 특보 틱, 마지막 = settleTick, 사이는 고르게 + 조금씩 흔들기 (겹치지 않게)
                    at[0] = revealTick;
                    at[k - 1] = settleTick;
                    for (int j = 1; j < k - 1; j++)
                    {
                        float pos = len * j / (k - 1f) + ((float)rng.NextDouble() - 0.5f) * len / (2f * (k - 1));
                        at[j] = Mathf.Clamp(revealTick + Mathf.RoundToInt(pos), at[j - 1] + 1, settleTick - (k - 1 - j));
                    }
                    // 구간마다 미끄러지듯 이어가거나(70%) 뚝 뛰거나(30%). 최고점으로 가는 구간은 항상 미끄러지듯 (한 번에 튀지 않게)
                    var ramp = new bool[k];
                    for (int j = 0; j < k; j++) ramp[j] = rng.NextDouble() < 0.7 || j == peakIdx - 1;
                    int seg = 0;
                    for (int i = revealTick; i < settleTick; i++)
                    {
                        while (seg < k - 2 && i >= at[seg + 1]) seg++;
                        float a = f[seg], b = f[seg + 1];
                        float t = (i - at[seg]) / (float)Mathf.Max(1, at[seg + 1] - at[seg]);
                        float v = ramp[seg] ? Mathf.Lerp(a, b, t) : a;
                        path[i] = Mathf.Max(prevClose * 0.01f, Mathf.LerpUnclamped(from, target, v) + (float)(rng.NextDouble() - 0.5) * wobble * 0.8f);
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
