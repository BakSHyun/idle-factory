using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>코드 기반 uGUI 생성 헬퍼 — 프리팹/씬 없이 UI를 조립한다.</summary>
    public static class UIFactory
    {
        public const float HudHeight = 170f;
        // 전투가 제품의 첫 인상이다. 상단 절반 가까이를 확보하고 목록 UI는 아래에서 스크롤한다.
        public const float BattleHeight = 700f;
        public const float MainContentTop = HudHeight + BattleHeight;
        public const float TabBarHeight = 132f;
        public const float MainContentBottom = TabBarHeight + 12f;
        public const float ScreenGutter = 16f;

        // 먹빛 바탕 + 도깨비불 청록. 기존의 탁한 보라 단색보다 아트가 선명하게 뜬다.
        public static readonly Color Bg = new Color(0.035f, 0.045f, 0.075f);
        public static readonly Color Panel = new Color(0.075f, 0.09f, 0.14f);
        public static readonly Color PanelRaised = new Color(0.105f, 0.12f, 0.18f);
        public static readonly Color Accent = new Color(0.20f, 0.78f, 0.72f);
        public static readonly Color AccentDeep = new Color(0.10f, 0.46f, 0.48f);
        public static readonly Color Gold = new Color(1.00f, 0.73f, 0.28f);
        public static readonly Color TextMain = new Color(0.94f, 0.96f, 1.00f);
        public static readonly Color TextDim = new Color(0.58f, 0.65f, 0.74f);
        public static readonly Color Stroke = new Color(0.30f, 0.72f, 0.70f, 0.22f);

        /// <summary>속성 컬러 (불 주황 / 뇌 노랑 / 암 보라)</summary>
        public static Color ElementColor(string element) => element switch
        {
            IdleCore.Elements.Fire => new Color(1f, 0.55f, 0.3f),
            IdleCore.Elements.Lightning => new Color(1f, 0.9f, 0.35f),
            IdleCore.Elements.Dark => new Color(0.75f, 0.55f, 1f),
            _ => TextDim,
        };

        /// <summary>등급 컬러 토큰 — 텍스트/테두리/아이콘 배경에 일관 적용 (게이머 관습 준수).</summary>
        public static Color GradeColor(IdleCore.Gacha.UnitGrade grade) => grade switch
        {
            IdleCore.Gacha.UnitGrade.Beginner => new Color(0.62f, 0.62f, 0.66f),     // 회색
            IdleCore.Gacha.UnitGrade.Intermediate => new Color(0.45f, 0.80f, 0.45f), // 초록
            IdleCore.Gacha.UnitGrade.Advanced => new Color(0.38f, 0.62f, 0.95f),     // 파랑
            IdleCore.Gacha.UnitGrade.Rare => new Color(0.72f, 0.48f, 0.98f),         // 보라
            IdleCore.Gacha.UnitGrade.Epic => new Color(0.95f, 0.72f, 0.30f),         // 금색
            IdleCore.Gacha.UnitGrade.Mythic => new Color(0.98f, 0.42f, 0.48f),       // 적혼색
            IdleCore.Gacha.UnitGrade.Ancient => new Color(0.35f, 0.95f, 0.85f),      // 청록 (미출시)
            _ => new Color(0.98f, 0.95f, 0.75f),                                     // 영원 (미출시)
        };

        private static readonly System.Collections.Generic.Dictionary<string, Sprite> _spriteCache
            = new System.Collections.Generic.Dictionary<string, Sprite>();

        /// <summary>StreamingAssets PNG → 스프라이트 (캐시). 없으면 null.</summary>
        public static Sprite LoadSprite(string relativePath)
        {
            if (_spriteCache.TryGetValue(relativePath, out var cached)) return cached;
            Sprite sprite = null;
            try
            {
                string path = System.IO.Path.Combine(Application.streamingAssetsPath, relativePath);
                if (System.IO.File.Exists(path))
                {
                    var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (texture.LoadImage(System.IO.File.ReadAllBytes(path)))
                        sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                            new Vector2(0.5f, 0.5f), 100f);
                }
            }
            catch { /* 폴백: null */ }
            _spriteCache[relativePath] = sprite;
            return sprite;
        }

        private static Font _font;
        public static Font DefaultFont
        {
            get
            {
                if (_font != null) return _font;
                _font = Resources.Load<Font>("Fonts/Jua"); // 주아체 (OFL)
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _font;
            }
        }

        private static Sprite _rounded;
        /// <summary>절차 생성 라운드 렉트 (9-slice) — 모든 패널/버튼의 입체감 기본기.</summary>
        public static Sprite RoundedSprite
        {
            get
            {
                if (_rounded != null) return _rounded;
                const int size = 64, radius = 20;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        // 모서리 라운드 알파
                        float dx = Mathf.Max(0, Mathf.Max(radius - x, x - (size - 1 - radius)));
                        float dy = Mathf.Max(0, Mathf.Max(radius - y, y - (size - 1 - radius)));
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = Mathf.Clamp01(radius - d + 0.5f);
                        // 상단이 살짝 밝은 수직 그라데이션 (입체감)
                        float shade = Mathf.Lerp(0.92f, 1.08f, y / (float)(size - 1));
                        texture.SetPixel(x, y, new Color(shade, shade, shade, a));
                    }
                texture.Apply();
                _rounded = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
                    100f, 0, SpriteMeshType.FullRect, new Vector4(radius + 4, radius + 4, radius + 4, radius + 4));
                return _rounded;
            }
        }

        /// <summary>Image에 라운드 스프라이트 + 그림자 적용.</summary>
        public static void Roundify(Image image, bool shadow = true)
        {
            image.sprite = RoundedSprite;
            image.type = Image.Type.Sliced;
            var outline = image.gameObject.AddComponent<Outline>();
            outline.effectColor = Stroke;
            outline.effectDistance = new Vector2(1, -1);
            if (shadow)
            {
                var s = image.gameObject.AddComponent<Shadow>();
                s.effectColor = new Color(0, 0, 0, 0.32f);
                s.effectDistance = new Vector2(0, -3);
            }
        }

        public static Canvas CreateCanvas(string name)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920); // 세로 모바일 기준
            // 세로형 모바일 UI는 폭을 기준으로 고정해야 기기 비율에 따라 좌우가 잘리지 않는다.
            // 높이가 긴 기기에는 여백이 늘어나고, 모든 기기에서 1080 기준 가로 레이아웃이 유지된다.
            scaler.matchWidthOrHeight = 0f;

            if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var es = new GameObject("EventSystem",
                    typeof(UnityEngine.EventSystems.EventSystem),
                    typeof(UnityEngine.EventSystems.StandaloneInputModule));
                Object.DontDestroyOnLoad(es);
            }
            return canvas;
        }

        public static RectTransform CreatePanel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            return (RectTransform)go.transform;
        }

        public static Text CreateText(Transform parent, string name, string content, int size,
            TextAnchor anchor = TextAnchor.MiddleCenter, Color? color = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = DefaultFont;
            text.text = content;
            text.fontSize = size;
            text.alignment = anchor;
            text.color = color ?? TextMain;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.62f);
            shadow.effectDistance = new Vector2(1.5f, -2f);
            return text;
        }

        /// <summary>모든 버튼 클릭에 연결되는 사운드 훅 (AudioManager가 주입).</summary>
        public static System.Action OnAnyButtonClick;

        public static Button CreateButton(Transform parent, string name, string label,
            UnityEngine.Events.UnityAction onClick, Color? bg = null, int fontSize = 30)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = bg ?? Accent;
            Roundify(image);
            var button = go.GetComponent<Button>();
            button.onClick.AddListener(() => OnAnyButtonClick?.Invoke());
            button.onClick.AddListener(onClick);
            // 눌림/비활성 트랜지션 (입체감: 누르면 어두워짐)
            var colors = button.colors;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            colors.pressedColor = new Color(0.68f, 0.82f, 0.82f);
            colors.highlightedColor = new Color(1f, 1f, 1f);
            colors.disabledColor = new Color(0.35f, 0.40f, 0.46f, 0.62f);
            button.colors = colors;
            var label_ = CreateText(go.transform, "Label", label, fontSize);
            label_.fontStyle = FontStyle.Bold;
            Fill(label_.rectTransform);
            return button;
        }

        /// <summary>HUD용 작은 재화 칩. 흩어진 숫자를 한 덩어리로 읽히게 한다.</summary>
        public static RectTransform CreateChip(Transform parent, string name, Color tint)
        {
            var chip = CreatePanel(parent, name, Color.Lerp(PanelRaised, tint, 0.12f));
            Roundify(chip.GetComponent<Image>(), shadow: false);
            return chip;
        }

        /// <summary>부모를 가득 채우는 앵커</summary>
        public static void Fill(RectTransform rect, float margin = 0)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(margin, margin);
            rect.offsetMax = new Vector2(-margin, -margin);
        }

        /// <summary>상단 기준 고정 높이 밴드 (yTop: 위에서부터의 거리)</summary>
        public static void TopBand(RectTransform rect, float yTop, float height, float sideMargin = 0)
        {
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(0.5f, 1);
            rect.offsetMin = new Vector2(sideMargin, -yTop - height);
            rect.offsetMax = new Vector2(-sideMargin, -yTop);
        }

        /// <summary>하단 기준 고정 높이 밴드</summary>
        public static void BottomBand(RectTransform rect, float yBottom, float height, float sideMargin = 0)
        {
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(1, 0);
            rect.pivot = new Vector2(0.5f, 0);
            rect.offsetMin = new Vector2(sideMargin, yBottom);
            rect.offsetMax = new Vector2(-sideMargin, yBottom + height);
        }

        /// <summary>위/아래 오프셋만 고정하고 나머지를 채우는 앵커 — 화면비가 달라도 겹치지 않는다.</summary>
        public static void Stretch(RectTransform rect, float topOffset, float bottomOffset, float sideMargin = 0)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(sideMargin, bottomOffset);
            rect.offsetMax = new Vector2(-sideMargin, -topOffset);
        }

        /// <summary>세로 스크롤 목록 생성 — content에 자식을 추가하면 된다.</summary>
        public static RectTransform CreateScrollList(RectTransform parent, float spacing = 14)
        {
            var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect), typeof(Image), typeof(Mask));
            scrollGo.transform.SetParent(parent, false);
            Fill((RectTransform)scrollGo.transform);
            scrollGo.GetComponent<Image>().color = Bg;

            var content = CreatePanel(scrollGo.transform, "Content", Bg);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            AddVerticalList(content, spacing);
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30;
            return content;
        }

        public static VerticalLayoutGroup AddVerticalList(RectTransform rect, float spacing = 14, int padding = 20)
        {
            var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = new RectOffset(padding, padding, padding, padding);
            // childControlHeight=true 여야 LayoutElement.preferredHeight가 실제로 적용된다
            // (false면 모든 카드가 기본 높이로 뭉개져 텍스트 겹침/잘림 발생 — 반복됐던 레이아웃 버그의 근본 원인)
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            return layout;
        }

        public static string FormatNumber(double value)
        {
            if (value >= 1e12) return $"{value / 1e12:0.##}T";
            if (value >= 1e9) return $"{value / 1e9:0.##}B";
            if (value >= 1e6) return $"{value / 1e6:0.##}M";
            if (value >= 1e3) return $"{value / 1e3:0.##}K";
            return $"{value:0}";
        }
    }
}
