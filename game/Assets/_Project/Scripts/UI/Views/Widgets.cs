using System;
using System.Collections.Generic;
using GuildMaster.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GuildMaster.UI
{
    /// <summary>Клик по ссылке <c>&lt;link&gt;</c> в тексте TMP: цель ссылки — <see cref="Clicked"/>.</summary>
    public sealed class LinkClickHandler : MonoBehaviour, IPointerClickHandler
    {
        public Action<TextLink> Clicked;

        private TMP_Text text;

        private void Awake() => text = GetComponent<TMP_Text>();

        public void OnPointerClick(PointerEventData eventData)
        {
            if (text == null || Clicked == null) return;
            Camera eventCamera = eventData.pressEventCamera;
            int index = TMP_TextUtilities.FindIntersectingLink(text, eventData.position, eventCamera);
            if (index < 0 || index >= text.textInfo.linkCount) return;
            if (LinkCodec.TryDecode(text.textInfo.linkInfo[index].GetLinkID(), out TextLink link)) Clicked(link);
        }
    }

    /// <summary>Полоска с числом: доля заливки и подпись поверх.</summary>
    public sealed class BarView
    {
        private readonly RectTransform fill;
        private readonly Image fillImage;
        private readonly TextMeshProUGUI label;

        public BarView(UiFactory factory, Transform parent, float width = -1, float height = -1)
        {
            UiTheme theme = factory.Theme;
            Image back = factory.Panel(parent, "Bar", theme.BarBack);
            Root = back.rectTransform;
            UiFactory.Size(back, width, height >= 0 ? height : theme.RowHeight - 12, width < 0 ? 1 : -1);

            fillImage = factory.Panel(back.transform, "Fill", theme.Accent);
            fill = fillImage.rectTransform;
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0, 1);
            fill.offsetMin = fill.offsetMax = Vector2.zero;

            label = factory.Label(back.transform, string.Empty, theme.FontSizeSmall, theme.Text, align: TextAlignmentOptions.Center);
            UiFactory.Stretch(label.rectTransform);
        }

        public RectTransform Root { get; }

        public void Set(float fraction, string text, Color? color = null)
        {
            fill.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1);
            if (color.HasValue) fillImage.color = color.Value;
            label.text = text ?? string.Empty;
        }
    }

    /// <summary>Столбец таблицы: заголовок, ширина (−1 — растягивается), полоска вместо текста, клик по заголовку.</summary>
    public sealed class TableColumn
    {
        public string Title;
        public float Width = -1;
        public bool Bar;
        public Action OnHeader;
    }

    /// <summary>
    /// Таблица: строка заголовков (клик — сортировка) и прокручиваемые строки. Строки переиспользуются: при обновлении
    /// меняются только тексты, лишние скрываются. Клик по строке — id, заданный строке.
    /// </summary>
    public sealed class TableView
    {
        private sealed class Row
        {
            public Button Button;
            public int Id;
            public TextMeshProUGUI[] Texts;
            public BarView[] Bars;
        }

        private readonly UiFactory factory;
        private readonly IReadOnlyList<TableColumn> columns;
        private readonly Action<int> onRowClick;
        private readonly RectTransform content;
        private readonly List<Row> rows = new List<Row>();
        private readonly TextMeshProUGUI[] headers;
        private int visible;
        private int selectedId;

        public TableView(UiFactory factory, Transform parent, IReadOnlyList<TableColumn> columns, Action<int> onRowClick)
        {
            this.factory = factory;
            this.columns = columns;
            this.onRowClick = onRowClick;
            UiTheme theme = factory.Theme;

            RectTransform root = UiFactory.Node(parent, "Table");
            Root = root;
            factory.Vertical(root, 2);
            UiFactory.Size(root, flexWidth: 1, flexHeight: 1);

            Image header = factory.Panel(root, "Header", theme.Background);
            factory.Horizontal(header, 6, 0).padding = new RectOffset(8, 18, 0, 0);
            UiFactory.Size(header, height: theme.RowHeight);
            headers = new TextMeshProUGUI[columns.Count];
            for (int i = 0; i < columns.Count; i++)
            {
                TableColumn column = columns[i];
                if (column.OnHeader != null)
                {
                    Button button = factory.RowButton(header.transform, "Col " + column.Title, () => column.OnHeader());
                    factory.SetRowColors(button, false);
                    ColorBlock colors = button.colors;
                    colors.normalColor = theme.Background;
                    button.colors = colors;
                    headers[i] = factory.Label(button.transform, column.Title, theme.FontSizeSmall, theme.TextDim);
                    UiFactory.Stretch(headers[i].rectTransform, 2, 0, 2, 0);
                    SizeCell(button, column);
                    UiFactory.Size(button, height: theme.RowHeight);
                }
                else
                {
                    headers[i] = factory.Label(header.transform, column.Title, theme.FontSizeSmall, theme.TextDim);
                    SizeCell(headers[i], column);
                }
            }

            content = factory.Scroll(root, "Rows", out ScrollRect scroll, 1);
            UiFactory.Size(scroll, flexWidth: 1, flexHeight: 1);
        }

        public RectTransform Root { get; }

        public void SetHeader(int column, string title) => headers[column].text = title;

        public void SetSelected(int id)
        {
            selectedId = id;
            for (int i = 0; i < visible; i++) factory.SetRowColors(rows[i].Button, rows[i].Id == selectedId);
        }

        public void SetRowCount(int count)
        {
            while (rows.Count < count) rows.Add(CreateRow());
            for (int i = 0; i < rows.Count; i++)
            {
                bool show = i < count;
                if (rows[i].Button.gameObject.activeSelf != show) rows[i].Button.gameObject.SetActive(show);
            }
            visible = count;
        }

        public void SetRowId(int row, int id)
        {
            rows[row].Id = id;
            factory.SetRowColors(rows[row].Button, id == selectedId && id != 0);
        }

        public void SetText(int row, int column, string text, Color? color = null)
        {
            TextMeshProUGUI label = rows[row].Texts[column];
            if (label == null) return;
            label.text = text ?? string.Empty;
            label.color = color ?? factory.Theme.Text;
        }

        public void SetBar(int row, int column, float fraction, string text, Color? color = null) =>
            rows[row].Bars[column]?.Set(fraction, text, color);

        private Row CreateRow()
        {
            var row = new Row { Texts = new TextMeshProUGUI[columns.Count], Bars = new BarView[columns.Count] };
            row.Button = factory.RowButton(content, "Row", () => onRowClick?.Invoke(row.Id));
            factory.Horizontal(row.Button, 6, 0).padding = new RectOffset(8, 8, 0, 0);
            UiFactory.Size(row.Button, height: factory.Theme.RowHeight);
            for (int i = 0; i < columns.Count; i++)
            {
                if (columns[i].Bar)
                {
                    RectTransform cell = UiFactory.Node(row.Button.transform, "Cell");
                    SizeCell(cell, columns[i]);
                    factory.Horizontal(cell, 0, 0).padding = new RectOffset(0, 6, 6, 6);
                    row.Bars[i] = new BarView(factory, cell);
                }
                else
                {
                    row.Texts[i] = factory.Label(row.Button.transform, string.Empty);
                    SizeCell(row.Texts[i], columns[i]);
                }
            }
            return row;
        }

        private static void SizeCell(Component cell, TableColumn column)
        {
            if (column.Width >= 0) UiFactory.Size(cell, column.Width, flexWidth: 0);
            else UiFactory.Size(cell, 60, flexWidth: 1);
        }
    }

    /// <summary>
    /// Лента: строки с кликабельными именами, новые — сверху. Дописывает только новые строки (по последней показанной),
    /// держит не больше заданного числа; если последней показанной строки в источнике нет — перестраивает всё.
    /// </summary>
    public sealed class FeedView
    {
        private readonly UiFactory factory;
        private readonly FeedFormatter formatter;
        private readonly Action<TextLink> onLink;
        private readonly RectTransform content;
        private readonly List<TextMeshProUGUI> lines = new List<TextMeshProUGUI>();
        private readonly int limit;
        private FeedEntry last;
        private IReadOnlyList<FeedEntry> lastSource;

        public FeedView(UiFactory factory, Transform parent, Action<TextLink> onLink)
        {
            this.factory = factory;
            this.onLink = onLink;
            UiTheme theme = factory.Theme;
            limit = theme.FeedLinesShown;
            formatter = new FeedFormatter(UiTheme.ToHex(theme.TextDim), UiTheme.ToHex(theme.Text), UiTheme.ToHex(theme.Accent));
            content = factory.Scroll(parent, "Feed", out ScrollRect scroll, 4);
            Scroll = scroll;
        }

        public ScrollRect Scroll { get; }

        /// <summary>Показать строки источника (от старых к новым), дописав только новые.</summary>
        public void Show(IReadOnlyList<FeedEntry> entries, Calendar calendar)
        {
            int start = 0;
            if (entries != lastSource || last == null)
            {
                Clear();
                lastSource = entries;
            }
            else
            {
                int index = IndexOf(entries, last);
                if (index < 0) Clear();
                else start = index + 1;
            }

            int from = Math.Max(start, entries.Count - limit);
            for (int i = from; i < entries.Count; i++) Add(entries[i], calendar);
            if (entries.Count > 0) last = entries[entries.Count - 1];
        }

        public void Clear()
        {
            foreach (TextMeshProUGUI line in lines) UiFactory.DestroyObject(line.gameObject);
            lines.Clear();
            last = null;
        }

        private void Add(FeedEntry entry, Calendar calendar)
        {
            TextMeshProUGUI line;
            if (lines.Count >= limit)
            {
                line = lines[lines.Count - 1];
                lines.RemoveAt(lines.Count - 1);
            }
            else
            {
                line = factory.LinkLabel(content, onLink, factory.Theme.FontSizeSmall);
            }
            line.text = formatter.Format(entry, calendar);
            line.transform.SetSiblingIndex(0);
            lines.Insert(0, line);
        }

        private static int IndexOf(IReadOnlyList<FeedEntry> entries, FeedEntry entry)
        {
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(entries[i], entry)) return i;
            }
            return -1;
        }
    }

    /// <summary>Выбор из списка стрелками «◄ значение ►»: отладочная панель, ранги, месяцы. Игрок сменил значение — <see cref="Changed"/>.</summary>
    public sealed class CycleSelector
    {
        private readonly TextMeshProUGUI label;
        private IReadOnlyList<string> values = Array.Empty<string>();

        public CycleSelector(UiFactory factory, Transform parent, float width)
        {
            RectTransform root = UiFactory.Node(parent, "Cycle");
            Root = root;
            factory.Horizontal(root, 2);
            UiFactory.Size(root, width, factory.Theme.RowHeight);
            factory.Button(root, "◄", () => Step(-1), 34);
            label = factory.Label(root, string.Empty, factory.Theme.FontSizeSmall, align: TextAlignmentOptions.Center);
            UiFactory.Size(label, flexWidth: 1);
            factory.Button(root, "►", () => Step(1), 34);
        }

        public RectTransform Root { get; }

        public int Index { get; private set; }

        public string Value => values.Count > 0 ? values[Index] : string.Empty;

        /// <summary>Игрок стрелкой выбрал другое значение: номер нового.</summary>
        public event Action<int> Changed;

        /// <summary>Показать значение с этим номером (из мира) — без <see cref="Changed"/>.</summary>
        public void SetIndex(int index)
        {
            if (values.Count == 0) return;
            Index = Math.Max(0, Math.Min(index, values.Count - 1));
            Show();
        }

        public void SetValues(IReadOnlyList<string> newValues, Func<string, string> caption = null)
        {
            values = newValues ?? Array.Empty<string>();
            Caption = caption;
            Index = Math.Min(Index, Math.Max(0, values.Count - 1));
            Show();
        }

        private Func<string, string> Caption { get; set; }

        private void Step(int delta)
        {
            if (values.Count == 0) return;
            Index = (Index + delta + values.Count) % values.Count;
            Show();
            Changed?.Invoke(Index);
        }

        private void Show() => label.text = values.Count == 0 ? "—" : Caption != null ? Caption(values[Index]) : values[Index];
    }

    /// <summary>Отпустили ползунок: значение — <see cref="Released"/> (команда уходит один раз, а не на каждый шаг).</summary>
    public sealed class SliderReleaseHandler : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public Action<float> Released;

        /// <summary>Ползунок держат мышью: значение из мира его не двигает.</summary>
        public bool IsHeld { get; private set; }

        public void OnPointerDown(PointerEventData eventData) => IsHeld = true;

        public void OnPointerUp(PointerEventData eventData)
        {
            IsHeld = false;
            if (TryGetComponent(out Slider slider)) Released?.Invoke(slider.value);
        }
    }

    /// <summary>
    /// Строка списка, которую перетаскивают мышью на другое место среди соседей: отпустили — <see cref="Dropped"/> (старый и
    /// новый номер среди строк). Пока тянут, строка полупрозрачна.
    /// </summary>
    public sealed class DragReorder : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public Action<int, int> Dropped;

        private CanvasGroup group;

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!TryGetComponent(out group)) group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0.5f;
        }

        public void OnDrag(PointerEventData eventData) { }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (group != null) group.alpha = 1f;
            Transform parent = transform.parent;
            if (parent == null) return;

            int from = -1;
            int to = 0;
            int index = 0;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (!child.gameObject.activeSelf || !child.TryGetComponent(out DragReorder _)) continue;
                if (child == transform)
                {
                    from = index++;
                    continue;
                }
                // Новое место — сколько других строк выше точки, где отпустили.
                var rect = (RectTransform)child;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position, eventData.pressEventCamera, out Vector2 local);
                if (local.y < rect.rect.center.y) to++;
                index++;
            }
            if (from >= 0 && to != from) Dropped?.Invoke(from, to);
        }
    }
}
