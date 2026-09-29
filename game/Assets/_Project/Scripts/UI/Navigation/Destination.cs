using GuildMaster.Core;

namespace GuildMaster.UI
{
    /// <summary>Экраны левой навигации. Новый экран — значение здесь и регистрация в <see cref="UiNavigator"/>.</summary>
    public enum ScreenId
    {
        Guild,
        Board,
        Quests,
        Treasury,
    }

    /// <summary>Вкладки экрана «Гильдия».</summary>
    public enum GuildTab
    {
        People,
        Parties,
        Buildings,
        Staff,
    }

    /// <summary>Куда ведёт переход: карточка человека поверх экрана или экран (вкладка) с выбранным объектом.</summary>
    public readonly struct Destination
    {
        private Destination(bool isCard, ScreenId screen, GuildTab tab, int selectId)
        {
            IsValid = true;
            IsCard = isCard;
            Screen = screen;
            Tab = tab;
            SelectId = selectId;
        }

        public static Destination None => default;

        public static Destination Card(int adventurerId) => new Destination(true, ScreenId.Guild, GuildTab.People, adventurerId);

        public static Destination ToScreen(ScreenId screen, int selectId = 0) => new Destination(false, screen, GuildTab.People, selectId);

        public static Destination ToGuild(GuildTab tab, int selectId = 0) => new Destination(false, ScreenId.Guild, tab, selectId);

        /// <summary>Переход есть (объект найден).</summary>
        public bool IsValid { get; }

        /// <summary>Открыть карточку авантюриста <see cref="SelectId"/>.</summary>
        public bool IsCard { get; }

        public ScreenId Screen { get; }
        public GuildTab Tab { get; }

        /// <summary>Что выбрать на экране или вкладке: id заказа, задания, постройки, сотрудника, группы; 0 — ничего.</summary>
        public int SelectId { get; }

        public override string ToString() =>
            !IsValid ? "none" : IsCard ? $"card {SelectId}" : Screen == ScreenId.Guild ? $"Guild/{Tab} {SelectId}" : $"{Screen} {SelectId}";
    }

    /// <summary>
    /// Куда ведёт ссылка из текста: человек → карточка (и ушедший, и кандидат); задание → «Задания»; заказ на доске → «Доска»,
    /// взятый — его задание (пока оно хранится); постройка → «Постройки»; сотрудник → «Персонал»; постоянная группа → «Группы»,
    /// группа под задание — её задание. Объекта больше нет — перехода нет.
    /// </summary>
    public static class LinkRouter
    {
        public static Destination Resolve(TextLink link, ISimulationClient client)
        {
            WorldState world = client.World;
            switch (link.Kind)
            {
                case TextLinkKind.Adventurer:
                    return EventTextSource.FindPerson(world, link.Id) != null ? Destination.Card(link.Id) : Destination.None;

                case TextLinkKind.Staff:
                    return world.Staff.TryGetKnown(link.Id, out _) || world.Staff.TryGetCandidate(link.Id, out _)
                        ? Destination.ToGuild(GuildTab.Staff, link.Id)
                        : Destination.None;

                case TextLinkKind.Quest:
                    return world.Quests.TryGetRun(link.Id, out _) ? Destination.ToScreen(ScreenId.Quests, link.Id) : Destination.None;

                case TextLinkKind.Order:
                    if (!world.Orders.TryGetOrder(link.Id, out Order order)) return Destination.None;
                    if (order.IsOpen) return Destination.ToScreen(ScreenId.Board, order.Id);
                    if (order.QuestRunId != 0 && world.Quests.TryGetRun(order.QuestRunId, out _))
                        return Destination.ToScreen(ScreenId.Quests, order.QuestRunId);
                    return order.IsInWork ? Destination.ToScreen(ScreenId.Board, order.Id) : Destination.None;

                case TextLinkKind.Building:
                    return world.Buildings.TryGetById(link.Id, out _) ? Destination.ToGuild(GuildTab.Buildings, link.Id) : Destination.None;

                case TextLinkKind.Party:
                    if (world.Parties.TryGetParty(link.Id, out Party party) && party.IsPermanent)
                        return Destination.ToGuild(GuildTab.Parties, link.Id);
                    foreach (QuestRun run in world.Quests.Active)
                    {
                        if (run.PartyId == link.Id) return Destination.ToScreen(ScreenId.Quests, run.Id);
                    }
                    return Destination.None;

                default:
                    return Destination.None;
            }
        }
    }
}
