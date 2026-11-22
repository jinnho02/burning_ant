using BurningAnt.Core;
using BurningAnt.Items;
using BurningAnt.Market;
using UnityEngine;
using UnityEngine.UIElements;

namespace BurningAnt.UI
{
    /// <summary>
    /// 브금과 효과음. 브금: 1·2단계 = 아침, 3단계 = 점심, 4·5단계 = 저녁 (계속 반복).
    /// 아이템을 사면 골드, 그 밖의 클릭은 코드로 만든 짧은 클릭음.
    /// 파일은 Resources/Audio (아침·점심·저녁·골드.mp3). 같은 이름으로 바꿔 넣으면 그대로 쓰인다.
    /// </summary>
    public class GameAudio : MonoBehaviour
    {
        const float BgmVolume = 0.5f, ClickVolume = 0.35f, GoldVolume = 0.9f;

        GameLoop game;
        AudioSource bgm, sfx, tickSfx;
        AudioClip tickTone;
        StockWindowController stockWindow;

        AudioClip morning, noon, evening, gold, click, alarm;
        int boughtFrame = -1;

        public void Init(GameLoop loop, VisualElement root)
        {
            game = loop;
            // 씬에 카메라 리스너가 없으면 소리가 안 나므로 하나 단다
            if (FindFirstObjectByType<AudioListener>() == null) gameObject.AddComponent<AudioListener>();
            bgm = gameObject.AddComponent<AudioSource>();
            bgm.loop = true;
            bgm.playOnAwake = false;
            bgm.volume = BgmVolume;
            sfx = gameObject.AddComponent<AudioSource>();
            sfx.playOnAwake = false;
            tickSfx = gameObject.AddComponent<AudioSource>(); // 주가 틱 소리 전용 (피치를 바꿔 쓰므로 따로)
            tickSfx.playOnAwake = false;
            tickTone = MakeTickTone();
            stockWindow = GetComponent<StockWindowController>();
            game.Session.Ticked += OnPriceTick;

            morning = Resources.Load<AudioClip>("Audio/아침");
            noon = Resources.Load<AudioClip>("Audio/점심");
            evening = Resources.Load<AudioClip>("Audio/저녁");
            gold = Resources.Load<AudioClip>("Audio/골드");
            click = MakeClick();
            alarm = MakeAlarm();

            game.PhaseChanged += OnPhaseChanged;
            game.ItemBought += OnItemBought;
            game.AlarmFired += OnAlarm;
            // 버블 단계(대상 처리 뒤)에서 듣는다: 같은 클릭으로 아이템을 샀으면 클릭음 대신 골드만
            root.RegisterCallback<ClickEvent>(_ =>
            {
                if (boughtFrame != Time.frameCount) sfx.PlayOneShot(click, ClickVolume);
            });
            OnPhaseChanged();
        }

        void OnDestroy()
        {
            if (game == null) return;
            game.PhaseChanged -= OnPhaseChanged;
            game.ItemBought -= OnItemBought;
            game.AlarmFired -= OnAlarm;
            if (game.Session != null) game.Session.Ticked -= OnPriceTick;
        }

        // 주가 효과음: 보고 있는 종목의 가격이 한 틱 움직일 때마다 짧은 음.
        // 오르면 높은 음, 내리면 낮은 음 (움직인 만큼 더 높게/낮게), 크게 움직일수록 크게.
        const float TickMaxVolume = 0.45f, TickMinVolume = 0.04f;

        void OnPriceTick()
        {
            var session = game.Session;
            var stock = stockWindow != null ? stockWindow.Selected : null;
            if (stock == null || session.Tick < 1) return;
            float change = stock.PriceAt(session.Tick) / Mathf.Max(0.0001f, stock.PriceAt(session.Tick - 1)) - 1f;

            // 한 틱 변동을 그 종목 하루 변동 구간 대비로 본다 (구간의 6%를 움직이면 최대)
            float strength = Mathf.Clamp01(Mathf.Abs(change) / Mathf.Max(0.0005f, stock.DailyLimit * 0.06f));
            float volume = Mathf.Lerp(TickMinVolume, TickMaxVolume, strength);
            if (strength < 0.03f) return; // 거의 안 움직였으면 조용히
            // 오르면 1.0~1.8배, 내리면 1.0~0.55배 피치
            tickSfx.pitch = change >= 0f ? Mathf.Lerp(1.05f, 1.8f, strength) : Mathf.Lerp(0.95f, 0.55f, strength);
            tickSfx.PlayOneShot(tickTone, volume);
        }

        /// <summary>주가 틱 소리: 660Hz 사인파 70ms, 부드럽게 사라짐 (피치로 높낮이를 바꿔 쓴다).</summary>
        static AudioClip MakeTickTone()
        {
            const int rate = 44100;
            int n = rate * 70 / 1000;
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                float attack = Mathf.Clamp01(i / (rate * 0.003f));
                data[i] = Mathf.Sin(2 * Mathf.PI * 660f * i / rate) * attack * Mathf.Pow(1f - t, 2f) * 0.7f;
            }
            var clip = AudioClip.Create("price-tick", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        void OnPhaseChanged()
        {
            var clip = game.Phase switch
            {
                GamePhase.News or GamePhase.Prep => morning,
                GamePhase.Market => noon,
                GamePhase.Settlement or GamePhase.Shop => evening,
                _ => bgm.clip, // 엔딩: 하던 브금 그대로
            };
            if (clip == null || (bgm.clip == clip && bgm.isPlaying)) return;
            bgm.clip = clip;
            bgm.Play();
        }

        void OnItemBought(ItemDefinition item)
        {
            boughtFrame = Time.frameCount;
            if (gold != null) sfx.PlayOneShot(gold, GoldVolume);
        }

        void OnAlarm() => sfx.PlayOneShot(alarm, 0.8f);

        /// <summary>파산 경보: 880Hz·660Hz를 번갈아 6번 (1.2초) 울리는 사이렌.</summary>
        static AudioClip MakeAlarm()
        {
            const int rate = 44100;
            int n = rate * 12 / 10;
            var data = new float[n];
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                int beep = i / (rate / 5);
                phase += 2 * System.Math.PI * (beep % 2 == 0 ? 880 : 660) / rate;
                data[i] = (float)System.Math.Sin(phase) * 0.6f;
            }
            var clip = AudioClip.Create("alarm", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>짧은 "틱" 소리: 1.4kHz에서 0.9kHz로 내려가며 빠르게 사라지는 사인파 (45ms).</summary>
        static AudioClip MakeClick()
        {
            const int rate = 44100;
            int n = rate * 45 / 1000;
            var data = new float[n];
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                phase += 2 * Mathf.PI * Mathf.Lerp(1400f, 900f, t) / rate;
                float attack = Mathf.Clamp01(i / (rate * 0.002f)); // 2ms 페이드인 (딱 소리 방지)
                data[i] = (float)System.Math.Sin(phase) * attack * Mathf.Pow(1f - t, 3f) * 0.8f;
            }
            var clip = AudioClip.Create("click", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
