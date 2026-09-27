using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>Особая черта у человека. Правила появления и замены — <see cref="TraitService"/>.</summary>
    public sealed class TraitInstance
    {
        internal TraitInstance(string traitId, long acquiredAtHours, int partnerId, StatId? affectedStat)
        {
            TraitId = traitId;
            AcquiredAtHours = acquiredAtHours;
            PartnerId = partnerId;
            AffectedStat = affectedStat;
        }

        /// <summary>Id <see cref="SpecialTraitDefinition"/>.</summary>
        public string TraitId { get; }

        /// <summary>Черта видна игроку. Все черты скрыты до триггера раскрытия (<see cref="RevealService"/>).</summary>
        public bool Revealed { get; internal set; }

        /// <summary>Когда черта раскрыта (для отчёта месяца); 0 — не раскрыта.</summary>
        public long RevealedAtHours { get; internal set; }

        /// <summary>С кем связана черта: партнёр Соперника и Влюблённого, погибший у Потерявшего товарища. 0 — ни с кем.</summary>
        public int PartnerId { get; internal set; }

        /// <summary>Когда появилась (у черт «от рождения» — время генерации).</summary>
        public long AcquiredAtHours { get; }

        /// <summary>Параметр, на который действует эффект <c>ProfileMultiplier</c> (какая характеристика снижена у Калеки).</summary>
        public StatId? AffectedStat { get; }

        /// <summary>Тип задания, после которого появилась черта (Кошмары); пусто — не от задания.</summary>
        public string SourceQuestTypeId { get; internal set; } = string.Empty;
    }
}
