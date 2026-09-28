using System.Collections.Generic;
using System.Globalization;
using System.Text;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Отчёт месяца строками: заголовок раздела, затем «подпись: значение». Подписи — шаблоны из набора шаблонов ленты
    /// (первый вариант); реестр только из чисел — вместо подписи ключ. Люди — по именам, раскрытия — «Имя — Трус»,
    /// нейтральная ось — «Имя — Риск: уравновешенность»; постройки — названием, сотрудники — «Имя — должность»
    /// (с долгом по зарплате — и суммой долга).
    /// </summary>
    public static class MonthReportText
    {
        public static List<string> Lines(MonthReport report, WorldState world, DataRegistry data, List<string> errors = null)
        {
            var lines = new List<string>();
            var text = new StringBuilder();
            foreach (ReportSection section in report.Sections)
            {
                lines.Add(Label(data, section.TitleKey, errors));
                foreach (ReportLine line in section.Lines)
                {
                    text.Clear();
                    text.Append(Label(data, line.TextKey, errors)).Append(": ");
                    if (line.HasAmount) text.Append(Amount(line.Amount, line.Signed));
                    if (line.HasChange) text.Append(Value(line.From)).Append(" → ").Append(Value(line.To));
                    for (int i = 0; i < line.Items.Count; i++)
                    {
                        if (i > 0) text.Append(", ");
                        AppendItem(text, line.Items[i], world, data, errors);
                    }
                    lines.Add(text.ToString());
                }
            }
            return lines;
        }

        /// <summary>Текст по ключу: первый вариант первого шаблона. Нет шаблона — ключ (и ошибка, если данные есть).</summary>
        public static string Label(DataRegistry data, string key, List<string> errors = null)
        {
            if (!data.HasDefinitions) return key;

            IReadOnlyList<FeedTemplate> templates = data.FeedTemplates(key);
            if (templates.Count == 0 || templates[0].Variants.Count == 0)
            {
                errors?.Add("no template " + key);
                return key;
            }
            return TextRenderer.Render(templates[0].Variants[0], EmptySource.Instance, errors);
        }

        public static string Amount(int amount, bool signed) =>
            amount.ToString(signed ? "+0;-0;0" : "0", CultureInfo.InvariantCulture);

        /// <summary>Дробное значение: до десятых, без лишних нулей («5», «7.5»).</summary>
        public static string Value(float value) => value.ToString("0.#", CultureInfo.InvariantCulture);

        private static void AppendItem(StringBuilder text, ReportItem item, WorldState world, DataRegistry data, List<string> errors)
        {
            if (item.Detail == MonthReportSections.BuildingDetail)
            {
                bool known = world.Buildings.TryGetById(item.PersonId, out Building building);
                text.Append(known && data.TryGet(building.DefinitionId, out BuildingDefinition definition) ? definition.DisplayName
                    : known ? building.DefinitionId : "#" + item.PersonId.ToString(CultureInfo.InvariantCulture));
                return;
            }
            if (item.Detail == MonthReportSections.StaffDetail)
            {
                if (!world.Staff.TryGetKnown(item.PersonId, out StaffMember member))
                {
                    text.Append('#').Append(item.PersonId.ToString(CultureInfo.InvariantCulture));
                    return;
                }
                text.Append(member.Name).Append(" — ")
                    .Append(data.TryGet(member.RoleId, out StaffRoleDefinition role) ? role.DisplayName : member.RoleId);
                if (member.UnpaidSalary > 0 && member.LeaveReason == StaffLeaveReason.None)
                    text.Append(' ').Append(member.UnpaidSalary.ToString(CultureInfo.InvariantCulture));
                return;
            }

            Adventurer adventurer = EventTextSource.FindPerson(world, item.PersonId);
            text.Append(adventurer != null ? adventurer.Name : "#" + item.PersonId.ToString(CultureInfo.InvariantCulture));
            if (item.Detail.Length == 0) return;

            text.Append(" — ").Append(DetailName(item.Detail, adventurer, data, errors));
        }

        /// <summary>Название раскрытого: черта, полюс оси в роде человека или «ось: уравновешенность».</summary>
        private static string DetailName(string detail, Adventurer adventurer, DataRegistry data, List<string> errors)
        {
            string[] parts = detail.Split(':');
            bool female = adventurer != null && adventurer.Gender == Gender.Female;
            if (parts.Length == 2 && parts[0] == "trait")
                return data.TryGet(parts[1], out SpecialTraitDefinition trait) ? trait.DisplayName : parts[1];

            if (parts.Length == 3 && parts[0] == "axis" && System.Enum.TryParse(parts[1], out AxisId axis) && data.HasDefinitions)
            {
                AxisDefinition definition = data.Axis(axis);
                if (System.Enum.TryParse(parts[2], out AxisPole pole))
                {
                    AxisPoleDefinition poleDefinition = definition.Pole(pole);
                    return female && !string.IsNullOrEmpty(poleDefinition.NameFemale) ? poleDefinition.NameFemale : poleDefinition.Name;
                }
                return definition.DisplayName + ": " + Label(data, MonthReportSections.AxisBalanced, errors);
            }
            return detail;
        }

        private sealed class EmptySource : ITextSource
        {
            public static readonly EmptySource Instance = new EmptySource();

            public bool TryGet(string label, out TextValue value)
            {
                value = null;
                return false;
            }
        }
    }
}
