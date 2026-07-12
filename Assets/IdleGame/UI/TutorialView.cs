using System;
using System.Collections.Generic;
using System.Linq;
using IdleCore;
using IdleCore.Economy;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// FTUE 튜토리얼 — 하단 탭 바 위에 안내 박스를 띄우고,
    /// 탭 진행 스텝과 조건 달성 스텝(강화 1회, 첫 소환 등)을 순서대로 소화한다.
    /// 완료 여부는 PlayerPrefs(뷰 레이어 관심사)로 저장 — 세이브 초기화와 독립.
    /// </summary>
    public sealed class TutorialView : MonoBehaviour
    {
        private const string DoneKey = "tutorial_done_v1";

        private sealed class Step
        {
            public string Message;
            /// <summary>null이면 탭해서 진행, 아니면 조건 달성 시 자동 진행</summary>
            public Func<bool> Condition;
            public Action OnEnter;
        }

        private GameSession _session;
        private List<Step> _steps;
        private int _current = -1;
        private Text _messageText;
        private Text _hintText;
        private Button _boxButton;

        public static bool IsDone => PlayerPrefs.GetInt(DoneKey, 0) == 1;

        public static TutorialView Create(Transform root, GameSession session)
        {
            var box = UIFactory.CreatePanel(root, "Tutorial", new Color(0.20f, 0.16f, 0.32f, 0.97f));
            UIFactory.BottomBand(box, 160, 250, 20);
            var view = box.gameObject.AddComponent<TutorialView>();
            view._session = session;
            view.Build(box);
            return view;
        }

        private void Build(RectTransform box)
        {
            var outline = box.gameObject.AddComponent<Outline>();
            outline.effectColor = UIFactory.Accent;
            outline.effectDistance = new Vector2(3, 3);

            _boxButton = box.gameObject.AddComponent<Button>();
            _boxButton.onClick.AddListener(OnBoxTapped);

            _messageText = UIFactory.CreateText(box, "Message", "", 27, TextAnchor.UpperLeft);
            UIFactory.Fill(_messageText.rectTransform, 28);
            _messageText.horizontalOverflow = HorizontalWrapMode.Wrap; // 박스 밖으로 흐르지 않게

            _hintText = UIFactory.CreateText(box, "Hint", "", 24, TextAnchor.LowerRight, UIFactory.Accent);
            UIFactory.Fill(_hintText.rectTransform, 18);

            _steps = BuildSteps();
            Advance();
        }

        private List<Step> BuildSteps()
        {
            // 조건 판정용 스냅샷
            int startLevel = _session.Stats.GetLevel("training_attack");
            bool grantedGems = false;

            return new List<Step>
            {
                new Step
                {
                    Message = "환영합니다, 신입 저승사자님.\n전투는 자동입니다 — 저승사자는 알아서 사냥합니다.",
                },
                new Step
                {
                    Message = "[성장] 탭에서 '수련: 낫질'을 강화해 보세요.\n골드는 사냥으로 자동으로 모입니다.",
                    Condition = () => _session.Stats.GetLevel("training_attack") > startLevel,
                },
                new Step
                {
                    Message = "강해지면 스테이지가 자동으로 올라갑니다.\n막히면 '벽' — 방치해서 재화를 모으거나 더 성장하면 뚫립니다.",
                },
                new Step
                {
                    Message = "이제 동료를 부를 시간입니다.\n[소환] 탭에서 10연 소환을 해보세요. (선물: 영옥 2,000 지급!)",
                    OnEnter = () =>
                    {
                        if (grantedGems) return;
                        grantedGems = true;
                        _session.Wallet.Earn(CurrencyIds.GemSoft, 2000);
                    },
                    Condition = () => _session.Units.AllOwned().Any(),
                },
                new Step
                {
                    Message = "소환한 차사는 자동 장착되어 전투력이 오릅니다.\n같은 차사를 또 뽑으면 '돌파' — 더 강해집니다.",
                },
                new Step
                {
                    Message = "마지막 비밀: 이 게임은 꺼도 됩니다.\n방치하는 동안 보상이 쌓입니다 (기본 8시간).\n[상점]의 월 정액은 방치 효율을 올려줍니다.\n\n행운을 빕니다, 저승사자님. 🌙",
                },
            };
        }

        private void OnBoxTapped()
        {
            // 조건 스텝은 탭으로 건너뛸 수 없다
            if (_steps[_current].Condition == null) Advance();
        }

        private void Advance()
        {
            _current++;
            if (_current >= _steps.Count)
            {
                PlayerPrefs.SetInt(DoneKey, 1);
                PlayerPrefs.Save();
                Destroy(gameObject);
                return;
            }
            var step = _steps[_current];
            step.OnEnter?.Invoke();
            _messageText.text = step.Message;
            _hintText.text = step.Condition == null ? "▶ 탭하여 계속" : "· · · 완료하면 자동 진행 · · ·";
        }

        private void Update()
        {
            // 플레이 중 스크립트 리로드(도메인 리로드)로 상태가 유실되면 조용히 닫는다
            if (_steps == null || _session == null || _messageText == null)
            {
                Destroy(gameObject);
                return;
            }
            if (_current < 0 || _current >= _steps.Count) return;
            var condition = _steps[_current].Condition;
            if (condition != null && condition()) Advance();
        }
    }
}
