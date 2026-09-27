using System.Globalization;
using System.Text;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Строки лога о людях: бросок шанса, новый человек со скрытым (оси, черты), показатели за сутки.
    /// Вызывать под проверкой уровня — строки строятся только при включённом.
    /// </summary>
    public static class AdventurerLog
    {
        /// <summary>
        /// Бросок (<see cref="SimLogLevel.Debug"/>): «roll breakdown #3 Имя stress=85.2 chance=0.1 rolled=0.0532 => yes».
        /// <paramref name="factor"/> — показатель, от которого зависел бросок (стресс, лояльность); <c>null</c> — без него.
        /// </summary>
        public static void WriteRoll(SimLogger log, string roll, Adventurer subject, float chance, float rolled, bool success,
            string factor, float factorValue)
        {
            StringBuilder line = log.Begin(SimLogLevel.Debug);
            line.Append("roll ").Append(roll);
            if (subject != null) AppendName(line.Append(' '), subject);
            if (factor != null) line.Append(' ').Append(factor).Append('=').Append(Number(factorValue));
            line.Append(" chance=").Append(chance.ToString("0.####", CultureInfo.InvariantCulture))
                .Append(" rolled=").Append(rolled.ToString("0.0000", CultureInfo.InvariantCulture))
                .Append(" => ").Append(success ? "yes" : "no");
            log.Commit();
        }

        /// <summary>
        /// Новый человек (<see cref="SimLogLevel.Debug"/>): тип и уровень генерации, пол, возраст, архетип, ранг, параметры,
        /// оси (скрытые — тоже), черты (скрытые помечены), кошелёк и стартовое состояние.
        /// </summary>
        public static void WriteGenerated(SimContext ctx, string what, Adventurer adventurer, ArchetypeDefinition type, AdventurerLevel level)
        {
            SimLogger log = ctx.Log;
            if (!log.IsOn(SimLogLevel.Debug)) return;

            StringBuilder line = log.Begin(SimLogLevel.Debug);
            AppendName(line.Append(what).Append(' '), adventurer);
            line.Append(' ').Append(adventurer.Gender == Gender.Male ? 'M' : 'F')
                .Append(" age=").Append(adventurer.Age.ToString(CultureInfo.InvariantCulture))
                .Append(" type=").Append(type != null ? type.Id : "-")
                .Append(" level=").Append(level)
                .Append(" archetype=").Append(adventurer.ArchetypeId)
                .Append(" power=").Append(Number(adventurer.PowerScore))
                .Append(" rank=").Append(adventurer.GuildRank);

            line.Append(" | stats");
            for (int i = 0; i < Vocabulary.StatCount; i++)
            {
                line.Append(' ').Append((StatId)i).Append('=').Append(Number(adventurer.GetStat((StatId)i)));
            }

            line.Append(" | axes");
            for (int i = 0; i < Vocabulary.AxisCount; i++)
            {
                float value = adventurer.GetAxis((AxisId)i);
                line.Append(' ').Append((AxisId)i).Append('=').Append(value >= 0f ? "+" : string.Empty).Append(Number(value));
            }

            line.Append(" | traits");
            if (adventurer.Traits.Count == 0) line.Append(" -");
            foreach (TraitInstance trait in adventurer.Traits)
            {
                line.Append(' ').Append(trait.TraitId);
                if (trait.PartnerId != 0) line.Append("->#").Append(trait.PartnerId.ToString(CultureInfo.InvariantCulture));
                if (!trait.Revealed) line.Append("(hidden)");
            }

            AdventurerState state = adventurer.State;
            line.Append(" | wallet=").Append(state.Wallet.ToString(CultureInfo.InvariantCulture))
                .Append(" fatigue=").Append(Number(state.Fatigue))
                .Append(" stress=").Append(Number(state.Stress))
                .Append(" contentment=").Append(Number(state.Contentment))
                .Append(" loyalty=").Append(Number(state.Loyalty));
            log.Commit();
        }

        /// <summary>
        /// Показатели за сутки (<see cref="SimLogLevel.Trace"/>): занятие, усталость, стресс, довольство и его цель,
        /// лояльность, кошелёк, флаги.
        /// </summary>
        public static void WriteDaily(SimContext ctx, Adventurer adventurer, float contentmentTarget)
        {
            SimLogger log = ctx.Log;
            if (!log.IsOn(SimLogLevel.Trace)) return;

            AdventurerState state = adventurer.State;
            StringBuilder line = log.Begin(SimLogLevel.Trace);
            AppendName(line.Append("day "), adventurer);
            line.Append(" activity=").Append(state.Activity)
                .Append(" fatigue=").Append(Number(state.Fatigue))
                .Append(" stress=").Append(Number(state.Stress))
                .Append(" contentment=").Append(Number(state.Contentment))
                .Append(" target=").Append(Number(contentmentTarget))
                .Append(" loyalty=").Append(Number(state.Loyalty))
                .Append(" wallet=").Append(state.Wallet.ToString(CultureInfo.InvariantCulture));
            if (state.IsWalletEmpty) line.Append(" walletEmpty");
            if (state.DebtToGuild > 0) line.Append(" debt=").Append(state.DebtToGuild.ToString(CultureInfo.InvariantCulture));
            if (state.Breakdown != BreakdownKind.None) line.Append(" breakdown=").Append(state.Breakdown);
            foreach (Condition condition in state.Conditions)
            {
                line.Append(' ').Append(condition.Kind).Append('=').Append(Number(condition.RemainingDays)).Append('d');
            }
            log.Commit();
        }

        /// <summary>«#3 Имя».</summary>
        public static StringBuilder AppendName(StringBuilder line, Adventurer adventurer) =>
            line.Append('#').Append(adventurer.Id.ToString(CultureInfo.InvariantCulture)).Append(' ').Append(adventurer.Name);

        /// <summary>Число для лога: до двух знаков после точки, без зависимости от культуры.</summary>
        public static string Number(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
