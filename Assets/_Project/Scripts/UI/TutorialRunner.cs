using System.Collections.Generic;
using BurningAnt.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace BurningAnt.UI
{
    /// <summary>
    /// 튜토리얼 진행: 1일차에 각 단계에 처음 들어가면 그 단계의 설명을 차례로 띄우고 대상 요소를 강조한다.
    /// 설명이 떠 있는 동안은 화면 클릭을 막고, 장 중이면 시간을 멈춘다 (GameLoop.ExternalPause).
    /// 게임을 실행할 때마다 1일차에 한 번 나온다 (끝까지 보거나 건너뛰면, 다시 하기로 새 판을 시작해도 그 실행 동안은 안 뜸).
    /// </summary>
    public class TutorialRunner
    {
        // 예전에는 PlayerPrefs에 "봤음"을 저장해서 한 번 본 PC에서는 다시 실행해도 안 떴다 → 실행마다로 바꿈
        static bool doneThisRun;

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

        readonly float scale;

        public TutorialRunner(VisualElement root, GameLoop game, TutorialScript script, bool forceShow, float scale = 1f)
        {
            this.scale = Mathf.Max(0.5f, scale);
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
            var skipButton = root.Q<Button>("tutorial-skip");
            skipButton.clicked += Finish;

            // 크기 배율: 창 폭·안쪽 여백·글자·버튼을 같이 키운다 (USS 값이 1배 기준)
            box.style.width = 480f * this.scale;
            box.style.paddingLeft = box.style.paddingRight = 20f * this.scale;
            box.style.paddingTop = box.style.paddingBottom = 16f * this.scale;
            stepLabel.style.fontSize = 12f * this.scale;
            titleLabel.style.fontSize = 20f * this.scale;
            textLabel.style.fontSize = 16f * this.scale;
            skipButton.style.fontSize = 13f * this.scale;
            nextButton.style.fontSize = 16f * this.scale;
            nextButton.style.width = 110f * this.scale;
            nextButton.style.height = 38f * this.scale;

            active = overlay != null && script != null && (forceShow || !doneThisRun);
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

        /// <summary>시연용 자동 진행에서 건너뛰기.</summary>
        public void Skip() => Finish();

        void Finish()
        {
            queue.Clear();
            current = null;
            active = false;
            doneThisRun = true;
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
            if (float.IsNaN(boxW) || boxW <= 0f) boxW = 480f * scale;
            if (float.IsNaN(boxH) || boxH <= 0f) boxH = 180f * scale;
            boxW = Mathf.Min(boxW, area.width - 16f);

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

            // 자리 고르기: 아래 → 위 → 오른쪽 → 왼쪽. 어느 쪽에도 안 들어가면 강조와 덜 겹치는 화면 위/아래 끝
            float cx = Mathf.Clamp(x + r.width * 0.5f - boxW * 0.5f, 8f, Mathf.Max(8f, area.width - boxW - 8f));
            float cy = Mathf.Clamp(y + r.height * 0.5f - boxH * 0.5f, 8f, Mathf.Max(8f, area.height - boxH - 8f));
            float below = y + r.height + pad + gap, above = y - pad - gap - boxH;
            float right = x + r.width + pad + gap, left = x - pad - gap - boxW;
            float bx, by;
            if (below + boxH <= area.height - 8f) { bx = cx; by = below; }
            else if (above >= 8f) { bx = cx; by = above; }
            else if (right + boxW <= area.width - 8f) { bx = right; by = cy; }
            else if (left >= 8f) { bx = left; by = cy; }
            else
            {
                bx = cx;
                bool targetInUpperHalf = y + r.height * 0.5f < area.height * 0.5f;
                by = targetInUpperHalf ? area.height - boxH - 8f : 8f;
            }
            box.style.left = bx;
            box.style.top = Mathf.Max(8f, by);
            box.style.width = boxW;
        }

        static bool IsShown(VisualElement e)
        {
            for (var p = e; p != null; p = p.parent)
                if (p.resolvedStyle.display == DisplayStyle.None) return false;
            return true;
        }
    }
}
