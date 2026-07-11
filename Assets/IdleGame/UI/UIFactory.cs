using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>코드 기반 uGUI 생성 헬퍼 — 프리팹/씬 없이 UI를 조립한다.</summary>
    public static class UIFactory
    {
        public static readonly Color Bg = new Color(0.08f, 0.07f, 0.12f);        // 저승 남색
        public static readonly Color Panel = new Color(0.14f, 0.12f, 0.20f);
        public static readonly Color Accent = new Color(0.55f, 0.45f, 0.95f);    // 도깨비불 보라
        public static readonly Color Gold = new Color(0.95f, 0.78f, 0.35f);
        public static readonly Color TextMain = new Color(0.92f, 0.90f, 0.97f);
        public static readonly Color TextDim = new Color(0.62f, 0.60f, 0.70f);

        private static Font _font;
        public static Font DefaultFont =>
            _font != null ? _font : _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        public static Canvas CreateCanvas(string name)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920); // 세로 모바일 기준
            scaler.matchWidthOrHeight = 0.5f;

            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
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
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        public static Button CreateButton(Transform parent, string name, string label,
            UnityEngine.Events.UnityAction onClick, Color? bg = null, int fontSize = 30)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = bg ?? Accent;
            var button = go.GetComponent<Button>();
            button.onClick.AddListener(onClick);
            var label_ = CreateText(go.transform, "Label", label, fontSize);
            Fill(label_.rectTransform);
            return button;
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
        public static void Stretch(RectTransform rect, float topOffset, float bottomOffset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(0, bottomOffset);
            rect.offsetMax = new Vector2(0, -topOffset);
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
            layout.childControlHeight = false;
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
