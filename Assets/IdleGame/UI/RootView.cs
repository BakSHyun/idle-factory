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

        private Text _stageText, _goldText, _soulText, _softText, _hardText, _dpsText, _killText, _powerText;
        private Button _bossButton;
        private Image _mobImage, _playerHpFill;
        private BattleAnimator _battleAnimator;
        private static readonly string[] MobIds = { "mob_wisp", "mob_gwisin", "mob_dokkaebi" };
        private GrowthPanel _growthPanel;
        private GachaPanel _gachaPanel;
        private DungeonPanel _dungeonPanel;
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

        private RectTransform _safeRoot;

        private void Build(Transform root)
        {
            var bg = UIFactory.CreatePanel(root, "Background", UIFactory.Bg);
            UIFactory.Fill(bg); // 배경은 노치 뒤까지 채운다

            // 세이프 에어리어: 카메라 펀치홀/노치를 피해서 UI 배치
            var safeGo = new GameObject("SafeArea", typeof(RectTransform));
            safeGo.transform.SetParent(root, false);
            _safeRoot = (RectTransform)safeGo.transform;
            var area = Screen.safeArea;
            _safeRoot.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
            _safeRoot.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
            _safeRoot.offsetMin = _safeRoot.offsetMax = Vector2.zero;
            root = _safeRoot;

            BuildHud(root);
            BuildBattleView(root);

            // 탭 패널 3종 (중앙~하단 탭 바 사이 영역)
            _growthPanel = GrowthPanel.Create(root, _session);
            _gachaPanel = GachaPanel.Create(root, _session);
            _dungeonPanel = DungeonPanel.Create(root, _session);
            _shopPanel = ShopPanel.Create(root, _session);
            _statusPanel = StatusPanel.Create(root, _session);
            _panels = new[] { _growthPanel.Rect, _gachaPanel.Rect, _dungeonPanel.Rect, _shopPanel.Rect, _statusPanel.Rect };

            BuildTabBar(root);
            ShowPanel(0);
            RefreshAll();

            if (!TutorialView.IsDone)
                TutorialView.Create(_safeRoot, _session);

            _session.Wallet.BalanceChanged += (_, _, _) => RefreshCurrencies();
            _session.Progression.StageCleared += _ => RefreshStage();
        }

        private void BuildHud(Transform root)
        {
            var hud = UIFactory.CreatePanel(root, "HUD", UIFactory.Panel);
            UIFactory.TopBand(hud, 0, 170);

            _stageText = UIFactory.CreateText(hud, "Stage", "1-1", 52, TextAnchor.MiddleLeft);
            UIFactory.TopBand(_stageText.rectTransform, 10, 70, 30);

            _powerText = UIFactory.CreateText(hud, "Power", "", 30, TextAnchor.MiddleRight, UIFactory.Gold);
            UIFactory.TopBand(_powerText.rectTransform, 14, 60, 30);
            _powerText.rectTransform.offsetMax = new Vector2(-210, _powerText.rectTransform.offsetMax.y); // 미션 버튼 회피

            _goldText = UIFactory.CreateText(hud, "Gold", "골드 0", 30, TextAnchor.MiddleLeft, UIFactory.Gold);
            UIFactory.TopBand(_goldText.rectTransform, 90, 40, 30);

            _soulText = UIFactory.CreateText(hud, "Soul", "영혼 0", 30, TextAnchor.MiddleCenter, UIFactory.Accent);
            UIFactory.TopBand(_soulText.rectTransform, 90, 40, 30);

            _softText = UIFactory.CreateText(hud, "Soft", "영옥 0", 30, TextAnchor.MiddleRight, UIFactory.TextMain);
            UIFactory.TopBand(_softText.rectTransform, 90, 40, 30);

            _hardText = UIFactory.CreateText(hud, "Hard", "수정 0", 30, TextAnchor.MiddleRight, new Color(1f, 0.5f, 0.55f));
            UIFactory.TopBand(_hardText.rectTransform, 130, 40, 30);

            var missionButton = UIFactory.CreateButton(hud, "MissionBtn", "📋 미션",
                () => MissionView.Open(_safeRoot, _session), UIFactory.Accent, 26);
            var missionRect = (RectTransform)missionButton.transform;
            missionRect.anchorMin = new Vector2(1, 1);
            missionRect.anchorMax = new Vector2(1, 1);
            missionRect.pivot = new Vector2(1, 1);
            missionRect.anchoredPosition = new Vector2(-24, -14);
            missionRect.sizeDelta = new Vector2(170, 62);
        }

        private void BuildBattleView(Transform root)
        {
            var battle = UIFactory.CreatePanel(root, "Battle", new Color(0.10f, 0.09f, 0.15f));
            UIFactory.TopBand(battle, 170, 420);

            // 배경 (저승길) — 있으면 깔기
            var bgSprite = UIFactory.LoadSprite("art/bg/bg_road.png");
            if (bgSprite != null)
            {
                var bgGo = new GameObject("BattleBg", typeof(RectTransform), typeof(Image));
                bgGo.transform.SetParent(battle, false);
                var bgImage = bgGo.GetComponent<Image>();
                bgImage.sprite = bgSprite;
                bgImage.color = new Color(1, 1, 1, 0.55f); // 캐릭터 가독성 위해 톤 다운
                UIFactory.Fill((RectTransform)bgGo.transform);
            }

            // 몬스터 (챕터별 로테이션) — RefreshStage에서 갱신
            var mobGo = new GameObject("Mob", typeof(RectTransform), typeof(Image));
            mobGo.transform.SetParent(battle, false);
            _mobImage = mobGo.GetComponent<Image>();
            _mobImage.preserveAspect = true;
            var mobRect = (RectTransform)mobGo.transform;
            mobRect.anchorMin = mobRect.anchorMax = new Vector2(0.78f, 0.55f);
            mobRect.sizeDelta = new Vector2(170, 170);

            // 메인 캐릭터: StreamingAssets/art의 스프라이트를 런타임 로드 (없으면 텍스트 폴백)
            var sprite = UIFactory.LoadSprite("art/main_character.png");
            if (sprite != null)
            {
                var artGo = new GameObject("ReaperArt", typeof(RectTransform), typeof(Image));
                artGo.transform.SetParent(battle, false);
                var image = artGo.GetComponent<Image>();
                image.sprite = sprite;
                image.preserveAspect = true;
                var artRect = (RectTransform)artGo.transform;
                artRect.anchorMin = artRect.anchorMax = new Vector2(0.28f, 0.5f);
                artRect.anchoredPosition = new Vector2(0, 10);
                artRect.sizeDelta = new Vector2(250, 250); // 좌측 아군 / 우측 몬스터 대치 구도
                // 체력바: 적(몬스터 위) + 아군(캐릭터 위)
                var mobHpFill = CreateHpBar(battle, new Vector2(0.78f, 0.78f), new Color(0.9f, 0.3f, 0.35f));
                _playerHpFill = CreateHpBar(battle, new Vector2(0.28f, 0.86f), new Color(0.35f, 0.85f, 0.45f));
                _battleAnimator = BattleAnimator.Attach(battle, artRect, _mobImage, mobHpFill);
            }
            else
            {
                var reaper = UIFactory.CreateText(battle, "ReaperArt", "🌙\n저승사자", 60);
                UIFactory.Fill(reaper.rectTransform);
            }

            _dpsText = UIFactory.CreateText(battle, "Dps", "DPS 0", 34, TextAnchor.LowerLeft, UIFactory.TextDim);
            UIFactory.Fill(_dpsText.rectTransform, 24);

            _killText = UIFactory.CreateText(battle, "Kills", "", 30, TextAnchor.LowerRight, UIFactory.TextDim);
            UIFactory.Fill(_killText.rectTransform, 24);

            // 보스 도전 — 벽을 '보이게' 만드는 연출 (예상 피해 % + 도전 버튼)
            _bossButton = UIFactory.CreateButton(battle, "BossBtn", "", OnBossChallenge, UIFactory.Accent, 28);
            var bossRect = (RectTransform)_bossButton.transform;
            bossRect.anchorMin = new Vector2(0.5f, 0);
            bossRect.anchorMax = new Vector2(0.5f, 0);
            bossRect.pivot = new Vector2(0.5f, 0);
            bossRect.anchoredPosition = new Vector2(0, 76);
            bossRect.sizeDelta = new Vector2(520, 82);
        }

        private static Image CreateHpBar(RectTransform parent, Vector2 anchor, Color color)
        {
            var barBg = UIFactory.CreatePanel(parent, "HpBar", new Color(0, 0, 0, 0.55f));
            barBg.anchorMin = barBg.anchorMax = anchor;
            barBg.sizeDelta = new Vector2(160, 16);
            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(barBg, false);
            var fill = fillGo.GetComponent<Image>();
            fill.color = color;
            var fr = (RectTransform)fillGo.transform;
            fr.anchorMin = new Vector2(0, 0);
            fr.anchorMax = new Vector2(1, 1);
            fr.offsetMin = new Vector2(2, 2);
            fr.offsetMax = new Vector2(-2, -2);
            return fill;
        }

        /// <summary>체력바 채움 비율 (anchorMax.x 조절 — 스프라이트 불필요)</summary>
        public static void SetHpFill(Image fill, float ratio)
        {
            if (fill == null) return;
            var rect = (RectTransform)fill.transform;
            rect.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1);
        }

        private void OnBossChallenge()
        {
            double ratio = _session.Progression.NextBossDamageRatio();
            if (_session.Progression.TryPush())
            {
                _killText.text = "보스 격파! 다음 구역으로";
                RefreshStage();
            }
            else
            {
                _killText.text = $"도전 실패 — 피해 {ratio * 100:0.#}%까지. 더 성장하세요";
            }
            RefreshBossButton();
        }

        private void RefreshBossButton()
        {
            if (_bossButton == null) return;
            var progression = _session.Progression;
            double ratio = progression.NextBossDamageRatio();
            var label = _bossButton.GetComponentInChildren<Text>();
            if (ratio <= 0) { _bossButton.gameObject.SetActive(false); return; }

            var nextStage = new StageId(progression.HighestClearedIndex + 1);
            bool ready = progression.CanClearNext();
            label.text = ready
                ? $"⚔ {nextStage.Display(_session.Config.stage)} 수문장 격파 가능!"
                : progression.BlockedBySurvival()
                    ? $"⚔ {nextStage.Display(_session.Config.stage)} 도전 — 화력은 충분, 체력 부족!"
                    : $"⚔ {nextStage.Display(_session.Config.stage)} 도전 — 예상 피해 {System.Math.Min(99.9, ratio * 100):0.#}%";
            _bossButton.image.color = ready ? new Color(0.85f, 0.35f, 0.35f) : UIFactory.Panel;
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

            string[] names = { "성장", "소환", "던전", "상점", "정보" };
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
            if (result.StagePushed || result.Retreated) RefreshStage();
            _killText.text = result.Retreated
                ? "💀 밀려났다! 체력을 키우세요"
                : result.Kills > 0
                    ? $"{UIFactory.FormatNumber(result.Kills / result.Seconds)}킬/초"
                    : "벽 — 성장 필요";
            var snapshot = _session.Stats.Snapshot();
            _battleAnimator?.SetRates(snapshot.Dps(),
                result.Seconds > 0 ? result.Kills / result.Seconds : 0);

            // 아군 체력바 = 생존 여유율 (적 반격 대비 유효 체력)
            var cfg = _session.Config.stage;
            int stage = _session.Progression.Current.Index;
            double enemyAttack = IdleCore.Progression.StageMath.EnemyAttack(cfg, stage);
            float ratio = 1f;
            if (enemyAttack > 0)
            {
                double killSeconds = IdleCore.Progression.StageMath.EnemyHp(cfg, stage) / System.Math.Max(0.0001, snapshot.Dps());
                double needed = enemyAttack * killSeconds * cfg.survivalKillBuffer;
                ratio = (float)System.Math.Min(1.0, snapshot.EffectiveHp() / System.Math.Max(1.0, needed * 1.5));
            }
            SetHpFill(_playerHpFill, ratio);
            if (_playerHpFill != null)
                _playerHpFill.color = ratio > 0.5f ? new Color(0.35f, 0.85f, 0.45f)
                    : ratio > 0.25f ? new Color(0.95f, 0.75f, 0.3f) : new Color(0.9f, 0.3f, 0.35f);

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
            _dungeonPanel.Refresh();
            _shopPanel.Refresh();
            _statusPanel.Refresh();
        }

        private void RefreshStage()
        {
            _stageText.text = $"스테이지 {_session.Progression.Current.Display(_session.Config.stage)}";
            var snapshot = _session.Stats.Snapshot();
            _dpsText.text = $"DPS {UIFactory.FormatNumber(snapshot.Dps())}";
            if (_powerText != null)
                _powerText.text = $"⚔ 전투력 {UIFactory.FormatNumber(snapshot.CombatPower())}";
            RefreshBossButton();

            if (_mobImage != null)
            {
                int chapter = _session.Progression.Current.Chapter(_session.Config.stage);
                var mob = UIFactory.LoadSprite($"art/units/{MobIds[chapter % MobIds.Length]}.png");
                _mobImage.sprite = mob;
                _mobImage.enabled = mob != null;
            }
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
