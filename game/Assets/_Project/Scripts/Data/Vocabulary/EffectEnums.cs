namespace GuildMaster.Data
{
    // Значения записаны в ассетах числами: новые — только в конец, существующие не менять.

    /// <summary>Сила влияния черты. Множитель на крайнем полюсе — <see cref="DecisionsBalance"/>.</summary>
    public enum Arrow
    {
        StrongDown = -2,
        Down = -1,
        None = 0,
        Up = 1,
        StrongUp = 2,
    }

    /// <summary>Вид эффекта черты (<see cref="TraitEffect"/>).</summary>
    public enum EffectKind
    {
        /// <summary>Вес мотива: мотив + стрелка.</summary>
        MotiveWeight = 0,
        /// <summary>Скорость роста или падения показателя состояния: стрелка или явный множитель.</summary>
        StateRate = 1,
        /// <summary>Ошибка оценки риска: доля к воспринимаемым требованиям (+0,3 — переоценивает).</summary>
        RiskPerception = 2,
        /// <summary>Добавка к шансу реакции в момент напряжения.</summary>
        TensionModifier = 3,
        /// <summary>Множитель чувствительности довольства к комиссии.</summary>
        PayContentmentSensitivity = 4,
        /// <summary>Предпочтение размера группы.</summary>
        PartyPreference = 5,
        /// <summary>Множитель к параметру профиля (Калека).</summary>
        ProfileMultiplier = 6,
    }

    /// <summary>Когда эффект действует.</summary>
    public enum EffectCondition
    {
        Always = 0,
        /// <summary>Человек в группе (не один).</summary>
        InGroup = 1,
        /// <summary>Человек один.</summary>
        Solo = 2,
        /// <summary>Только на крайнем полюсе оси (|значение| ≥ порога крайнего полюса).</summary>
        ExtremePole = 3,
        /// <summary>Стресс выше порога эффекта.</summary>
        StressAbove = 4,
    }

    /// <summary>Показатель состояния для <see cref="EffectKind.StateRate"/>.</summary>
    public enum StateStat
    {
        Stress = 0,
        Contentment = 1,
        Loyalty = 2,
        Fatigue = 3,
    }

    public enum RateDirection
    {
        Growth = 0,
        Decay = 1,
    }

    /// <summary>Реакция в момент напряжения.</summary>
    public enum TensionKind
    {
        Panic = 0,
        Flee = 1,
        Rush = 2,
        Heroism = 3,
    }

    public enum PartySizePreference
    {
        None = 0,
        Solo = 1,
        SmallGroup = 2,
        Group = 3,
    }
}
