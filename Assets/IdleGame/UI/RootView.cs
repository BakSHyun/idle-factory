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
        private Image _mobImage, _playerHpFill, _weaponImage, _gateFill, _bossMobFill;
        private Text _gateText;
        private RectTransform _partyContainer, _skillBar;
        private Text _mobElementText, _advantageText;
        private readonly System.Collections.Generic.List<(string unitId, Image overlay, RectTransform rect)> _skillIcons
            = new System.Collections.Generic.List<(string, Image, RectTransform)>();
        private BattleAnimator _battleAnimator;
        private static readonly string[] MobIds = { "mob_wisp", "mob_gwisin", "mob_dokkaebi" };
        private GrowthPanel _growthPanel;
        private EquipPanel _equipPanel;
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
            _equipPanel = EquipPanel.Create(root, _session);
            _gachaPanel = GachaPanel.Create(root, _session);
            _dungeonPanel = DungeonPanel.Create(root, _session);
            _shopPanel = ShopPanel.Create(root, _session);
            _statusPanel = StatusPanel.Create(root, _session);
            _panels = new[] { _growthPanel.Rect, _equipPanel.Rect, _gachaPanel.Rect, _dungeonPanel.Rect, _shopPanel.Rect, _statusPanel.Rect };

            BuildTabBar(root);
            ShowPanel(0);
            RefreshAll();

            if (!TutorialView.IsDone)
                TutorialView.Create(_safeRoot, _session);

            _session.Wallet.BalanceChanged += (_, _, _) => RefreshCurrencies();
            _session.Progression.StageCleared += _ => RefreshStage();
            _session.Units.UnitChanged += _ => { RefreshHeldWeapon(); RefreshSkillIcons(); };
            _session.SkillCast += OnSkillCast;
            RefreshHeldWeapon();
            RefreshSkillIcons();
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

            // 장착 파티(차사 미니/오브) 컨테이너 — 캐릭터보다 먼저 생성해 뒤에 깔림
            var partyGo = new GameObject("Party", typeof(RectTransform));
            partyGo.transform.SetParent(battle, false);
            _partyContainer = (RectTransform)partyGo.transform;
            UIFactory.Fill(_partyContainer);

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
                _bossMobFill = CreateHpBar(battle, new Vector2(0.78f, 0.78f), new Color(0.9f, 0.3f, 0.35f));
                _playerHpFill = CreateHpBar(battle, new Vector2(0.28f, 0.86f), new Color(0.35f, 0.85f, 0.45f));
                _battleAnimator = BattleAnimator.Attach(battle, artRect, _mobImage, _bossMobFill, _playerHpFill);
            }
            else
            {
                var reaper = UIFactory.CreateText(battle, "ReaperArt", "🌙\n저승사자", 60);
                UIFactory.Fill(reaper.rectTransform);
            }

            _dpsText = UIFactory.CreateText(battle, "Dps", "DPS 0", 34, TextAnchor.LowerLeft, UIFactory.TextDim);
            UIFactory.Fill(_dpsText.rectTransform, 24);

            // 스킬 쿨타임 아이콘 바 (우하단) — 장착 스킬이 자동 시전되는 게 보인다
            var skillBarGo = new GameObject("SkillBar", typeof(RectTransform));
            skillBarGo.transform.SetParent(battle, false);
            _skillBar = (RectTransform)skillBarGo.transform;
            _skillBar.anchorMin = _skillBar.anchorMax = new Vector2(1, 0);
            _skillBar.pivot = new Vector2(1, 0);
            _skillBar.anchoredPosition = new Vector2(-18, 170);
            _skillBar.sizeDelta = new Vector2(4 * 92, 86);
            var skillLayout = skillBarGo.AddComponent<HorizontalLayoutGroup>();
            skillLayout.childControlWidth = false;
            skillLayout.childControlHeight = false;
            skillLayout.childAlignment = TextAnchor.LowerRight;
            skillLayout.spacing = 8;

            _killText = UIFactory.CreateText(battle, "Kills", "", 30, TextAnchor.LowerRight, UIFactory.TextDim);
            UIFactory.Fill(_killText.rectTransform, 24);

            // 속성 표시: 몬스터 속성(우상단) + 내 파티 상성(좌상단)
            _mobElementText = UIFactory.CreateText(battle, "MobElem", "", 26, TextAnchor.UpperRight, UIFactory.TextMain);
            UIFactory.Fill(_mobElementText.rectTransform, 20);
            _advantageText = UIFactory.CreateText(battle, "Adv", "", 26, TextAnchor.UpperLeft, UIFactory.TextMain);
            UIFactory.Fill(_advantageText.rectTransform, 20);

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

        /// <summary>전투 중 타격 데미지 숫자 (보스전용).</summary>
        private void SpawnFightText(Vector2 anchor, string message, Color color)
        {
            var battle = transform.Find("SafeArea/Battle");
            if (battle == null) return;
            var text = UIFactory.CreateText(battle, "FDmg", message, 34, TextAnchor.MiddleCenter, color);
            text.fontStyle = FontStyle.Bold;
            text.rectTransform.anchorMin = text.rectTransform.anchorMax = anchor;
            text.rectTransform.anchoredPosition = new Vector2(Random.Range(-26f, 26f), 0);
            StartCoroutine(FloatFade(text));
        }

        private System.Collections.IEnumerator FloatFade(Text text)
        {
            var start = text.rectTransform.anchoredPosition;
            var baseColor = text.color;
            for (float t = 0; t < 1f; t += Time.deltaTime / 0.7f)
            {
                text.rectTransform.anchoredPosition = start + new Vector2(0, t * 64f);
                text.color = new Color(baseColor.r, baseColor.g, baseColor.b, 1f - t * t);
                yield return null;
            }
            Destroy(text.gameObject);
        }

        private System.Collections.IEnumerator PunchScale(RectTransform rect, float baseScale)
        {
            for (float t = 0; t < 1f; t += Time.deltaTime / 0.18f)
            {
                rect.localScale = Vector3.one * (baseScale + Mathf.Sin(t * Mathf.PI) * 0.12f);
                yield return null;
            }
            rect.localScale = Vector3.one * baseScale;
        }

        /// <summary>장착 시각화: 낫(등 뒤) + 차사 미니(파티) + 오브(부유) — 장비 교체가 눈에 보인다.</summary>
        private void RefreshHeldWeapon()
        {
            if (_weaponImage != null)
            {
                var weapon = System.Linq.Enumerable.FirstOrDefault(
                    _session.Units.AllOwned(),
                    u => u.equipped && _session.Units.Defs[u.unitId].kind == "weapon");
                if (weapon == null) _weaponImage.enabled = false;
                else
                {
                    var def = _session.Units.Defs[weapon.unitId];
                    var sprite = UIFactory.LoadSprite($"art/units/{def.artId ?? def.id}.png");
                    _weaponImage.sprite = sprite;
                    _weaponImage.enabled = sprite != null;
                }
            }

            // 차사 미니 파티 + 오브
            if (_partyContainer == null) return;
            foreach (Transform child in _partyContainer) Destroy(child.gameObject);

            var heroAnchors = new[]
            {
                new Vector2(0.09f, 0.40f), new Vector2(0.15f, 0.62f),
                new Vector2(0.42f, 0.36f), new Vector2(0.44f, 0.64f),
            };
            int heroIndex = 0;
            foreach (var unit in _session.Units.AllOwned())
            {
                if (!unit.equipped) continue;
                var def = _session.Units.Defs[unit.unitId];
                if (def.kind == "hero" && heroIndex < heroAnchors.Length)
                {
                    AddBattleSprite(def, heroAnchors[heroIndex], 118);
                    heroIndex++;
                }
                else if (def.kind == "orb")
                {
                    AddBattleSprite(def, new Vector2(0.38f, 0.78f), 76);
                }
            }
        }

        /// <summary>장착 스킬 아이콘 재구성 (아이콘 + 쿨타임 오버레이).</summary>
        private void RefreshSkillIcons()
        {
            if (_skillBar == null) return;
            foreach (Transform child in _skillBar) Destroy(child.gameObject);
            _skillIcons.Clear();

            foreach (var unit in _session.Units.AllOwned())
            {
                if (!unit.equipped) continue;
                var def = _session.Units.Defs[unit.unitId];
                if (def.kind != "skill" || def.activeSkill == null) continue;
                if (_skillIcons.Count >= 4) break;

                var slot = UIFactory.CreatePanel(_skillBar, $"SK_{def.id}", new Color(0, 0, 0, 0.5f));
                UIFactory.Roundify(slot.GetComponent<Image>(), shadow: false);
                slot.sizeDelta = new Vector2(84, 84);
                var outline = slot.gameObject.AddComponent<Outline>();
                outline.effectColor = UIFactory.GradeColor(def.grade);
                outline.effectDistance = new Vector2(2, 2);

                var sprite = UIFactory.LoadSprite($"art/units/{def.artId ?? def.id}.png");
                if (sprite != null)
                {
                    var iconGo = new GameObject("I", typeof(RectTransform), typeof(Image));
                    iconGo.transform.SetParent(slot, false);
                    var iconImage = iconGo.GetComponent<Image>();
                    iconImage.sprite = sprite;
                    iconImage.preserveAspect = true;
                    UIFactory.Fill((RectTransform)iconGo.transform, 5);
                }

                // 쿨타임 오버레이: 위에서부터 어둡게 덮였다가 준비되면 사라짐
                var overlayGo = new GameObject("CD", typeof(RectTransform), typeof(Image));
                overlayGo.transform.SetParent(slot, false);
                var overlay = overlayGo.GetComponent<Image>();
                overlay.color = new Color(0, 0, 0, 0.65f);
                overlay.raycastTarget = false;
                var overlayRect = (RectTransform)overlayGo.transform;
                overlayRect.anchorMin = new Vector2(0, 0);
                overlayRect.anchorMax = new Vector2(1, 1);
                overlayRect.offsetMin = overlayRect.offsetMax = Vector2.zero;

                _skillIcons.Add((unit.unitId, overlay, slot));
            }
        }

        private void OnSkillCast(string unitId)
        {
            AudioManager.Play("hit", 0.35f);
            foreach (var (id, _, rect) in _skillIcons)
                if (id == unitId) StartCoroutine(PunchScale(rect, 1f));
            // 차사 스킬이면 해당 미니 차사가 돌진 연출
            var party = _partyContainer != null ? _partyContainer.Find($"P_{unitId}") : null;
            party?.GetComponent<PartyBob>()?.Lunge();
            // 시전 이펙트: 몬스터 위에 스킬 아트 플래시
            var def = _session.Units.Defs[unitId];
            var sprite = UIFactory.LoadSprite($"art/units/{def.artId ?? def.id}.png");
            var battle = transform.Find("SafeArea/Battle");
            if (sprite == null || battle == null) return;
            var fx = new GameObject("SkillFx", typeof(RectTransform), typeof(Image));
            fx.transform.SetParent(battle, false);
            var fxImage = fx.GetComponent<Image>();
            fxImage.sprite = sprite;
            fxImage.preserveAspect = true;
            fxImage.raycastTarget = false;
            var fxRect = (RectTransform)fx.transform;
            fxRect.anchorMin = fxRect.anchorMax = new Vector2(0.78f, 0.55f);
            fxRect.sizeDelta = new Vector2(200, 200);
            StartCoroutine(FadeOutAndDestroy(fxImage));
        }

        private System.Collections.IEnumerator FadeOutAndDestroy(Image image)
        {
            for (float t = 0; t < 1f; t += Time.deltaTime / 0.5f)
            {
                if (image == null) yield break;
                image.color = new Color(1, 1, 1, 1f - t);
                ((RectTransform)image.transform).sizeDelta = Vector2.one * (200 + t * 60);
                yield return null;
            }
            if (image != null) Destroy(image.gameObject);
        }

        private void AddBattleSprite(IdleCore.Gacha.UnitDef def, Vector2 anchor, float size)
        {
            var sprite = UIFactory.LoadSprite($"art/units/{def.artId ?? def.id}.png");
            if (sprite == null) return;
            var go = new GameObject($"P_{def.id}", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_partyContainer, false);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.sizeDelta = new Vector2(size, size);
            go.AddComponent<PartyBob>(); // 생동감: 숨쉬기 + 폴짝
        }

        /// <summary>체력바 채움 비율 (anchorMax.x 조절 — 스프라이트 불필요)</summary>
        public static void SetHpFill(Image fill, float ratio)
        {
            if (fill == null) return;
            var rect = (RectTransform)fill.transform;
            rect.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1);
        }

        private bool _bossFighting;

        private void OnBossChallenge()
        {
            if (_bossFighting || !_session.Progression.BossGateOpen) return;
            StartCoroutine(BossFightSequence());
        }

        /// <summary>
        /// 실제 보스전 연출: 시뮬레이션 타임라인을 압축 재생 —
        /// 보스 체력 vs 내 체력 vs 제한시간의 경주를 눈으로 보고, 결과가 그대로 판정.
        /// </summary>
        private System.Collections.IEnumerator BossFightSequence()
        {
            _bossFighting = true;
            _battleAnimator?.SetFighting(true); // 파밍 연출 정지 (보스전 체력바와 충돌 방지)
            var preview = _session.Progression.SimulateBossFight();
            var label = _bossButton.GetComponentInChildren<Text>();
            _bossButton.interactable = false;
            SetHpFill(_bossMobFill, 1f);
            SetHpFill(_playerHpFill, 1f);

            // 수문장 등장 연출: 몬스터 확대 + 붉은 기운
            var mobRect = _mobImage != null ? (RectTransform)_mobImage.transform : null;
            if (mobRect != null) { mobRect.localScale = Vector3.one * 1.5f; _mobImage.color = new Color(1f, 0.75f, 0.75f); }
            _gateText.text = "⚔ 수문장 전투!";
            AudioManager.Play("hit", 0.8f);

            // 타격 교환식 재생: 때릴 때마다 그 데미지만큼 뚝뚝 닳는다 (독처럼 스르륵 X)
            float simLength = (float)System.Math.Min(preview.EndTime, preview.TimeLimit);
            int next = _session.Progression.HighestClearedIndex + 1;
            double bossHpTotal = IdleCore.Progression.StageMath.BossHp(_session.Config.stage, next);
            double bossDps = IdleCore.Progression.StageMath.EnemyAttack(_session.Config.stage, next)
                             * _session.Config.stage.bossAttackMultiplier;
            double killFrac = System.Math.Min(1.0, simLength / preview.TimeToKillBoss);
            double dieFrac = double.IsInfinity(preview.TimeToDie)
                ? 0 : System.Math.Min(1.0, simLength / preview.TimeToDie);
            const int exchanges = 7;

            for (int i = 1; i <= exchanges; i++)
            {
                // 내 공격 → 보스 체력이 타격량만큼 감소
                AudioManager.Play("hit", 0.55f);
                SpawnFightText(new Vector2(0.78f, 0.62f),
                    "-" + UIFactory.FormatNumber(bossHpTotal * killFrac / exchanges), UIFactory.Gold);
                SetHpFill(_bossMobFill, Mathf.Clamp01(1f - (float)(killFrac * i / exchanges)));
                if (mobRect != null) StartCoroutine(PunchScale(mobRect, 1.5f));
                label.text = $"⏱ {System.Math.Max(0, preview.TimeLimit - simLength * i / exchanges):0.0}초";
                yield return new WaitForSeconds(0.28f);

                // 보스 반격 → 내 체력이 타격량만큼 감소
                if (bossDps > 0)
                {
                    AudioManager.Play("hurt", 0.4f);
                    SpawnFightText(new Vector2(0.28f, 0.66f),
                        "-" + UIFactory.FormatNumber(bossDps * simLength / exchanges),
                        new Color(1f, 0.35f, 0.4f));
                    SetHpFill(_playerHpFill, Mathf.Clamp01(1f - (float)(dieFrac * i / exchanges)));
                }
                yield return new WaitForSeconds(0.30f);
            }

            // 판정 — 시뮬레이션 결과 그대로
            if (preview.Victory && _session.Progression.TryPush())
            {
                _gateText.text = "🔥 수문장 격파!";
                _killText.text = "격파! 다음 구역으로";
                AudioManager.Play("victory", 0.8f);
            }
            else if (preview.Reason == "death")
            {
                _gateText.text = "💀 쓰러졌다...";
                _killText.text = "수문장에게 당했다 — 체력을 키우세요";
                AudioManager.Play("hurt", 0.7f);
            }
            else
            {
                _gateText.text = "⏱ 시간 초과!";
                _killText.text = "제한시간 안에 못 잡았다 — 공격력을 키우세요";
                AudioManager.Play("hurt", 0.7f);
            }

            if (mobRect != null) { mobRect.localScale = Vector3.one; _mobImage.color = Color.white; }
            _battleAnimator?.SetFighting(false); // 파밍 연출 재개 + 체력바 리셋
            _bossButton.interactable = true;
            _bossFighting = false;
            RefreshStage();
        }

        private void RefreshBossButton()
        {
            if (_bossButton == null || _bossFighting) return; // 전투 중엔 코루틴이 라벨 소유
            var progression = _session.Progression;
            var cfg = _session.Config.stage;
            var label = _bossButton.GetComponentInChildren<Text>();

            // 처치 게이지 갱신
            int need = cfg.killsToBoss;
            long kills = progression.KillsOnStage;
            if (_gateFill != null && need > 0)
            {
                SetHpFill(_gateFill, Mathf.Clamp01(kills / (float)need));
                _gateText.text = progression.BossGateOpen
                    ? "🔥 수문장이 나타났다!"
                    : $"몬스터 처치 {kills}/{need}";
            }

            var nextStage = new StageId(progression.HighestClearedIndex + 1);
            bool open = progression.BossGateOpen;
            // 결과는 싸워봐야 안다 — 사전 판정 노출 없음
            label.text = open
                ? $"⚔ {nextStage.Display(cfg)} 수문장 도전!"
                : $"수문장 대기 중 ({kills}/{need} 처치)";
            _bossButton.interactable = open;
            _bossButton.image.color = open ? new Color(0.85f, 0.35f, 0.35f) : UIFactory.Panel;
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

            string[] names = { "성장", "편성", "소환", "던전", "상점", "정보" };
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

            // 스킬 쿨타임 오버레이 갱신 (proc형은 오버레이 없음 — 확률 발동)
            foreach (var (unitId, overlay, _) in _skillIcons)
            {
                var skillDef = _session.Units.Defs[unitId];
                if (skillDef.activeSkill == null || skillDef.activeSkill.trigger == "proc")
                {
                    overlay.enabled = false;
                    continue;
                }
                double cooldown = _session.EffectiveSkill(unitId).cooldown;
                float cdRatio = cooldown > 0
                    ? (float)(_session.SkillCooldownRemaining(unitId) / cooldown) : 0;
                ((RectTransform)overlay.transform).anchorMax = new Vector2(1, Mathf.Clamp01(cdRatio));
                overlay.enabled = cdRatio > 0.02f;
            }

            var snapshot = _session.Stats.Snapshot();
            var cfg = _session.Config.stage;
            int stage = _session.Progression.Current.Index;
            double enemyAttack = IdleCore.Progression.StageMath.EnemyAttack(cfg, stage);
            double perHit = enemyAttack * 1.5; // 타격당 피해 (1.5초 간격 환산)
            _battleAnimator?.SetRates(snapshot.Dps(),
                result.Seconds > 0 ? result.Kills / result.Seconds : 0,
                perHit,
                perHit / System.Math.Max(1.0, snapshot.EffectiveHp())); // 타격당 체력 감소 비율

            // 체력바 색 = 생존 여유 (감소는 애니메이터가 타격마다 실제 수치로 반영)
            if (_playerHpFill != null && enemyAttack > 0)
            {
                double killSeconds = IdleCore.Progression.StageMath.EnemyHp(cfg, stage) / System.Math.Max(0.0001, snapshot.Dps());
                double needed = enemyAttack * killSeconds * cfg.survivalKillBuffer;
                float margin = (float)System.Math.Min(1.0, snapshot.EffectiveHp() / System.Math.Max(1.0, needed * 1.5));
                _playerHpFill.color = margin > 0.5f ? new Color(0.35f, 0.85f, 0.45f)
                    : margin > 0.25f ? new Color(0.95f, 0.75f, 0.3f) : new Color(0.9f, 0.3f, 0.35f);
            }

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
            _equipPanel.Refresh();
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

                // 속성 표기 + 상성 안내
                string mobElement = IdleCore.Elements.MobElement(chapter);
                _mobElementText.text = $"{IdleCore.Elements.Icon(mobElement)} {IdleCore.Elements.Label(mobElement)} 속성";
                double advantage = _session.ElementAdvantage();
                if (System.Math.Abs(advantage) < 0.001)
                {
                    _advantageText.text = "";
                }
                else if (advantage > 0)
                {
                    _advantageText.text = $"상성 유리 +{advantage * 100:0}%";
                    _advantageText.color = new Color(0.45f, 0.9f, 0.5f);
                }
                else
                {
                    _advantageText.text = $"상성 불리 {advantage * 100:0}%";
                    _advantageText.color = new Color(0.95f, 0.45f, 0.5f);
                }
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
