using BurningAnt.Core;
using BurningAnt.Items;
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
        AudioSource bgm, sfx;
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
