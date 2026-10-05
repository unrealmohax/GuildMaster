using System;
using System.Collections.Generic;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Метки текстов обращения (тело, варианты, видимые последствия): <c>{имя}</c> — кто обратился (у обращения персонала — сотрудник),
    /// <c>{напарник}</c> — второй участник, <c>{место}</c> — место задания, <c>{причина}</c> — причина просьбы, <c>{сумма}</c> — у варианта
    /// деньги этого варианта (<see cref="DilemmaRules.OptionCost"/>), у тела — запрошенная сумма. Люди — со ссылками.
    /// </summary>
    public sealed class DilemmaTextSource : ITextSource
    {
        private readonly Dilemma dilemma;
        private readonly WorldState world;
        private readonly DataRegistry data;
        private readonly int optionIndex;

        public DilemmaTextSource(Dilemma dilemma, WorldState world, DataRegistry data, int optionIndex = -1)
        {
            this.dilemma = dilemma ?? throw new ArgumentNullException(nameof(dilemma));
            this.world = world ?? throw new ArgumentNullException(nameof(world));
            this.data = data ?? throw new ArgumentNullException(nameof(data));
            this.optionIndex = optionIndex;
        }

        public bool TryGet(string label, out TextValue value)
        {
            switch (label)
            {
                case "имя": value = dilemma.SubjectId != 0 ? Person(dilemma.SubjectId) : Staff(); break;
                case "напарник": value = dilemma.PartnerId != 0 ? Person(dilemma.PartnerId) : null; break;
                case "место":
                    value = dilemma.Place != null ? TextValue.Noun(dilemma.Place) : null;
                    if (value != null && dilemma.QuestRunId != 0) value = value.WithLink(new TextLink(TextLinkKind.Quest, dilemma.QuestRunId));
                    break;
                case "причина": value = string.IsNullOrEmpty(dilemma.Reason) ? null : TextValue.Word(dilemma.Reason); break;
                case "сумма":
                    value = TextValue.Number(optionIndex >= 0 ? DilemmaRules.OptionCost(world, data, dilemma, optionIndex) : dilemma.Amount);
                    break;
                default: value = null; break;
            }
            return value != null;
        }

        /// <summary>Текст по шаблону обращения; ошибки разметки — в <paramref name="errors"/>.</summary>
        public static string Render(string template, Dilemma dilemma, WorldState world, DataRegistry data, int optionIndex = -1,
            List<string> errors = null, List<TextSpan> spans = null) =>
            string.IsNullOrEmpty(template) ? string.Empty
                : TextRenderer.Render(template, new DilemmaTextSource(dilemma, world, data, optionIndex), errors, spans);

        private TextValue Person(int id)
        {
            Adventurer adventurer = EventTextSource.FindPerson(world, id);
            return adventurer != null
                ? EventTextSource.PersonValue(adventurer, data).WithLink(new TextLink(TextLinkKind.Adventurer, id))
                : null;
        }

        private TextValue Staff()
        {
            foreach (StaffMember member in world.Staff.Members)
            {
                if (member.Id == dilemma.StaffId)
                    return TextValue.Person(member.Name, data.NameForms(member.Name), member.Gender).WithLink(new TextLink(TextLinkKind.Staff, member.Id));
            }
            return null;
        }
    }
}
