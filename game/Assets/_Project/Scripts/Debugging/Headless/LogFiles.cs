using System;
using System.Globalization;
using System.IO;
using GuildMaster.Core;
using UnityEngine;

namespace GuildMaster.Debugging
{
    /// <summary>Имена и папка файлов прогона без интерфейса, копия лога в консоль Unity.</summary>
    public static class LogFiles
    {
        /// <summary>Папка <c>Logs</c> рядом с <c>Assets</c> — в папке проекта, вне ассетов.</summary>
        public static string DefaultFolder => Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "Logs");

        /// <summary>Метка реального времени запуска для имени лога: <c>20260927_153012</c>.</summary>
        public static string Stamp(DateTime time) => time.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);

        public static string LogName(uint seed, string stamp) => $"sim_{Seed(seed)}_{stamp}.log";

        public static string SummaryName(uint seed) => $"summary_{Seed(seed)}.csv";

        public static string ScenarioName(uint seed) => $"scenario_{Seed(seed)}.txt";

        /// <summary>Свёртка нескольких прогонов, начиная с зерна <paramref name="firstSeed"/>.</summary>
        public static string AggregateName(uint firstSeed, int runs) => $"summary_{Seed(firstSeed)}_x{runs.ToString(CultureInfo.InvariantCulture)}.csv";

        /// <summary>Строка лога в консоль Unity: ошибки — <see cref="Debug.LogError(object)"/>, остальное — <see cref="Debug.Log(object)"/>.</summary>
        public static void UnityConsole(SimLogLevel level, string line)
        {
            if (level == SimLogLevel.Error) Debug.LogError(line);
            else Debug.Log(line);
        }

        private static string Seed(uint seed) => seed.ToString(CultureInfo.InvariantCulture);
    }
}
