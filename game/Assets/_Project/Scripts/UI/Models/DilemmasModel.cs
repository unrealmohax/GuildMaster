using System.Collections.Generic;
using GuildMaster.Core;
using GuildMaster.Data;

namespace GuildMaster.UI
{
    /// <summary>Строка списка обращений: заголовок, от кого, сколько осталось до ответа (или чем кончилось).</summary>
    public sealed class DilemmaRow
    {
        public int Id;
        public string Title;
        public string From;
        public string Remaining;
        public bool IsOpen;
    }

    /// <summary>Вариант ответа на карточке: текст, видимые последствия, можно ли выбрать (и почему нет).</summary>
    public sealed class DilemmaChoice
    {
        public int Index;
        public string Text;
        public string Consequences;
        public bool Available;
        public bool Unaffordable;
    }

    /// <summary>
    /// Карточка обращения: от кого (имя и архетип; у обращения персонала — должность), заголовок, текст с подстановками и
    /// ссылками, варианты (только те, что для игрока) с видимыми последствиями, «ответить до …». Отложенные последствия
    /// не показываются. Закрытое обращение — какой вариант сработал.
    /// </summary>
    public sealed class DilemmaCard
    {
        public int Id;
        public string Title;
        public string From;
        public string Body;
        public string Deadline;
        public bool IsOpen;
        public bool Sent;
        public string Outcome;
        public List<DilemmaChoice> Choices = new List<DilemmaChoice>();
    }

    /// <summary>
    /// Экран «Обращения»: открытые — по порядку появления, затем последние закрытые. Ответ — <see cref="Answer"/> (команда
    /// <see cref="AnswerDilemmaCommand"/>); пока команда не применена (время идёт), обращение помечено «ответ отправлен».
    /// </summary>
    public sealed class DilemmasModel
    {
        /// <summary>Сколько закрытых обращений показывать под открытыми.</summary>
        public const int ClosedShown = 15;

        private readonly HashSet<int> sent = new HashSet<int>();

        public List<DilemmaRow> Open { get; } = new List<DilemmaRow>();
        public List<DilemmaRow> Closed { get; } = new List<DilemmaRow>();

        public void Refresh(ISimulationClient client)
        {
            Open.Clear();
            Closed.Clear();
            DilemmaBook book = client.World.Dilemmas;
            foreach (Dilemma dilemma in book.Open) Open.Add(Row(client, dilemma));
            for (int i = book.Closed.Count - 1; i >= 0 && Closed.Count < ClosedShown; i--) Closed.Add(Row(client, book.Closed[i]));
            sent.RemoveWhere(id => !book.TryGetOpen(id, out _));
        }

        /// <summary>Игрок ответил вариантом: команда уходит в симуляцию (на паузе применяется в тот же кадр).</summary>
        public bool Answer(ISimulationClient client, int dilemmaId, int optionIndex)
        {
            if (sent.Contains(dilemmaId) || !client.World.Dilemmas.TryGetOpen(dilemmaId, out Dilemma dilemma)) return false;
            if (!DilemmaRules.IsAvailable(client.World, client.Data, dilemma, optionIndex)) return false;
            client.Send(new AnswerDilemmaCommand(dilemmaId, optionIndex));
            if (client.World.Dilemmas.TryGetOpen(dilemmaId, out _)) sent.Add(dilemmaId);
            return true;
        }

        public bool IsSent(int dilemmaId) => sent.Contains(dilemmaId);

        public static DilemmaRow Row(ISimulationClient client, Dilemma dilemma)
        {
            return new DilemmaRow
            {
                Id = dilemma.Id,
                Title = Title(client.Data, dilemma),
                From = From(client, dilemma),
                IsOpen = dilemma.IsOpen,
                Remaining = dilemma.IsOpen
                    ? string.Format(UiStrings.DilemmaRemainingFormat, UiFormat.Duration(dilemma.DeadlineAtHours - client.World.Time.TotalHours, client.Calendar))
                    : Outcome(client, dilemma),
            };
        }

        /// <summary>Карточка обращения; нет такого — <c>null</c>.</summary>
        public DilemmaCard Card(ISimulationClient client, int dilemmaId, string linkHex)
        {
            if (!client.World.Dilemmas.TryGetDilemma(dilemmaId, out Dilemma dilemma)) return null;
            DataRegistry data = client.Data;
            WorldState world = client.World;
            var card = new DilemmaCard
            {
                Id = dilemma.Id,
                Title = Title(data, dilemma),
                From = From(client, dilemma),
                IsOpen = dilemma.IsOpen,
                Sent = sent.Contains(dilemma.Id),
                Outcome = dilemma.IsOpen ? string.Empty : Outcome(client, dilemma),
                Deadline = string.Format(UiStrings.DilemmaDeadlineFormat, UiFormat.Stamp(client.Calendar.At(dilemma.DeadlineAtHours)),
                    UiFormat.Duration(System.Math.Max(0, dilemma.DeadlineAtHours - world.Time.TotalHours), client.Calendar)),
            };
            if (!data.TryGet(dilemma.DefinitionId, out DilemmaDefinition definition)) return card;

            var spans = new List<TextSpan>();
            string body = DilemmaTextSource.Render(definition.BodyTemplate, dilemma, world, data, spans: spans);
            card.Body = FeedFormatter.WithLinks(body, spans, linkHex);
            for (int i = 0; i < definition.Options.Count; i++)
            {
                DilemmaOption option = definition.Options[i];
                if (!option.PlayerSelectable) continue;
                bool unaffordable = dilemma.IsOpen && DilemmaRules.IsUnaffordable(world, data, dilemma, i);
                card.Choices.Add(new DilemmaChoice
                {
                    Index = i,
                    Text = LinkCodec.Escape(DilemmaTextSource.Render(option.Text, dilemma, world, data, i)),
                    Consequences = LinkCodec.Escape(DilemmaTextSource.Render(option.VisibleConsequencesText, dilemma, world, data, i)),
                    Available = dilemma.IsOpen && !card.Sent && !unaffordable,
                    Unaffordable = unaffordable,
                });
            }
            return card;
        }

        public static string Title(DataRegistry data, Dilemma dilemma) =>
            data.TryGet(dilemma.DefinitionId, out DilemmaDefinition definition) ? definition.Title : dilemma.DefinitionId;

        /// <summary>От кого: имя и архетип (и второй участник); у обращения персонала — должность и имя.</summary>
        public static string From(ISimulationClient client, Dilemma dilemma)
        {
            WorldState world = client.World;
            if (dilemma.SubjectId == 0)
            {
                foreach (StaffMember member in world.Staff.Members)
                {
                    if (member.Id == dilemma.StaffId) return $"{StaffModel.RoleName(client.Data, member.RoleId)} {member.Name}";
                }
                return string.Empty;
            }
            string from = Person(client, dilemma.SubjectId);
            return dilemma.PartnerId != 0 ? from + ", " + Person(client, dilemma.PartnerId) : from;
        }

        private static string Person(ISimulationClient client, int id)
        {
            Adventurer person = EventTextSource.FindPerson(client.World, id);
            return person == null ? string.Empty : $"{person.Name} ({PersonText.Archetype(person, client.Data)})";
        }

        /// <summary>Чем кончилось: выбранный вариант, «без ответа» или «снято».</summary>
        public static string Outcome(ISimulationClient client, Dilemma dilemma)
        {
            DataRegistry data = client.Data;
            switch (dilemma.Status)
            {
                case DilemmaStatus.Withdrawn: return UiStrings.DilemmaWithdrawn;
                case DilemmaStatus.Open: return string.Empty;
            }
            DilemmaOption option = DilemmaRules.OptionOf(data, dilemma, dilemma.OptionIndex);
            string text = option != null && option.PlayerSelectable
                ? DilemmaTextSource.Render(option.Text, dilemma, client.World, data, dilemma.OptionIndex)
                : string.Empty;
            if (dilemma.Status == DilemmaStatus.TimedOut)
                return string.IsNullOrEmpty(text) ? UiStrings.DilemmaNoAnswer : string.Format(UiStrings.DilemmaNoAnswerFormat, text);
            return string.Format(UiStrings.DilemmaAnsweredFormat, text);
        }
    }
}
