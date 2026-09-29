using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace GuildMaster.UI
{
    /// <summary>
    /// Сборка элементов интерфейса кодом: панели, тексты, кнопки, прокрутка, поля ввода, раскладки. Цвета, шрифт и размеры —
    /// из <see cref="UiTheme"/>. Экраны собираются из этих деталей, префабов нет.
    /// </summary>
    public sealed class UiFactory
    {
        public UiFactory(UiTheme theme)
        {
            Theme = theme;
            Font = theme.Font != null ? theme.Font : TMP_Settings.defaultFontAsset;
        }

        public UiTheme Theme { get; }
        public TMP_FontAsset Font { get; }

        // ---------- Основа ----------

        /// <summary>Удалить объект интерфейса: в игре — в конце кадра, вне игры (тесты, редактор) — сразу.</summary>
        public static void DestroyObject(GameObject target)
        {
            if (target == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(target);
            else UnityEngine.Object.DestroyImmediate(target);
        }

        public static RectTransform Node(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        /// <summary>Растянуть на весь родитель с отступами (слева, снизу, справа, сверху).</summary>
        public static RectTransform Stretch(RectTransform rect, float left = 0, float bottom = 0, float right = 0, float top = 0)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
            return rect;
        }

        /// <summary>Полоса у верхнего края родителя заданной высоты.</summary>
        public static RectTransform TopStrip(RectTransform rect, float height)
        {
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(0.5f, 1);
            rect.offsetMin = new Vector2(0, -height);
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        /// <summary>Прямоугольник по центру родителя заданного размера (для окон).</summary>
        public static RectTransform Centered(RectTransform rect, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = Vector2.zero;
            return rect;
        }

        public static LayoutElement Size(Component target, float width = -1, float height = -1, float flexWidth = -1, float flexHeight = -1)
        {
            if (!target.TryGetComponent(out LayoutElement layout)) layout = target.gameObject.AddComponent<LayoutElement>();
            if (width >= 0) layout.preferredWidth = layout.minWidth = width;
            if (height >= 0) layout.preferredHeight = layout.minHeight = height;
            if (flexWidth >= 0) layout.flexibleWidth = flexWidth;
            if (flexHeight >= 0) layout.flexibleHeight = flexHeight;
            return layout;
        }

        public Image Panel(Transform parent, string name, Color color, bool blocksClicks = false)
        {
            RectTransform rect = Node(parent, name);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = blocksClicks;
            return image;
        }

        public VerticalLayoutGroup Vertical(Component target, float spacing = 4, float padding = 0, bool expandHeight = false)
        {
            var layout = target.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = new RectOffset((int)padding, (int)padding, (int)padding, (int)padding);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = expandHeight;
            layout.childAlignment = TextAnchor.UpperLeft;
            return layout;
        }

        public HorizontalLayoutGroup Horizontal(Component target, float spacing = 6, float padding = 0, bool expandWidth = false)
        {
            var layout = target.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = new RectOffset((int)padding, (int)padding, (int)padding, (int)padding);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = expandWidth;
            // Не растягивать детей по высоте: иначе ряд сам становится «гибким» и забирает высоту у соседей по столбцу.
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.MiddleLeft;
            return layout;
        }

        /// <summary>
        /// Высота по содержимому — только для корня, который не лежит в раскладке (содержимое прокрутки). Детям раскладок высоту
        /// даёт сама раскладка.
        /// </summary>
        public static ContentSizeFitter FitHeight(Component target)
        {
            var fitter = target.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            return fitter;
        }

        // ---------- Текст ----------

        public TextMeshProUGUI Label(Transform parent, string text, float size = 0, Color? color = null, bool wrap = false,
            TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft, string name = "Text")
        {
            RectTransform rect = Node(parent, name);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = Font;
            label.fontSize = size > 0 ? size : Theme.FontSize;
            label.color = color ?? Theme.Text;
            label.richText = true;
            label.alignment = align;
            label.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            label.overflowMode = wrap ? TextOverflowModes.Overflow : TextOverflowModes.Ellipsis;
            label.raycastTarget = false;
            label.text = text ?? string.Empty;
            return label;
        }

        public TextMeshProUGUI Title(Transform parent, string text) => Label(parent, text, Theme.FontSizeTitle, Theme.Text, name: "Title");

        /// <summary>Подзаголовок блока — акцентом.</summary>
        public TextMeshProUGUI Heading(Transform parent, string text)
        {
            TextMeshProUGUI label = Label(parent, text, Theme.FontSizeLarge, Theme.Accent, name: "Heading");
            Size(label, height: Theme.FontSizeLarge + 10);
            return label;
        }

        /// <summary>Текст с кликабельными ссылками <c>&lt;link&gt;</c>: клик — <paramref name="onLink"/>.</summary>
        public TextMeshProUGUI LinkLabel(Transform parent, Action<GuildMaster.Core.TextLink> onLink, float size = 0, bool wrap = true)
        {
            TextMeshProUGUI label = Label(parent, string.Empty, size, wrap: wrap);
            label.raycastTarget = true;
            label.gameObject.AddComponent<LinkClickHandler>().Clicked = onLink;
            return label;
        }

        // ---------- Кнопки ----------

        public Button Button(Transform parent, string text, UnityAction onClick, float width = -1, float height = -1,
            float fontSize = 0, string name = "Button")
        {
            Image background = Panel(parent, name, Color.white, blocksClicks: true);
            var button = background.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            SetButtonColors(button, false);
            if (onClick != null) button.onClick.AddListener(onClick);

            TextMeshProUGUI label = Label(background.transform, text, fontSize, Theme.Text, align: TextAlignmentOptions.Center);
            float inset = width >= 0 && width < 60 ? 1 : 8;
            Stretch(label.rectTransform, inset, 0, inset, 0);
            if (width >= 0 || height >= 0) Size(background, width, height >= 0 ? height : Theme.RowHeight);
            else Size(background, height: Theme.RowHeight);
            return button;
        }

        /// <summary>Подсветка выбранной кнопки (вкладка, скорость, фильтр) акцентом.</summary>
        public void SetButtonColors(Button button, bool selected)
        {
            Color normal = selected ? Theme.Accent : Theme.PanelAlt;
            ColorBlock colors = button.colors;
            colors.normalColor = normal;
            colors.highlightedColor = Color.Lerp(normal, Color.white, 0.12f);
            colors.pressedColor = Color.Lerp(normal, Theme.Accent, 0.5f);
            colors.selectedColor = normal;
            colors.disabledColor = Theme.Panel;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0f;
            button.colors = colors;

            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) label.color = selected ? Theme.Background : Theme.Text;
        }

        /// <summary>Строка-кнопка без фона: подсветка при наведении, выбранная — своим цветом.</summary>
        public Button RowButton(Transform parent, string name, UnityAction onClick)
        {
            Image background = Panel(parent, name, Color.white, blocksClicks: true);
            var button = background.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            SetRowColors(button, false);
            if (onClick != null) button.onClick.AddListener(onClick);
            return button;
        }

        public void SetRowColors(Button button, bool selected)
        {
            Color normal = selected ? Theme.RowSelected : Theme.Panel;
            ColorBlock colors = button.colors;
            colors.normalColor = normal;
            colors.highlightedColor = Color.Lerp(Theme.Panel, Theme.RowSelected, 0.6f);
            colors.pressedColor = Theme.RowSelected;
            colors.selectedColor = normal;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0f;
            button.colors = colors;
        }

        // ---------- Прокрутка ----------

        /// <summary>Прокручиваемый список: возвращает содержимое (вертикальная раскладка, высота по содержимому).</summary>
        public RectTransform Scroll(Transform parent, string name, out ScrollRect scroll, float spacing = 2)
        {
            Image frame = Panel(parent, name, Theme.Panel, blocksClicks: true);
            scroll = frame.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;
            scroll.inertia = false;

            RectTransform viewport = Stretch(Node(frame.transform, "Viewport"), 0, 0, 10, 0);
            viewport.gameObject.AddComponent<RectMask2D>();
            scroll.viewport = viewport;

            RectTransform content = Node(viewport, "Content");
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.offsetMin = content.offsetMax = Vector2.zero;
            Vertical(content, spacing, 4);
            FitHeight(content);
            scroll.content = content;

            scroll.verticalScrollbar = Scrollbar(frame.transform);
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            return content;
        }

        private Scrollbar Scrollbar(Transform parent)
        {
            Image track = Panel(parent, "Scrollbar", Theme.Background, blocksClicks: true);
            RectTransform rect = track.rectTransform;
            rect.anchorMin = new Vector2(1, 0);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(1, 0.5f);
            rect.sizeDelta = new Vector2(8, 0);
            rect.anchoredPosition = Vector2.zero;

            RectTransform area = Stretch(Node(track.transform, "Sliding Area"));
            Image handle = Panel(area, "Handle", Theme.BarBack, blocksClicks: true);
            Stretch(handle.rectTransform);

            var bar = track.gameObject.AddComponent<Scrollbar>();
            bar.handleRect = handle.rectTransform;
            bar.targetGraphic = handle;
            bar.direction = UnityEngine.UI.Scrollbar.Direction.BottomToTop;
            bar.navigation = new Navigation { mode = Navigation.Mode.None };
            return bar;
        }

        // ---------- Ползунок ----------

        /// <summary>Ползунок целых значений: дорожка, заполнение акцентом, ручка. Отпустили — <see cref="SliderReleaseHandler.Released"/>.</summary>
        public Slider Slider(Transform parent, float width, float min, float max, Action<float> onRelease)
        {
            RectTransform root = Node(parent, "Slider");
            Size(root, width, Theme.RowHeight);

            Image track = Panel(root, "Track", Theme.BarBack);
            RectTransform trackRect = track.rectTransform;
            trackRect.anchorMin = new Vector2(0, 0.5f);
            trackRect.anchorMax = new Vector2(1, 0.5f);
            trackRect.sizeDelta = new Vector2(-16, 8);

            RectTransform fillArea = Node(root, "Fill Area");
            fillArea.anchorMin = new Vector2(0, 0.5f);
            fillArea.anchorMax = new Vector2(1, 0.5f);
            fillArea.sizeDelta = new Vector2(-16, 8);
            Image fill = Panel(fillArea, "Fill", Theme.Accent);
            fill.rectTransform.sizeDelta = Vector2.zero;

            RectTransform handleArea = Stretch(Node(root, "Handle Area"), 8, 0, 8, 0);
            Image handle = Panel(handleArea, "Handle", Theme.Text, blocksClicks: true);
            handle.rectTransform.sizeDelta = new Vector2(14, 0);

            var slider = root.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = true;
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            ColorBlock colors = slider.colors;
            colors.fadeDuration = 0f;
            slider.colors = colors;

            var release = root.gameObject.AddComponent<SliderReleaseHandler>();
            release.Released = onRelease;
            return slider;
        }

        // ---------- Поля ----------

        public TMP_InputField Input(Transform parent, string placeholder, float width = -1, TMP_InputField.ContentType type = TMP_InputField.ContentType.Standard)
        {
            Image background = Panel(parent, "Input", Theme.Background, blocksClicks: true);
            Size(background, width, Theme.RowHeight, width < 0 ? 1 : -1);

            RectTransform area = Stretch(Node(background.transform, "Text Area"), 8, 2, 8, 2);
            area.gameObject.AddComponent<RectMask2D>();

            TextMeshProUGUI hint = Label(area, placeholder, Theme.FontSizeSmall, Theme.TextDim, name: "Placeholder");
            Stretch(hint.rectTransform);
            TextMeshProUGUI text = Label(area, string.Empty, name: "Value");
            Stretch(text.rectTransform);

            var input = background.gameObject.AddComponent<TMP_InputField>();
            input.textViewport = area;
            input.textComponent = text;
            input.placeholder = hint;
            input.fontAsset = Font;
            input.pointSize = Theme.FontSize;
            input.contentType = type;
            input.caretColor = Theme.Accent;
            input.selectionColor = new Color(Theme.Accent.r, Theme.Accent.g, Theme.Accent.b, 0.35f);
            input.navigation = new Navigation { mode = Navigation.Mode.None };
            return input;
        }
    }
}
