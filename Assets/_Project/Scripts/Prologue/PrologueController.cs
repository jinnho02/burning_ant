using System.Collections.Generic;
using System.Globalization;
using BurningAnt.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace BurningAnt.Prologue
{
    /// <summary>
    /// 프롤로그(대사 + 소품 카드 + 주인공) → "DAY 1" → 게임 씬.
    /// 게임 화면의 타이틀을 처음 클릭했을 때 한 번만 열리고(GameScreenController), 끝나면 타이틀 없이 1일차로 간다.
    /// 대사·그림은 PrologueScript 에셋에서 고친다. 실행 인자 -autoplay: 자동 진행 (시연용).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class PrologueController : MonoBehaviour
    {
        public const string SceneName = "Prologue";
        const string SeenKey = "BurningAnt.PrologueSeen";

        /// <summary>프롤로그를 한 번이라도 끝까지(또는 건너뛰기로) 봤는지.</summary>
        public static bool HasSeen => PlayerPrefs.GetInt(SeenKey, 0) == 1;
        /// <summary>프롤로그가 끝나 게임 씬으로 돌아가는 중. 게임 화면이 타이틀을 한 번 건너뛰고 지운다.</summary>
        public static bool ReturnedFromPrologue;
        public static bool AutoplayArg => System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-autoplay") >= 0;

        [SerializeField] PrologueScript script;
        [SerializeField, Tooltip("글자 하나가 나오는 간격(초)")] float secondsPerChar = 0.035f;
        [SerializeField, Tooltip("AUTO일 때 대사가 다 나온 뒤 넘어가기까지(초)")] float autoDelay = 1.6f;

        VisualElement storyScreen, daySplash, portrait, stamp, loanFill, loginFill;
        Label speaker, line, footnote, nextMark, loanAmount, loanTerms, accountCash, accountDebt, dreamAmount;
        Button autoButton;
        readonly Dictionary<PrologueProp, VisualElement> props = new();

        int index = -1;
        string fullText = "";
        float shownChars, autoTimer, propTimer;
        bool ending, auto;
        PrologueProp currentProp;

        void Start()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            KoreanFont.Apply(root);
            Query(root);

            float debt = script.loanSettings != null ? script.loanSettings.startingDebt : 10000f;
            accountCash.text = Money(debt);
            accountDebt.text = Money(debt);
            dreamAmount.text = Money(debt * 2f) + "?!";
            loanTerms.text = script.Fill("한도 100% · 이자 {rate} / {period}");

            storyScreen.RegisterCallback<PointerDownEvent>(e => { if (!IsButton(e.target)) Advance(); });
            root.Q<Button>("skip-button").clicked += Finish;
            autoButton.clicked += () => SetAuto(!auto);
            SetAuto(AutoplayArg);

            Next();
        }

        void Query(VisualElement root)
        {
            storyScreen = root.Q("story-screen");
            daySplash = root.Q("day-splash");
            portrait = root.Q("story-portrait");
            speaker = root.Q<Label>("speaker");
            line = root.Q<Label>("line");
            footnote = root.Q<Label>("footnote");
            nextMark = root.Q<Label>("next-mark");
            autoButton = root.Q<Button>("auto-button");

            loanAmount = root.Q<Label>("loan-amount");
            loanTerms = root.Q<Label>("loan-terms");
            loanFill = root.Q("loan-limit-fill");
            stamp = root.Q("stamp");
            loginFill = root.Q("login-fill");
            accountCash = root.Q<Label>("account-cash");
            accountDebt = root.Q<Label>("account-debt");
            dreamAmount = root.Q<Label>("dream-amount");

            props[PrologueProp.LoanApp] = root.Q("prop-loan");
            props[PrologueProp.Dream] = root.Q("prop-dream");
            props[PrologueProp.Account] = root.Q("prop-account");
            props[PrologueProp.Login] = root.Q("prop-login");
        }

        void Update()
        {
            if (ending) return;

            // 글자 한 자씩
            if (shownChars < fullText.Length)
            {
                shownChars = Mathf.Min(fullText.Length, shownChars + Time.deltaTime / secondsPerChar);
                line.text = fullText.Substring(0, (int)shownChars);
            }
            bool typing = shownChars < fullText.Length;
            nextMark.style.visibility = typing ? Visibility.Hidden : Visibility.Visible;

            AnimateProp();

            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                Advance();
            else if (auto && !typing && (autoTimer += Time.deltaTime) > autoDelay)
                Advance();
        }

        /// <summary>글자가 나오는 중이면 한 번에 다 보여주고, 다 나왔으면 다음 대사.</summary>
        void Advance()
        {
            if (shownChars < fullText.Length)
            {
                shownChars = fullText.Length;
                line.text = fullText;
                return;
            }
            Next();
        }

        void Next()
        {
            index++;
            autoTimer = 0f;
            if (index >= script.lines.Count)
            {
                Finish();
                return;
            }

            var l = script.lines[index];
            string who = script.Fill(l.speaker);
            speaker.text = who;
            speaker.style.display = string.IsNullOrEmpty(who) ? DisplayStyle.None : DisplayStyle.Flex;
            fullText = script.Fill(l.text);
            shownChars = 0f;
            line.text = "";
            footnote.text = script.Fill(l.footnote);
            if (l.portrait != null) portrait.style.backgroundImage = new StyleBackground(l.portrait);
            ShowProp(l.prop);
        }

        void ShowProp(PrologueProp prop)
        {
            bool sameCard = prop == currentProp || (prop == PrologueProp.Approved && currentProp == PrologueProp.LoanApp);
            if (!sameCard) propTimer = 0f;
            currentProp = prop;

            foreach (var (kind, element) in props)
            {
                bool on = kind == prop || (kind == PrologueProp.LoanApp && prop == PrologueProp.Approved);
                if (on && element.resolvedStyle.display == DisplayStyle.None)
                {
                    element.style.display = DisplayStyle.Flex;
                    element.schedule.Execute(() => element.AddToClassList("prop--on")); // 한 프레임 뒤에 켜야 페이드가 보인다
                }
                else if (!on)
                {
                    element.RemoveFromClassList("prop--on");
                    element.style.display = DisplayStyle.None;
                }
            }
            stamp.EnableInClassList("stamp--on", prop == PrologueProp.Approved);
        }

        /// <summary>대출 금액이 한도까지 올라가고, 로그인 막대가 차오른다.</summary>
        void AnimateProp()
        {
            propTimer += Time.deltaTime;
            float t = Mathf.Clamp01(propTimer / 1.2f);
            if (currentProp is PrologueProp.LoanApp or PrologueProp.Approved)
            {
                float debt = script.loanSettings != null ? script.loanSettings.startingDebt : 10000f;
                float eased = 1f - (1f - t) * (1f - t);
                loanAmount.text = Money(Mathf.Round(debt * eased / 100f) * 100f);
                loanFill.style.width = Length.Percent(eased * 100f);
            }
            if (currentProp == PrologueProp.Login)
                loginFill.style.width = Length.Percent(t * 100f);
        }

        void SetAuto(bool on)
        {
            auto = on;
            autoTimer = 0f;
            autoButton.EnableInClassList("story-button--on", on);
        }

        /// <summary>프롤로그를 봤다고 기록하고 "DAY 1"을 띄운 뒤 게임 씬으로 (타이틀 없이).</summary>
        void Finish()
        {
            if (ending) return;
            ending = true;
            PlayerPrefs.SetInt(SeenKey, 1);
            PlayerPrefs.Save();
            ReturnedFromPrologue = true;
            daySplash.style.display = DisplayStyle.Flex;
            daySplash.schedule.Execute(() => daySplash.AddToClassList("day-splash--on"));
            daySplash.schedule.Execute(() => SceneManager.LoadScene(script.nextScene)).ExecuteLater(1600);
        }

        static bool IsButton(IEventHandler target) =>
            target is VisualElement ve && ve.GetFirstOfType<Button>() != null;

        static string Money(float value) => "$" + value.ToString("N0", CultureInfo.InvariantCulture);
    }
}
