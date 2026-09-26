using System;
using UnityEngine;

namespace GuildMaster.Data
{
    /// <summary>Раздел <c>Travel</c>: путь, события в пути, находки (numbers.md → Длительность, Случайные события, ТЗ 09).</summary>
    [Serializable]
    public sealed class TravelBalance
    {
        [Tooltip("Близко: путь в одну сторону, часов")]
        [SerializeField, Min(0)] private int nearHours = 3;

        [Tooltip("Далеко: путь в одну сторону, часов дневного времени")]
        [SerializeField] private IntRange farHours = new IntRange(24, 36);

        [Tooltip("Случайное событие: шанс на каждый путь, близко")]
        [SerializeField, Range(0f, 1f)] private float eventChanceNear = 0.1f;

        [Tooltip("Случайное событие: шанс на каждый путь, далеко")]
        [SerializeField, Range(0f, 1f)] private float eventChanceFar = 0.25f;

        [Tooltip("После отступления шанс события на обратном пути ×")]
        [SerializeField, Min(0f)] private float eventChanceAfterRetreatMultiplier = 1.5f;

        [Tooltip("Стресс всем при провале события")]
        [SerializeField, Min(0f)] private float eventFailStress = 5f;

        [Tooltip("Находка: шанс за задание")]
        [SerializeField, Range(0f, 1f)] private float discoveryChance = 0.05f;

        public int NearHours => nearHours;
        public IntRange FarHours => farHours;
        public float EventChanceNear => eventChanceNear;
        public float EventChanceFar => eventChanceFar;
        public float EventChanceAfterRetreatMultiplier => eventChanceAfterRetreatMultiplier;
        public float EventFailStress => eventFailStress;
        public float DiscoveryChance => discoveryChance;
    }
}
