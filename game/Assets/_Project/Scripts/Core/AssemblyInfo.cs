using System.Runtime.CompilerServices;

// Тестам нужен доступ к internal-сеттерам мира, как системам симуляции.
[assembly: InternalsVisibleTo("GuildMaster.Tests")]
