using System.Collections.Generic;
using IdleCore;
using IdleCore.Economy;
using IdleCore.Progression;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 던전 탭 — 카드형: 재화 아이콘 원형 + 이름/최고층 칩 + 무료 입장 점(●●○) +
    /// [도전][소탕][광고] 컬러 버튼. 결과는 상단 토스트 텍스트.
    /// </summary>
    public sealed class DungeonPanel : MonoBehaviour
    {
        public RectTransform Rect { get; private set; }
        private GameSession _session;
        private Text _ticketText, _resultText;
        private readonly List<(DungeonDef def, Text title, Text floor, Text pips, Button challenge, Button sweep, Button ad, Text lockText)> _cards
            = new List<(DungeonDef, Text, Text, Text, Button, Button, Button, Text)>();

        private static readonly Dictionary<string, Color> RewardColor = new Dictionary<string, Color>
        {
            { "gold", new Color(0.95f, 0.78f, 0.35f) },
            { "soul", new Color(0.62f, 0.5f, 0.95f) },
            { "awaken_stone", new Color(0.4f, 0.85f, 0.9f) },
        };

        public static DungeonPanel Create(Transform root, GameSession session)
        {
            var rect = UIFactory.CreatePanel(root, "DungeonPanel", UIFactory.Bg);
            UIFactory.Stretch(rect, 590, 150);
            var panel = rect.gameObject.AddComponent<DungeonPanel>();
            panel.Rect = rect;
            panel._session = session;
            panel.Build();
            return panel;
        }

        private void Build()
        {
            var list = UIFactory.CreateScrollList(Rect, spacing: 16);

            _ticketText = UIFactory.CreateText(list, "Tickets", "", 28, TextAnchor.MiddleCenter, UIFactory.Gold);
            _ticketText.gameObject.AddComponent<LayoutElement>().preferredHeight = 46;

            _resultText = UIFactory.CreateText(list, "Result", "", 26, TextAnchor.MiddleCenter, UIFactory.TextMain);
            _resultText.gameObject.AddComponent<LayoutElement>().preferredHeight = 70;

            foreach (var def in _session.Dungeons.Defs.Values)
                BuildCard(list, def);
        }

        private void BuildCard(RectTransform list, DungeonDef def)
        {
            var card = UIFactory.CreatePanel(list, $"D_{def.id}", UIFactory.Panel);
            UIFactory.Roundify(card.GetComponent<Image>());
            card.gameObject.AddComponent<LayoutElement>().preferredHeight = 250;

            var rewardColor = RewardColor.TryGetValue(def.rewardCurrency, out var c) ? c : UIFactory.Accent;

            // 재화 컬러 원형 배지
            var badge = UIFactory.CreatePanel(card, "Badge", rewardColor);
            UIFactory.Roundify(badge.GetComponent<Image>(), shadow: false);
            badge.anchorMin = badge.anchorMax = new Vector2(0, 1);
            badge.pivot = new Vector2(0, 1);
            badge.anchoredPosition = new Vector2(20, -18);
            badge.sizeDelta = new Vector2(72, 72);
            var badgeLabel = UIFactory.CreateText(badge, "L", GrowthPanel.CurrencyLabel(def.rewardCurrency).Substring(0, 1),
                34, TextAnchor.MiddleCenter, Color.black);
            UIFactory.Fill(badgeLabel.rectTransform);

            var title = UIFactory.CreateText(card, "Title", def.name, 31, TextAnchor.UpperLeft);
            UIFactory.TopBand(title.rectTransform, 22, 42, 110);

            var floor = UIFactory.CreateText(card, "Floor", "", 25, TextAnchor.UpperLeft, UIFactory.Gold);
            UIFactory.TopBand(floor.rectTransform, 66, 36, 110);

            var pips = UIFactory.CreateText(card, "Pips", "", 26, TextAnchor.UpperRight, UIFactory.TextDim);
            UIFactory.TopBand(pips.rectTransform, 24, 40, 24);

            // 버튼 3개
            var buttonBar = UIFactory.CreatePanel(card, "Buttons", UIFactory.Panel);
            buttonBar.GetComponent<Image>().color = new Color(0, 0, 0, 0);
            UIFactory.BottomBand(buttonBar, 16, 92, 16);
            var layout = buttonBar.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.spacing = 10;

            var challenge = UIFactory.CreateButton(buttonBar, "Go", " 도전", () =>
            {
                var result = _session.Dungeons.TryChallenge(def.id, _session.Progression.HighestClearedIndex);
                Toast(def, result, "입장 횟수가 없습니다");
            }, UIFactory.Accent, 26);

            var sweep = UIFactory.CreateButton(buttonBar, "Sweep", "", () =>
            {
                var result = _session.Dungeons.TrySweep(def.id);
                Toast(def, result, "소탕권 부족 또는 1층 미클리어");
            }, UIFactory.Gold, 25);
            sweep.GetComponentInChildren<Text>().color = Color.black;

            var ad = UIFactory.CreateButton(buttonBar, "Ad", "", () =>
            {
                if (!_session.Ads.CanUse("dungeon_sweep")) { _resultText.text = "오늘의 광고 소탕 소진"; return; }
                _session.Ads.Use("dungeon_sweep", ok =>
                {
                    if (!ok) return;
                    Toast(def, _session.Dungeons.GrantAdSweep(def.id), "1층부터 클리어하세요");
                });
            }, new Color(0.55f, 0.3f, 0.75f), 25);

            var lockText = UIFactory.CreateText(card, "Lock", "", 27, TextAnchor.MiddleCenter, UIFactory.TextDim);
            UIFactory.Fill(lockText.rectTransform);

            _cards.Add((def, title, floor, pips, challenge, sweep, ad, lockText));
        }

        private void Toast(DungeonDef def, DungeonRunResult result, string failMessage)
        {
            if (result == null) _resultText.text = $"{def.name}: {failMessage}";
            else
            {
                string push = result.FloorsCleared > 0 ? $"  (+{result.FloorsCleared}층 돌파!)" : "";
                _resultText.text = $"{def.name} {result.HighestFloor}층 — " +
                    $"{GrowthPanel.CurrencyLabel(result.RewardCurrency)} +{UIFactory.FormatNumber(result.RewardAmount)}{push}";
                AudioManager.Play("coin", 0.6f);
            }
            Refresh();
        }

        public void Refresh()
        {
            if (_session == null || _ticketText == null) return;
            _ticketText.text = $" 소탕권 {_session.Wallet.Get(CurrencyIds.SweepTicket)}장  ·  " +
                               $" 광고 소탕 {_session.Ads.RemainingToday("dungeon_sweep")}회";

            foreach (var (def, title, floor, pips, challenge, sweep, ad, lockText) in _cards)
            {
                bool unlocked = _session.Dungeons.IsUnlocked(def.id, _session.Progression.HighestClearedIndex);
                var state = _session.Dungeons.State(def.id);

                title.gameObject.SetActive(unlocked);
                floor.gameObject.SetActive(unlocked);
                pips.gameObject.SetActive(unlocked);
                challenge.gameObject.SetActive(unlocked);
                sweep.gameObject.SetActive(unlocked);
                ad.gameObject.SetActive(unlocked);

                if (!unlocked)
                {
                    var unlockStage = new StageId(def.unlockStageIndex);
                    lockText.text = $" {def.name} — {unlockStage.Display(_session.Config.stage)} 클리어 시 해금";
                    continue;
                }
                lockText.text = "";

                floor.text = $"최고 {state.highestFloorCleared}층 · 보상 {GrowthPanel.CurrencyLabel(def.rewardCurrency)} " +
                             $"{UIFactory.FormatNumber(def.FloorReward(Mathf.Max(1, state.highestFloorCleared)))}";

                int free = _session.Dungeons.RemainingFreeEntries(def.id);
                pips.text = string.Concat(System.Linq.Enumerable.Repeat("●", free))
                          + string.Concat(System.Linq.Enumerable.Repeat("○", def.freeEntriesPerDay - free));

                challenge.interactable = free > 0;
                challenge.GetComponentInChildren<Text>().text = free > 0 ? $" 도전 ({free})" : "내일 다시";
                long tickets = _session.Wallet.Get(CurrencyIds.SweepTicket);
                sweep.interactable = tickets > 0 && state.highestFloorCleared >= 1;
                sweep.GetComponentInChildren<Text>().text = $" 소탕";
                int adLeft = _session.Ads.RemainingToday("dungeon_sweep");
                ad.interactable = adLeft > 0 && state.highestFloorCleared >= 1;
                ad.GetComponentInChildren<Text>().text = $" 소탕 ({adLeft})";
            }
        }
    }
}
