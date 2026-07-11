using IdleCore;
using IdleCore.Economy;
using IdleCore.Progression;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 화면 전체: HUD(상단) + 전투 뷰(중앙) + 탭 패널(성장/소환/상점) + 하단 탭 바.
    /// </summary>
    public sealed class RootView : MonoBehaviour
    {
        private GameSession _session;

        private Text _stageText, _goldText, _soulText, _softText, _hardText, _dpsText, _killText;
        private GrowthPanel _growthPanel;
        private GachaPanel _gachaPanel;
        private ShopPanel _shopPanel;
        private StatusPanel _statusPanel;
        private RectTransform[] _panels;

        public static RootView Create(GameSession session)
        {
            var canvas = UIFactory.CreateCanvas("GameCanvas");
            Object.DontDestroyOnLoad(canvas.gameObject);
            var view = canvas.gameObject.AddComponent<RootView>();
            view._session = session;
            view.Build(canvas.transform);
            return view;
        }

        private void Build(Transform root)
        {
            var bg = UIFactory.CreatePanel(root, "Background", UIFactory.Bg);
            UIFactory.Fill(bg);

            BuildHud(root);
            BuildBattleView(root);

            // 탭 패널 3종 (중앙~하단 탭 바 사이 영역)
            _growthPanel = GrowthPanel.Create(root, _session);
            _gachaPanel = GachaPanel.Create(root, _session);
            _shopPanel = ShopPanel.Create(root, _session);
            _statusPanel = StatusPanel.Create(root, _session);
            _panels = new[] { _growthPanel.Rect, _gachaPanel.Rect, _shopPanel.Rect, _statusPanel.Rect };

            BuildTabBar(root);
            ShowPanel(0);
            RefreshAll();

            if (!TutorialView.IsDone)
                TutorialView.Create(transform, _session);

            _session.Wallet.BalanceChanged += (_, _, _) => RefreshCurrencies();
            _session.Progression.StageCleared += _ => RefreshStage();
        }

        private void BuildHud(Transform root)
        {
            var hud = UIFactory.CreatePanel(root, "HUD", UIFactory.Panel);
            UIFactory.TopBand(hud, 0, 170);

            _stageText = UIFactory.CreateText(hud, "Stage", "1-1", 52, TextAnchor.MiddleLeft);
            UIFactory.TopBand(_stageText.rectTransform, 10, 70, 30);

            _goldText = UIFactory.CreateText(hud, "Gold", "골드 0", 30, TextAnchor.MiddleLeft, UIFactory.Gold);
            UIFactory.TopBand(_goldText.rectTransform, 90, 40, 30);

            _soulText = UIFactory.CreateText(hud, "Soul", "영혼 0", 30, TextAnchor.MiddleCenter, UIFactory.Accent);
            UIFactory.TopBand(_soulText.rectTransform, 90, 40, 30);

            _softText = UIFactory.CreateText(hud, "Soft", "영옥 0", 30, TextAnchor.MiddleRight, UIFactory.TextMain);
            UIFactory.TopBand(_softText.rectTransform, 90, 40, 30);

            _hardText = UIFactory.CreateText(hud, "Hard", "수정 0", 30, TextAnchor.MiddleRight, new Color(1f, 0.5f, 0.55f));
            UIFactory.TopBand(_hardText.rectTransform, 130, 40, 30);
        }

        private void BuildBattleView(Transform root)
        {
            var battle = UIFactory.CreatePanel(root, "Battle", new Color(0.10f, 0.09f, 0.15f));
            UIFactory.TopBand(battle, 170, 420);

            UIFactory.CreateText(battle, "ReaperArt", "🌙\n저승사자", 60).rectTransform
                .anchoredPosition = new Vector2(0, 60);
            var reaper = battle.Find("ReaperArt").GetComponent<Text>();
            UIFactory.Fill(reaper.rectTransform);

            _dpsText = UIFactory.CreateText(battle, "Dps", "DPS 0", 34, TextAnchor.LowerLeft, UIFactory.TextDim);
            UIFactory.Fill(_dpsText.rectTransform, 24);

            _killText = UIFactory.CreateText(battle, "Kills", "", 30, TextAnchor.LowerRight, UIFactory.TextDim);
            UIFactory.Fill(_killText.rectTransform, 24);
        }

        private void BuildTabBar(Transform root)
        {
            var bar = UIFactory.CreatePanel(root, "TabBar", UIFactory.Panel);
            UIFactory.BottomBand(bar, 0, 140);
            var layout = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.spacing = 4;

            string[] names = { "성장", "소환", "상점", "정보" };
            for (int i = 0; i < names.Length; i++)
            {
                int index = i;
                UIFactory.CreateButton(bar, $"Tab_{names[i]}", names[i], () => ShowPanel(index),
                    UIFactory.Panel, 38);
            }
        }

        private void ShowPanel(int index)
        {
            for (int i = 0; i < _panels.Length; i++)
                _panels[i].gameObject.SetActive(i == index);
            RefreshAll();
        }

        public void OnFarmTick(FarmResult result)
        {
            if (result.StagePushed) RefreshStage();
            _killText.text = result.Kills > 0
                ? $"{UIFactory.FormatNumber(result.Kills / result.Seconds)}킬/초"
                : "벽 — 성장 필요";
            RefreshCurrencies();
        }

        public void ShowOfflinePopup(OfflineReward reward)
        {
            var overlay = UIFactory.CreatePanel(transform, "OfflineOverlay", new Color(0, 0, 0, 0.75f));
            UIFactory.Fill(overlay);
            var popup = UIFactory.CreatePanel(overlay, "Popup", UIFactory.Panel);
            popup.anchorMin = new Vector2(0.1f, 0.35f);
            popup.anchorMax = new Vector2(0.9f, 0.65f);
            popup.offsetMin = popup.offsetMax = Vector2.zero;

            UIFactory.CreateText(popup, "Title",
                $"방치 보상 ({reward.CreditedHours:0.#}시간)\n\n골드 {UIFactory.FormatNumber(reward.Gold)}\n영혼 {UIFactory.FormatNumber(reward.Soul)}",
                38);
            UIFactory.Fill(popup.Find("Title").GetComponent<Text>().rectTransform, 20);

            var claim = UIFactory.CreateButton(popup, "Claim", "받기", () => Destroy(overlay.gameObject));
            UIFactory.BottomBand((RectTransform)claim.transform, 20, 80, 60);
        }

        private void RefreshAll()
        {
            RefreshStage();
            RefreshCurrencies();
            _growthPanel.Refresh();
            _gachaPanel.Refresh();
            _shopPanel.Refresh();
            _statusPanel.Refresh();
        }

        private void RefreshStage()
        {
            _stageText.text = $"스테이지 {_session.Progression.Current.Display(_session.Config.stage)}";
            _dpsText.text = $"DPS {UIFactory.FormatNumber(_session.Stats.Snapshot().Dps())}";
        }

        private void RefreshCurrencies()
        {
            var w = _session.Wallet;
            _goldText.text = $"골드 {UIFactory.FormatNumber(w.Get(CurrencyIds.Gold))}";
            _soulText.text = $"영혼 {UIFactory.FormatNumber(w.Get(CurrencyIds.Soul))}";
            _softText.text = $"영옥 {UIFactory.FormatNumber(w.Get(CurrencyIds.GemSoft))}";
            _hardText.text = $"수정 {UIFactory.FormatNumber(w.Get(CurrencyIds.GemHard))}";
            _growthPanel.Refresh();
        }
    }
}
