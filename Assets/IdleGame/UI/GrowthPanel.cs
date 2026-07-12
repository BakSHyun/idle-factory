using System.Collections.Generic;
using IdleCore;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 성장 탭 — 카드형: 축 이름 + Lv 칩 + 현재 효과, 우측 큰 강화 버튼 (재화/비용 2줄).
    /// </summary>
    public sealed class GrowthPanel : MonoBehaviour
    {
        public RectTransform Rect { get; private set; }
        private GameSession _session;
        private readonly List<(string axisId, Text level, Text effect, Button button, Text buttonLabel, Text lockText)> _rows
            = new List<(string, Text, Text, Button, Text, Text)>();

        public static GrowthPanel Create(Transform root, GameSession session)
        {
            var rect = UIFactory.CreatePanel(root, "GrowthPanel", UIFactory.Bg);
            UIFactory.Stretch(rect, 590, 150);
            var panel = rect.gameObject.AddComponent<GrowthPanel>();
            panel.Rect = rect;
            panel._session = session;
            panel.Build();
            return panel;
        }

        private void Build()
        {
            var list = UIFactory.CreateScrollList(Rect, spacing: 14);

            foreach (var kv in _session.Stats.Axes)
            {
                string axisId = kv.Key;
                var axis = kv.Value;

                var card = UIFactory.CreatePanel(list, $"Axis_{axisId}", UIFactory.Panel);
                UIFactory.Roundify(card.GetComponent<Image>());
                card.gameObject.AddComponent<LayoutElement>().preferredHeight = 150;

                // 이름 + Lv 칩
                var name = UIFactory.CreateText(card, "Name", axis.name, 31, TextAnchor.UpperLeft);
                UIFactory.TopBand(name.rectTransform, 20, 44, 28);
                var level = UIFactory.CreateText(card, "Lv", "", 27, TextAnchor.UpperLeft, UIFactory.Gold);
                UIFactory.TopBand(level.rectTransform, 24, 40, 28);
                level.rectTransform.offsetMin = new Vector2(280, level.rectTransform.offsetMin.y);

                // 현재 효과 (흐리게)
                var effect = UIFactory.CreateText(card, "Effect", "", 24, TextAnchor.LowerLeft, UIFactory.TextDim);
                UIFactory.BottomBand(effect.rectTransform, 18, 40, 28);

                // 강화 버튼 (2줄: 강화 / 재화 비용)
                var button = UIFactory.CreateButton(card, "Up", "", () =>
                {
                    if (!_session.Stats.CanLevelUp(axisId)) return;
                    long cost = _session.Stats.NextCost(axisId);
                    if (!_session.Wallet.TrySpend(axis.costCurrency, cost)) return;
                    _session.Stats.LevelUp(axisId);
                    Refresh();
                }, UIFactory.Accent, 25);
                var buttonRect = (RectTransform)button.transform;
                buttonRect.anchorMin = new Vector2(1, 0.5f);
                buttonRect.anchorMax = new Vector2(1, 0.5f);
                buttonRect.pivot = new Vector2(1, 0.5f);
                buttonRect.anchoredPosition = new Vector2(-20, 0);
                buttonRect.sizeDelta = new Vector2(230, 112);

                // 잠금 안내 (해금 전용, 카드 전체 덮는 텍스트)
                var lockText = UIFactory.CreateText(card, "Lock", "", 26, TextAnchor.MiddleRight, UIFactory.TextDim);
                UIFactory.Fill(lockText.rectTransform, 24);

                _rows.Add((axisId, level, effect, button, button.GetComponentInChildren<Text>(), lockText));
            }
        }

        public void Refresh()
        {
            if (_session == null) return;
            foreach (var (axisId, level, effect, button, buttonLabel, lockText) in _rows)
            {
                var axis = _session.Stats.Axes[axisId];
                int lv = _session.Stats.GetLevel(axisId);
                bool unlocked = _session.Progression.HighestClearedIndex >= axis.unlockStageIndex;

                if (!unlocked)
                {
                    var stage = new IdleCore.Progression.StageId(axis.unlockStageIndex);
                    level.text = "";
                    effect.text = "";
                    lockText.text = $"🔒 {stage.Display(_session.Config.stage)} 클리어 시 해금";
                    button.gameObject.SetActive(false);
                    continue;
                }

                lockText.text = "";
                button.gameObject.SetActive(true);
                level.text = $"Lv.{lv}";
                effect.text = axis.effects.Count > 0 && lv > 0
                    ? "현재 " + UnitDetailView.Describe(axis.effects[0], lv)
                    : "강화하여 효과 획득";

                long cost = _session.Stats.NextCost(axisId);
                bool affordable = _session.Wallet.CanAfford(axis.costCurrency, cost)
                                  && _session.Stats.CanLevelUp(axisId);
                button.interactable = affordable;
                buttonLabel.text = $"강화\n{CurrencyLabel(axis.costCurrency)} {UIFactory.FormatNumber(cost)}";
            }
        }

        public static string CurrencyLabel(string currency) => currency switch
        {
            "gold" => "골드",
            "soul" => "영혼",
            "gem_soft" => "영옥",
            "gem_hard" => "수정",
            "awaken_stone" => "각성석",
            "sweep_ticket" => "소탕권",
            "mileage" => "마일리지",
            _ => currency,
        };
    }
}
