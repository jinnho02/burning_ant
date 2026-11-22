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
        public static float[] GenerateWithReveal(float prevClose, float limit, int revealTick, float targetChange, MarketSettings settings, System.Random rng, int settleTick = -1, int steps = 1, float overshoot = 1f, int shape = 1, float fakeLead = 0f)
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
                    // 중간 목표 steps개 (전체 변동 대비 비율, 마지막 = 1 = 결과 값). 모양(shape)마다 다르게:
                    // 1 넘겼다 돌아옴 · 2 넘기지 않고 다가감 · 3 막판 몰아치기 · 4 마감까지 한 방향 (MoveShape 순서)
                    int k = steps;
                    var f = new float[k];
                    var at = new int[k];
                    float R() => (float)rng.NextDouble();
                    float S() => (float)rng.NextDouble() * 2f - 1f;
                    int peakIdx = -1;
                    switch (shape)
                    {
                        case 1: // 넘겼다 돌아옴: 무작위 시점에 최고점(overshoot), 돌아오는 방식도 제각각
                        {
                            float peak = Mathf.Max(1.05f, overshoot);
                            peakIdx = 1 + rng.Next(0, Mathf.Max(1, k - 2));
                            f[0] = 0.3f + R() * 0.5f; // 특보 순간 이미 크게 반영 (따라 사면 남는 게 적거나 물림)
                            float amp = 0.1f + R() * 0.15f;
                            for (int j = 1; j < peakIdx; j++)
                                f[j] = Mathf.Clamp(f[0] + (peak - f[0]) * j / peakIdx + S() * amp, -0.1f, peak * 0.95f);
                            f[peakIdx] = peak;
                            for (int j = peakIdx + 1; j < k - 1; j++)
                                f[j] = Mathf.Clamp(peak + (1f - peak) * (j - peakIdx) / (k - 1f - peakIdx) + S() * amp, 0.7f, peak * 0.97f);
                            break;
                        }
                        case 3: // 막판 몰아치기: 한동안 조금만(0~45%) 오르내리다 마지막 구간에 몰아서
                        {
                            f[0] = 0.05f + R() * 0.2f;
                            for (int j = 1; j < k - 1; j++)
                                f[j] = Mathf.Clamp(f[0] + 0.25f * j / (k - 1f) + S() * 0.15f, -0.2f, 0.45f);
                            break;
                        }
                        case 4: // 마감까지 한 방향: 꾸준히 밀리며 작은 되돌림만
                        {
                            f[0] = 0.05f + R() * 0.15f;
                            for (int j = 1; j < k - 1; j++)
                                f[j] = Mathf.Clamp(f[0] + (1f - f[0]) * j / (k - 1f) + S() * 0.07f, f[j - 1] - 0.08f, 0.97f);
                            break;
                        }
                        default: // 2 넘기지 않고 다가감: 되돌림 섞으며 결과 값 아래에서 다가감
                        {
                            f[0] = 0.3f + R() * 0.5f; // 특보 순간 이미 크게 반영
                            float amp = 0.12f + R() * 0.18f;
                            for (int j = 1; j < k - 1; j++)
                                f[j] = Mathf.Clamp(f[0] + (1f - f[0]) * j / (k - 1f) + S() * amp, -0.15f, 0.95f);
                            break;
                        }
                    }
                    f[k - 1] = 1f;

                    // 반전 결과: 초반엔 기사 방향(= 결과와 반대)으로 fakeLead(전날 종가 대비 10~20%)만큼 따라가다가 꺾인다.
                    // 꺾이는 시점(중간 목표 1~k-3번째)과 따라가는 모양은 경로마다 다르고, 꺾인 뒤는 다가감 모양으로 결과 값까지.
                    if (fakeLead > 0f && k >= 4 && Mathf.Abs(target - from) > 1e-4f)
                    {
                        float fake = -fakeLead * prevClose / Mathf.Abs(target - from);
                        int turn = 1 + rng.Next(0, k - 3);
                        f[0] = fake * (0.3f + R() * 0.4f);
                        for (int j = 1; j <= turn; j++)
                            f[j] = j == turn ? fake : Mathf.Lerp(f[0], fake, j / (float)turn) + S() * Mathf.Abs(fake) * 0.25f;
                        for (int j = turn + 1; j < k - 1; j++)
                            f[j] = Mathf.Clamp(fake + (1f - fake) * (j - turn) / (k - 1f - turn) + S() * 0.15f, fake, 0.95f);
                        peakIdx = -1;
                    }
                    // 목표 시점: 첫 목표 = 특보 틱, 마지막 = settleTick, 사이는 고르게 + 조금씩 흔들기 (겹치지 않게)
                    at[0] = revealTick;
                    at[k - 1] = settleTick;
                    for (int j = 1; j < k - 1; j++)
                    {
                        float pos = len * j / (k - 1f) + ((float)rng.NextDouble() - 0.5f) * len / (2f * (k - 1));
                        at[j] = Mathf.Clamp(revealTick + Mathf.RoundToInt(pos), at[j - 1] + 1, settleTick - (k - 1 - j));
                    }
                    // 구간마다 미끄러지듯 이어가거나 뚝 뛰거나 (비율은 경로마다 40~90%). 최고점으로 가는 구간은 항상 미끄러지듯
                    var ramp = new bool[k];
                    float rampChance = 0.4f + (float)rng.NextDouble() * 0.5f; // 경로마다 뚝뚝 끊기는 정도가 다름
                    for (int j = 0; j < k; j++) ramp[j] = rng.NextDouble() < rampChance || j == peakIdx - 1;
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
