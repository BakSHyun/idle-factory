using IdleCore;
using IdleCore.Economy;
using IdleCore.Progression;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>던전 탭 — 재화별 던전 도전/소탕/광고 소탕. 소탕권 경제의 소비처.</summary>
    public sealed class DungeonPanel : MonoBehaviour
    {
        public RectTransform Rect { get; private set; }
        private GameSession _session;
        private Text _ticketText, _resultText;
        private RectTransform _list;

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
            _list = UIFactory.CreateScrollList(Rect, spacing: 16);

            _ticketText = UIFactory.CreateText(_list, "Tickets", "", 30, TextAnchor.MiddleLeft, UIFactory.Gold);
            _ticketText.gameObject.AddComponent<LayoutElement>().preferredHeight = 50;

            _resultText = UIFactory.CreateText(_list, "Result", "", 27, TextAnchor.MiddleCenter, UIFactory.TextMain);
            _resultText.gameObject.AddComponent<LayoutElement>().preferredHeight = 90;

            foreach (var def in _session.Dungeons.Defs.Values)
                BuildDungeonRow(def);
        }

        private void BuildDungeonRow(DungeonDef def)
        {
            var box = UIFactory.CreatePanel(_list, $"D_{def.id}", UIFactory.Panel);
            box.gameObject.AddComponent<LayoutElement>().preferredHeight = 240;

            var title = UIFactory.CreateText(box, "Title", "", 30, TextAnchor.UpperLeft);
            UIFactory.Fill(title.rectTransform, 20);
            title.name = $"Title_{def.id}";

            var buttonBar = UIFactory.CreatePanel(box, "Buttons", UIFactory.Panel);
            UIFactory.BottomBand(buttonBar, 14, 90, 14);
            var layout = buttonBar.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.spacing = 10;

            UIFactory.CreateButton(buttonBar, "Challenge", "도전", () =>
            {
                var result = _session.Dungeons.TryChallenge(def.id, _session.Progression.HighestClearedIndex);
                ShowResult(def, result, "입장");
            }, UIFactory.Accent, 26);

            UIFactory.CreateButton(buttonBar, "Sweep", "소탕", () =>
            {
                var result = _session.Dungeons.TrySweep(def.id);
                ShowResult(def, result, result == null ? "소탕권 부족 또는 1층 미클리어" : "소탕");
            }, UIFactory.Gold, 26).GetComponentInChildren<Text>().color = Color.black;

            UIFactory.CreateButton(buttonBar, "AdSweep", "광고 소탕", () =>
            {
                if (!_session.Ads.CanUse("dungeon_sweep")) { _resultText.text = "오늘의 광고 소탕을 다 썼습니다"; return; }
                _session.Ads.Use("dungeon_sweep", ok =>
                {
                    if (!ok) return;
                    var result = _session.Dungeons.GrantAdSweep(def.id);
                    ShowResult(def, result, result == null ? "1층부터 클리어하세요" : "광고 소탕");
                });
            }, new Color(0.2f, 0.45f, 0.3f), 26);
        }

        private void ShowResult(DungeonDef def, DungeonRunResult result, string action)
        {
            if (result == null)
            {
                _resultText.text = $"{def.name}: {action} 실패 (해금·횟수 확인)";
            }
            else
            {
                string push = result.FloorsCleared > 0 ? $" (+{result.FloorsCleared}층 돌파!)" : "";
                _resultText.text =
                    $"{def.name} {result.HighestFloor}층{push}\n{CurrencyLabel(result.RewardCurrency)} +{UIFactory.FormatNumber(result.RewardAmount)}";
            }
            Refresh();
        }

        public void Refresh()
        {
            if (_session == null || _ticketText == null) return;
            _ticketText.text = $"소탕권 {_session.Wallet.Get(CurrencyIds.SweepTicket)}장  ·  " +
                               $"광고 소탕 {_session.Ads.RemainingToday("dungeon_sweep")}회 남음";

            foreach (var def in _session.Dungeons.Defs.Values)
            {
                var title = transform.GetComponentInChildren<ScrollRect>().content
                    .Find($"D_{def.id}/Title_{def.id}")?.GetComponent<Text>();
                if (title == null) continue;
                bool unlocked = _session.Dungeons.IsUnlocked(def.id, _session.Progression.HighestClearedIndex);
                var state = _session.Dungeons.State(def.id);
                var unlockStage = new StageId(def.unlockStageIndex);
                title.text = unlocked
                    ? $"{def.name}  —  최고 {state.highestFloorCleared}층\n" +
                      $"보상: {CurrencyLabel(def.rewardCurrency)}  ·  무료 입장 {_session.Dungeons.RemainingFreeEntries(def.id)}/{def.freeEntriesPerDay}"
                    : $"{def.name}  —  🔒 {unlockStage.Display(_session.Config.stage)} 해금";
            }
        }

        public static string CurrencyLabel(string currency) => currency switch
        {
            "gold" => "골드",
            "soul" => "영혼",
            "awaken_stone" => "각성석",
            "gem_soft" => "영옥",
            "sweep_ticket" => "소탕권",
            "mileage" => "마일리지",
            _ => currency,
        };
    }
}
