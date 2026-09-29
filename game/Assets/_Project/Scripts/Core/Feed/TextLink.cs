namespace GuildMaster.Core
{
    /// <summary>На что указывает подставленное в текст значение: по нему интерфейс делает имя кликабельным.</summary>
    public enum TextLinkKind
    {
        None,
        Adventurer,
        Staff,
        Quest,
        Order,
        Building,
        Party,
    }

    /// <summary>Цель ссылки: вид объекта и его id (у людей, сотрудников, заказов, заданий, построек и групп — свои счётчики).</summary>
    public readonly struct TextLink
    {
        public TextLink(TextLinkKind kind, int id)
        {
            Kind = kind;
            Id = id;
        }

        public static TextLink None => default;

        public TextLinkKind Kind { get; }
        public int Id { get; }

        public bool IsNone => Kind == TextLinkKind.None || Id == 0;

        public override string ToString() => IsNone ? "none" : $"{Kind}:{Id}";
    }

    /// <summary>Место в готовом тексте, где стоит значение со ссылкой: начало, длина, цель.</summary>
    public readonly struct TextSpan
    {
        public TextSpan(int start, int length, TextLink link)
        {
            Start = start;
            Length = length;
            Link = link;
        }

        public int Start { get; }
        public int Length { get; }
        public TextLink Link { get; }
    }
}
