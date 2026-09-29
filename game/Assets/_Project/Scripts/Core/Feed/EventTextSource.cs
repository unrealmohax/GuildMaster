using System;
using System.Collections.Generic;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Значения меток строки из события. Откуда берётся каждая метка — таблица <see cref="Sources"/>:
    /// <list type="bullet">
    /// <item><c>{имя}</c> — первый участник, <c>{напарник}</c> — второй; <c>{лекарь}</c>, <c>{щит}</c>, <c>{решающий}</c> —
    /// id человека в данных события под ключами <c>medic</c>, <c>shield</c>, <c>leader</c>. Человек ищется среди
    /// активных, в архиве и среди кандидатов; падежи имени — из списка имён. У событий персонала участников нет — <c>{имя}</c>
    /// берётся из данных события под ключом <c>staff</c>.</item>
    /// <item>Названия — в данных события под ключами <c>party</c>, <c>place</c>, <c>enemy</c>, <c>client</c>, <c>cargo</c>,
    /// <c>decree</c>, <c>building</c>, <c>title</c> (название постоянной группы): <see cref="NounForms"/>, <see cref="TextValue"/>
    /// или строка.</item>
    /// <item>Числа — под ключами <c>count</c>, <c>total</c>, <c>amount</c>, <c>income</c>, <c>expense</c>.</item>
    /// <item><c>{причина}</c> — текст причин ухода, строка в данных события под ключом <c>reason</c>.</item>
    /// <item><c>{архетип}</c> — название архетипа в роде первого участника: id из данных события (<c>to</c>, <c>archetype</c>)
    /// или текущий архетип человека.</item>
    /// </list>
    /// Ссылки (<see cref="TextValue.Link"/>): люди — на человека, <c>{имя}</c> сотрудника — на сотрудника (<c>staffId</c>),
    /// место, враг, заказчик, груз — на задание (<c>quest</c>), а без него — на заказ (<c>order</c>); постройка — на постройку
    /// с этим названием; группа и её название — на группу (<c>partyId</c>), а без неё — на задание.
    /// Новая метка с источником в событии — строка в <see cref="Sources"/>.
    /// </summary>
    public sealed class EventTextSource : ITextSource
    {
        private static readonly Dictionary<string, Func<EventTextSource, TextValue>> Sources =
            new Dictionary<string, Func<EventTextSource, TextValue>>(StringComparer.Ordinal)
            {
                ["имя"] = s => s.Participant(0) ?? s.Staff(),
                ["напарник"] = s => s.Participant(1),
                ["лекарь"] = s => s.PersonFromPayload("medic"),
                ["щит"] = s => s.PersonFromPayload("shield"),
                ["решающий"] = s => s.PersonFromPayload("leader"),
                ["группа"] = s => s.PartyNoun("party"),
                ["место"] = s => s.QuestNoun("place"),
                ["враг"] = s => s.QuestNoun("enemy"),
                ["заказчик"] = s => s.QuestNoun("client"),
                ["груз"] = s => s.QuestNoun("cargo"),
                ["распоряжение"] = s => s.NounFromPayload("decree"),
                ["постройка"] = s => s.BuildingNoun(),
                ["число"] = s => s.NumberFromPayload("count"),
                ["всего"] = s => s.NumberFromPayload("total"),
                ["сумма"] = s => s.NumberFromPayload("amount"),
                ["доход"] = s => s.NumberFromPayload("income"),
                ["расход"] = s => s.NumberFromPayload("expense"),
                ["архетип"] = s => s.Archetype(),
                ["причина"] = s => s.NounFromPayload("reason"),
                ["название"] = s => s.PartyNoun("title"),
            };

        private readonly SimEvent simEvent;
        private readonly WorldState world;
        private readonly DataRegistry data;

        public EventTextSource(SimEvent simEvent, WorldState world, DataRegistry data)
        {
            this.simEvent = simEvent ?? throw new ArgumentNullException(nameof(simEvent));
            this.world = world ?? throw new ArgumentNullException(nameof(world));
            this.data = data ?? throw new ArgumentNullException(nameof(data));
        }

        public bool TryGet(string label, out TextValue value)
        {
            value = Sources.TryGetValue(label, out Func<EventTextSource, TextValue> source) ? source(this) : null;
            return value != null;
        }

        /// <summary>Человек по id: активный, из архива или кандидат; <c>null</c> — нет такого.</summary>
        public static Adventurer FindPerson(WorldState world, int id)
        {
            if (world.Adventurers.TryGetKnown(id, out Adventurer adventurer)) return adventurer;
            return world.Adventurers.TryGetCandidate(id, out Candidate candidate) ? candidate.Adventurer : null;
        }

        /// <summary>Имя человека с падежами из списка имён.</summary>
        public static TextValue PersonValue(Adventurer adventurer, DataRegistry data) =>
            TextValue.Person(adventurer.Name, data.NameForms(adventurer.Name), adventurer.Gender);

        private TextValue Participant(int index) =>
            index < simEvent.Participants.Count ? Person(simEvent.Participants[index]) : null;

        private TextValue PersonFromPayload(string key) =>
            simEvent.TryGet(key, out int id) ? Person(id) : null;

        private TextValue Person(int id)
        {
            Adventurer adventurer = FindPerson(world, id);
            return adventurer != null ? PersonValue(adventurer, data).WithLink(new TextLink(TextLinkKind.Adventurer, id)) : null;
        }

        private TextValue Staff()
        {
            TextValue value = NounFromPayload("staff");
            return value != null && simEvent.TryGet("staffId", out int id) ? value.WithLink(new TextLink(TextLinkKind.Staff, id)) : value;
        }

        private TextValue QuestNoun(string key)
        {
            TextValue value = NounFromPayload(key);
            if (value == null) return null;
            if (simEvent.TryGet("quest", out int questId) && questId != 0) return value.WithLink(new TextLink(TextLinkKind.Quest, questId));
            if (simEvent.TryGet("order", out int orderId) && orderId != 0) return value.WithLink(new TextLink(TextLinkKind.Order, orderId));
            return value;
        }

        private TextValue PartyNoun(string key)
        {
            TextValue value = NounFromPayload(key);
            if (value == null) return null;
            if (simEvent.TryGet("partyId", out int id) && id != 0) return value.WithLink(new TextLink(TextLinkKind.Party, id));
            return simEvent.TryGet("quest", out int questId) && questId != 0 ? value.WithLink(new TextLink(TextLinkKind.Quest, questId)) : value;
        }

        /// <summary>Постройка — по названию из данных события: ссылка на постройку мира с тем же определением.</summary>
        private TextValue BuildingNoun()
        {
            TextValue value = NounFromPayload("building");
            if (value == null || !simEvent.TryGet("building", out NounForms forms)) return value;

            foreach (Building building in world.Buildings.All)
            {
                if (data.TryGet(building.DefinitionId, out BuildingDefinition definition) && definition.NameForms == forms)
                    return value.WithLink(new TextLink(TextLinkKind.Building, building.Id));
            }
            return value;
        }

        private TextValue NounFromPayload(string key)
        {
            if (simEvent.TryGet(key, out TextValue value)) return value;
            if (simEvent.TryGet(key, out NounForms forms)) return TextValue.Noun(forms);
            return simEvent.TryGet(key, out string text) ? TextValue.Word(text) : null;
        }

        private TextValue NumberFromPayload(string key)
        {
            if (simEvent.TryGet(key, out int number)) return TextValue.Number(number);
            if (simEvent.TryGet(key, out long longNumber)) return TextValue.Number(longNumber);
            return simEvent.TryGet(key, out float fraction) ? TextValue.Number((long)Math.Round(fraction)) : null;
        }

        private TextValue Archetype()
        {
            Adventurer subject = simEvent.Participants.Count > 0 ? FindPerson(world, simEvent.Participants[0]) : null;
            string id = simEvent.TryGet("to", out string to) ? to
                : simEvent.TryGet("archetype", out string archetype) ? archetype
                : subject?.ArchetypeId;
            if (!data.TryGet(id, out ArchetypeDefinition definition)) return null;

            bool female = subject != null && subject.Gender == Gender.Female;
            return TextValue.Word(female ? definition.DisplayNameFemale : definition.DisplayName);
        }
    }
}
