using BurningAnt.Core;
using BurningAnt.Items;
using BurningAnt.Market;
using UnityEngine;
using UnityEngine.UIElements;

namespace BurningAnt.UI
{
    /// <summary>
    /// 브금과 효과음. 브금: 1·2단계 = 아침, 3단계(장) = 없음, 4·5단계 = 저녁 (계속 반복).
    /// 아이템을 사면 골드, 그 밖의 클릭은 코드로 만든 짧은 클릭음.
    /// 파일은 Resources/Audio (title·morning·night·골드.mp3). 같은 이름으로 바꿔 넣으면 그대로 쓰인다.
    /// </summary>
    public class GameAudio : MonoBehaviour
    {
        const float BgmVolume = 0.5f, ClickVolume = 0.35f, GoldVolume = 0.9f;

        GameLoop game;
        AudioSource bgm, sfx, tickSfx;
        AudioClip tickUp, tickDown;
        StockWindowController stockWindow;

        AudioClip title, morning, evening, gold, click, alarm;
        VisualElement[] introScreens;
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
            tickUp = MakeChipBlip(true);
            tickDown = MakeChipBlip(false);
            stockWindow = GetComponent<StockWindowController>();
            game.Session.Ticked += OnPriceTick;

            // 브금 3곡: title = 경고창·타이틀·프롤로그·엔딩, morning = 1·2단계, night = 4·5단계. 3단계(장)는 브금 없이 주가 효과음만
            title = Resources.Load<AudioClip>("Audio/title");
            morning = Resources.Load<AudioClip>("Audio/morning");
            evening = Resources.Load<AudioClip>("Audio/night");
            introScreens = new[] { root.Q("warning-overlay"), root.Q("title-overlay"), root.Q("prologue-overlay") };
            gold = Resources.Load<AudioClip>("Audio/골드");
            click = MakeClick();
            alarm = MakeAlarm();

            game.PhaseChanged += OnPhaseChanged;
            game.ItemBought += OnItemBought;
            game.AlarmFired += OnAlarm;
            // 버블 단계(대상 처리 뒤)에서 듣는다: 같은 클릭으로 아이템을 샀으면 클릭음 대신 골드만
            root.RegisterCallback<ClickEvent>(e =>
            {
                if (boughtFrame == Time.frameCount) return; // 같은 클릭으로 아이템을 샀으면 골드 소리만
                if (IsClickable(e.target as VisualElement)) sfx.PlayOneShot(click, ClickVolume);
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
        const float TickMaxVolume = 0.48f, TickMinVolume = 0.08f, TickCapVolume = 1.6f; // 클립 진폭 0.5 기준, 처음 대비 약 4배

        // 클릭음이 나는 요소: 버튼, 상점 칸, 종목 줄, 그리고 클릭해서 넘기는 화면(경고창·타이틀·프롤로그·엔딩).
        // 빈 곳·글자·카드를 누를 때는 소리가 안 난다.
        static readonly string[] ClickScreens = { "warning-overlay", "title-overlay", "prologue-overlay", "end-overlay" };

        static bool IsClickable(VisualElement e)
        {
            for (var v = e; v != null; v = v.parent)
            {
                if (!v.enabledInHierarchy) return false;
                if (v is Button || v is ShopSlotElement || v is StockListItem || v is Toggle) return true;
                if (System.Array.IndexOf(ClickScreens, v.name) >= 0) return true;
            }
            return false;
        }

        void OnPriceTick()
        {
            var session = game.Session;
            var stock = stockWindow != null ? stockWindow.Selected : null;
            if (stock == null || session.Tick < 1) return;
            float change = stock.PriceAt(session.Tick) / Mathf.Max(0.0001f, stock.PriceAt(session.Tick - 1)) - 1f;

            // 한 틱 변동을 그 종목 하루 변동 구간 대비로 본다 (구간의 6%를 움직이면 최대)
            float raw = Mathf.Abs(change) / Mathf.Max(0.0005f, stock.DailyLimit * 0.06f);
            float strength = Mathf.Clamp01(raw);
            // 평소(구간의 6%까지)는 그대로, 그보다 크게 튀면(폭등·폭락·특보) 변동률만큼 더 크게 (최대 TickCapVolume)
            float volume = raw <= 1f ? Mathf.Lerp(TickMinVolume, TickMaxVolume, strength) : Mathf.Min(TickCapVolume, TickMaxVolume * raw);
            if (strength < 0.12f) return; // 작은 흔들림엔 조용히 (자주 울리지 않게)
            // 오르면 1.0~1.6배, 내리면 1.0~0.6배 피치 (상승·하락 소리 자체도 위·아래로 미끄러짐)
            tickSfx.pitch = change >= 0f ? Mathf.Lerp(1.0f, 1.1f, strength) : Mathf.Lerp(1.0f, 0.92f, strength); // 상승 740~880Hz, 하락 552~660Hz (겹치지 않게)
            tickSfx.PlayOneShot(change >= 0f ? tickUp : tickDown, volume);
        }

        /// <summary>
        /// 주가 틱 소리 (아케이드 칩튠): 듀티 25% 사각파 + 4비트 계단 음량, 60ms.
        /// 상승 = 660→1320Hz로 올라가는 "삥", 하락 = 660→330Hz로 내려가는 "뿅". 높낮이·크기는 재생할 때 피치·볼륨으로 더 바꾼다.
        /// </summary>
        static AudioClip MakeChipBlip(bool up)
        {
            // 부드러운 칩튠: 듀티 50% 사각파(덜 날카로움)를 낮은 음역에서 짧게 살짝만 미끄러뜨리고, 고역을 깎아 둥글게
            const int rate = 44100;
            int n = rate * 45 / 1000;
            var data = new float[n];
            double phase = 0;
            float smooth = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                float freq = up ? Mathf.Lerp(740f, 800f, t) : Mathf.Lerp(660f, 600f, t); // 상승은 위쪽, 하락은 아래쪽 음역으로 나눠 귀로 구별
                phase += freq / rate;
                float square = (phase % 1.0) < 0.5 ? 1f : -1f;
                smooth += (square - smooth) * 0.18f;                       // 간단한 로우패스 (날카로운 고역 줄임)
                float env = Mathf.Pow(1f - t, 1.5f) * Mathf.Clamp01(i / (rate * 0.004f));
                env = Mathf.Round(env * 15f) / 15f;                        // 4비트 계단 음량 (전자음 느낌은 유지)
                data[i] = smooth * env * 0.5f;
            }
            var clip = AudioClip.Create(up ? "price-up" : "price-down", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        void OnPhaseChanged() => PlayBgmFor(CurrentBgm());

        // 경고창·타이틀·프롤로그가 닫히는 순간 게임 브금으로 바뀌게 매 프레임 확인한다
        void Update()
        {
            if (game != null && bgm != null) PlayBgmFor(CurrentBgm());
        }

        AudioClip CurrentBgm()
        {
            bool intro = introScreens != null && System.Array.Exists(introScreens, o => o != null && o.resolvedStyle.display != DisplayStyle.None);
            if (intro) return title;
            return game.Phase switch
            {
                GamePhase.News or GamePhase.Prep => morning,
                GamePhase.Market => null, // 장 중엔 브금 없음
                GamePhase.Settlement or GamePhase.Shop => evening,
                _ => title, // 엔딩
            };
        }

        void PlayBgmFor(AudioClip clip)
        {
            if (clip == null)
            {
                if (bgm.isPlaying) bgm.Stop();
                bgm.clip = null;
                return;
            }
            if (bgm.clip == clip && bgm.isPlaying) return;
            bgm.clip = clip;
            bgm.volume = BgmVolume * Gain(clip);
            bgm.Play();
        }

        // 곡마다 원래 음량이 달라서 (실측 RMS: title 0.139, morning 0.109, night 0.199)
        // 모두 RMS 0.11 기준으로 맞춘다. 곡을 바꿔 넣으면 이 값도 다시 재서 고치기.
        float Gain(AudioClip clip)
        {
            if (clip == title) return 0.11f / 0.139f;
            if (clip == morning) return 0.11f / 0.109f;
            if (clip == evening) return 0.11f / 0.199f;
            return 1f;
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
