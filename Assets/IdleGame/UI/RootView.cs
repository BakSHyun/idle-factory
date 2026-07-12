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
        private Image _mobImage, _playerHpFill, _weaponImage, _gateFill;
        private Text _gateText;
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
            _session.Units.UnitChanged += _ => RefreshHeldWeapon();
            RefreshHeldWeapon();
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

            // 장착 무기 (캐릭터 등 뒤에 메는 연출 — 캐릭터보다 먼저 생성해 뒤에 깔림)
            var weaponGo = new GameObject("HeldWeapon", typeof(RectTransform), typeof(Image));
            weaponGo.transform.SetParent(battle, false);
            _weaponImage = weaponGo.GetComponent<Image>();
            _weaponImage.preserveAspect = true;
            _weaponImage.enabled = false;
            var weaponRect = (RectTransform)weaponGo.transform;
            weaponRect.anchorMin = weaponRect.anchorMax = new Vector2(0.28f, 0.5f);
            weaponRect.anchoredPosition = new Vector2(72, 88);   // 캐릭터 오른쪽 어깨 뒤
            weaponRect.sizeDelta = new Vector2(150, 150);
            weaponRect.localRotation = Quaternion.Euler(0, 0, -35f);

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

            // 처치 게이지: 몇 마리 잡으면 보스가 나오는지 — '왜 진행 안 되는지'의 답
            var gateBg = UIFactory.CreatePanel(battle, "GateBar", new Color(0, 0, 0, 0.55f));
            UIFactory.Roundify(gateBg.GetComponent<Image>(), shadow: false);
            gateBg.anchorMin = new Vector2(0.5f, 0);
            gateBg.anchorMax = new Vector2(0.5f, 0);
            gateBg.pivot = new Vector2(0.5f, 0);
            gateBg.anchoredPosition = new Vector2(0, 168);
            gateBg.sizeDelta = new Vector2(520, 40);
            var gateFillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            gateFillGo.transform.SetParent(gateBg, false);
            _gateFill = gateFillGo.GetComponent<Image>();
            _gateFill.color = UIFactory.Gold;
            UIFactory.Roundify(_gateFill, shadow: false);
            var gateFillRect = (RectTransform)gateFillGo.transform;
            gateFillRect.anchorMin = Vector2.zero;
            gateFillRect.anchorMax = new Vector2(0, 1);
            gateFillRect.offsetMin = new Vector2(3, 3);
            gateFillRect.offsetMax = new Vector2(-3, -3);
            _gateText = UIFactory.CreateText(gateBg, "T", "", 24, TextAnchor.MiddleCenter, UIFactory.TextMain);
            UIFactory.Fill(_gateText.rectTransform);

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

        /// <summary>장착 중인 낫을 캐릭터 등 뒤에 표시 — 장비 교체가 눈에 보인다.</summary>
        private void RefreshHeldWeapon()
        {
            if (_weaponImage == null) return;
            var equipped = System.Linq.Enumerable.FirstOrDefault(
                _session.Units.AllOwned(),
                u => u.equipped && _session.Units.Defs[u.unitId].kind == "weapon");
            if (equipped == null)
            {
                _weaponImage.enabled = false;
                return;
            }
            var def = _session.Units.Defs[equipped.unitId];
            var sprite = UIFactory.LoadSprite($"art/units/{def.artId ?? def.id}.png");
            _weaponImage.sprite = sprite;
            _weaponImage.enabled = sprite != null;
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
            var cfg = _session.Config.stage;
            double ratio = progression.NextBossDamageRatio();
            var label = _bossButton.GetComponentInChildren<Text>();
            if (ratio <= 0) { _bossButton.gameObject.SetActive(false); return; }

            // 처치 게이지 갱신
            int need = cfg.killsToBoss;
            long kills = progression.KillsOnStage;
            if (_gateFill != null && need > 0)
            {
                SetHpFill(_gateFill, Mathf.Clamp01(kills / (float)need));
                _gateText.text = progression.BossGateOpen
                    ? "🔥 수문장 등장!"
                    : $"몬스터 처치 {kills}/{need} — 다 잡으면 수문장 등장";
            }

            var nextStage = new StageId(progression.HighestClearedIndex + 1);
            string stageName = nextStage.Display(cfg);
            bool ready = progression.BossGateOpen && progression.CanClearNext();

            // 막힌 이유를 한 문장으로 — 유저가 '왜 안 가는지' 즉시 알 수 있게
            if (!progression.BossGateOpen)
                label.text = $"⚔ {stageName} 수문장 대기 중 ({kills}/{need} 처치)";
            else if (ready)
                label.text = $"⚔ {stageName} 수문장 격파 가능!";
            else if (progression.BlockedBySurvival())
                label.text = $"🛡 수문장이 너무 아프다 — 체력을 키우세요";
            else
                label.text = $"💪 수문장이 너무 단단하다 — 예상 피해 {System.Math.Min(99.9, ratio * 100):0.#}% (공격력 필요)";
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
            RefreshBossButton(); // 처치 게이지는 매 틱 갱신
            var snapshot = _session.Stats.Snapshot();
            var cfg = _session.Config.stage;
            int stage = _session.Progression.Current.Index;
            double enemyAttack = IdleCore.Progression.StageMath.EnemyAttack(cfg, stage);
            _battleAnimator?.SetRates(snapshot.Dps(),
                result.Seconds > 0 ? result.Kills / result.Seconds : 0,
                enemyAttack * 1.5); // 타격당 피해 표기 (1.5초 간격 환산)

            // 아군 체력바 = 생존 여유율 (적 반격 대비 유효 체력)
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
