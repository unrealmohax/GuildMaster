using TMPro;
using UnityEngine;

namespace GuildMaster.UI
{
    /// <summary>
    /// Числа и цвета интерфейса: шрифт, палитра тёмной темы, размеры, частота перерисовки. Не баланс игры — на симуляцию
    /// не влияет. Нет ассета — значения по умолчанию (<see cref="CreateDefault"/>).
    /// </summary>
    [CreateAssetMenu(menuName = "GuildMaster/UI Theme", fileName = "UiTheme")]
    public sealed class UiTheme : ScriptableObject
    {
        [Header("Шрифт")]
        [Tooltip("TMP-шрифт с кириллицей")]
        [SerializeField] private TMP_FontAsset font;

        [Header("Палитра")]
        [SerializeField] private Color background = Hex(0x15171C);
        [SerializeField] private Color panel = Hex(0x1E2128);
        [SerializeField] private Color panelAlt = Hex(0x262A33);
        [SerializeField] private Color rowSelected = Hex(0x33363F);
        [SerializeField] private Color text = Hex(0xD8D4CC);
        [SerializeField] private Color textDim = Hex(0x8A8A8A);
        [Tooltip("Единственный акцентный цвет: выбранное, заметные строки, полоски")]
        [SerializeField] private Color accent = Hex(0xD4A24C);
        [Tooltip("Только сигнал: минус в казне, банкротство, тяжёлая рана, гибель")]
        [SerializeField] private Color danger = Hex(0xC8554B);
        [SerializeField] private Color barBack = Hex(0x2E323B);
        [SerializeField] private Color overlayShade = new Color(0f, 0f, 0f, 0.55f);

        [Header("Размеры (при опорном 1920×1080)")]
        [SerializeField, Min(8)] private float fontSize = 20f;
        [SerializeField, Min(8)] private float fontSizeSmall = 17f;
        [SerializeField, Min(8)] private float fontSizeLarge = 24f;
        [SerializeField, Min(8)] private float fontSizeTitle = 30f;
        [SerializeField, Min(16)] private float rowHeight = 34f;
        [SerializeField, Min(24)] private float topBarHeight = 60f;
        [SerializeField, Min(80)] private float navWidth = 200f;
        [SerializeField, Min(200)] private float feedWidth = 440f;
        [SerializeField, Min(0)] private float padding = 12f;

        [Header("Обновление")]
        [Tooltip("Сколько раз в секунду не чаще перерисовывать верхнюю панель")]
        [SerializeField, Min(1)] private float topBarRefreshPerSecond = 10f;
        [Tooltip("Сколько раз в секунду не чаще перерисовывать открытый экран и окна")]
        [SerializeField, Min(1)] private float screenRefreshPerSecond = 4f;
        [Tooltip("Сколько строк ленты держать на экране")]
        [SerializeField, Min(10)] private int feedLinesShown = 150;

        public TMP_FontAsset Font => font;
        public Color Background => background;
        public Color Panel => panel;
        public Color PanelAlt => panelAlt;
        public Color RowSelected => rowSelected;
        public Color Text => text;
        public Color TextDim => textDim;
        public Color Accent => accent;
        public Color Danger => danger;
        public Color BarBack => barBack;
        public Color OverlayShade => overlayShade;
        public float FontSize => fontSize;
        public float FontSizeSmall => fontSizeSmall;
        public float FontSizeLarge => fontSizeLarge;
        public float FontSizeTitle => fontSizeTitle;
        public float RowHeight => rowHeight;
        public float TopBarHeight => topBarHeight;
        public float NavWidth => navWidth;
        public float FeedWidth => feedWidth;
        public float Padding => padding;
        public float TopBarRefreshPerSecond => topBarRefreshPerSecond;
        public float ScreenRefreshPerSecond => screenRefreshPerSecond;
        public int FeedLinesShown => feedLinesShown;

        /// <summary>Цвет строкой для разметки TMP: «#RRGGBB».</summary>
        public static string ToHex(Color color) => "#" + ColorUtility.ToHtmlStringRGB(color);

        public static UiTheme CreateDefault()
        {
            var theme = CreateInstance<UiTheme>();
            theme.hideFlags = HideFlags.DontSave;
            return theme;
        }

        private static Color Hex(int rgb) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
    }
}
