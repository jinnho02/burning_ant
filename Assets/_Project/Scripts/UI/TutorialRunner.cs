using System.Collections.Generic;
using BurningAnt.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace BurningAnt.UI
{
    /// <summary>
    /// 튜토리얼 진행: 1일차에 각 단계에 처음 들어가면 그 단계의 설명을 차례로 띄우고 대상 요소를 강조한다.
    /// 설명이 떠 있는 동안은 화면 클릭을 막고, 장 중이면 시간을 멈춘다 (GameLoop.ExternalPause).
    /// 한 번 끝까지 보거나 건너뛰면 PlayerPrefs에 기록해 다음 판부터는 안 뜬다.
    /// </summary>
    public class TutorialRunner
    {
        public const string DoneKey = "BurningAnt.TutorialDone";

        readonly VisualElement root, overlay, highlight, box;
        readonly Label stepLabel, titleLabel, textLabel;
        readonly Button nextButton;
        readonly GameLoop game;
        readonly TutorialScript script;
        readonly Queue<TutorialScript.Step> queue = new();
        readonly HashSet<GamePhase> shownPhases = new();
        TutorialScript.Step current;
        bool active;

        public bool IsShowing => current != null;

        public TutorialRunner(VisualElement root, GameLoop game, TutorialScript script, bool forceShow)
        {
            this.root = root;
            this.game = game;
            this.script = script;
            overlay = root.Q("tutorial-overlay");
            highlight = root.Q("tutorial-highlight");
            box = root.Q("tutorial-box");
            stepLabel = root.Q<Label>("tutorial-step");
            titleLabel = root.Q<Label>("tutorial-title");
            textLabel = root.Q<Label>("tutorial-text");
            nextButton = root.Q<Button>("tutorial-next");
            nextButton.clicked += Next;
            root.Q<Button>("tutorial-skip").clicked += Finish;

            active = overlay != null && script != null && (forceShow || PlayerPrefs.GetInt(DoneKey, 0) == 0);
            Hide();
        }

        /// <summary>단계가 바뀔 때 부른다. 1일차에 처음 들어간 단계면 그 단계 설명을 줄 세운다.</summary>
        public void OnPhaseChanged()
        {
            if (!active || game.Day != 1 || !shownPhases.Add(game.Phase)) return;
            foreach (var step in script.steps)
                if (step.phase == game.Phase) queue.Enqueue(step);
        }

        /// <summary>매 프레임. blocked(타이틀 등이 떠 있음)면 기다렸다가, 줄 선 설명이 있으면 띄운다.</summary>
        public void Update(bool blocked)
        {
            if (!active) return;
            if (current == null)
            {
                if (blocked || queue.Count == 0) return;
                Show(queue.Dequeue());
            }
            // 이미 지난 단계의 설명이면 닫는다 (예: 장 마감으로 단계가 넘어감)
            if (current != null && current.phase != game.Phase)
            {
                Next();
                return;
            }
            if (current != null) Place();
        }

        void Show(TutorialScript.Step step)
        {
            current = step;
            int total = script.steps.Count, index = script.steps.IndexOf(step) + 1;
            stepLabel.text = $"튜토리얼 {index}/{total}";
            titleLabel.text = step.title;
            textLabel.text = step.text;
            nextButton.text = queue.Count > 0 ? "다음" : "확인";
            overlay.style.display = DisplayStyle.Flex;
            overlay.BringToFront();
            game.ExternalPause = true;
            Place();
        }

        void Next()
        {
            current = null;
            if (queue.Count > 0)
            {
                Show(queue.Dequeue());
                return;
            }
            Hide();
            // 마지막 단계(상점)까지 다 봤으면 끝
            if (game.Phase == GamePhase.Shop) Finish();
        }

        void Finish()
        {
            queue.Clear();
            current = null;
            active = false;
            PlayerPrefs.SetInt(DoneKey, 1);
            PlayerPrefs.Save();
            Hide();
        }

        void Hide()
        {
            if (overlay != null) overlay.style.display = DisplayStyle.None;
            if (game != null) game.ExternalPause = false;
        }

        /// <summary>대상 요소 둘레에 강조 테두리, 그 아래(자리가 없으면 위)에 설명 상자. 대상이 안 보이면 가운데.</summary>
        void Place()
        {
            var area = overlay.worldBound;
            var target = string.IsNullOrEmpty(current.target) ? null : root.Q(current.target);
            bool visible = target != null && target.resolvedStyle.display != DisplayStyle.None &&
                           target.worldBound.width > 1f && target.worldBound.height > 1f && IsShown(target);

            float boxW = box.resolvedStyle.width, boxH = box.resolvedStyle.height;
            if (float.IsNaN(boxW) || boxW <= 0f) boxW = 480f;
            if (float.IsNaN(boxH) || boxH <= 0f) boxH = 180f;

            if (!visible)
            {
                highlight.style.display = DisplayStyle.None;
                box.style.left = (area.width - boxW) * 0.5f;
                box.style.top = (area.height - boxH) * 0.5f;
                return;
            }

            var r = target.worldBound;
            float x = r.x - area.x, y = r.y - area.y;
            const float pad = 6f, gap = 14f;
            highlight.style.display = DisplayStyle.Flex;
            highlight.style.left = x - pad;
            highlight.style.top = y - pad;
            highlight.style.width = r.width + pad * 2f;
            highlight.style.height = r.height + pad * 2f;

            float bx = Mathf.Clamp(x + r.width * 0.5f - boxW * 0.5f, 8f, Mathf.Max(8f, area.width - boxW - 8f));
            float by = y + r.height + pad + gap;
            if (by + boxH > area.height - 8f) by = y - pad - gap - boxH;      // 아래 자리가 없으면 위
            if (by < 8f) by = Mathf.Clamp(y + r.height * 0.5f - boxH * 0.5f, 8f, area.height - boxH - 8f); // 둘 다 없으면 겹쳐서
            box.style.left = bx;
            box.style.top = by;
        }

        static bool IsShown(VisualElement e)
        {
            for (var p = e; p != null; p = p.parent)
                if (p.resolvedStyle.display == DisplayStyle.None) return false;
            return true;
        }
    }
}
