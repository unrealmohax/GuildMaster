using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GuildMaster.Core;
using GuildMaster.Data;
using GuildMaster.Debugging;
using UnityEditor;
using UnityEngine;

namespace GuildMaster.ClaudeSandbox
{
    /// <summary>
    /// Песочница. Летопись постоянных групп за год (зерно 1, бот «Простой»): когда сложилась и из кого, кто вступил и вышел
    /// (когда и почему), распад, сколько заданий группой; у каждого — ранг и архетип на момент события и на конец года.
    /// Итог — в файл <c>Logs/parties_year_{зерно}.txt</c> и путь в консоль.
    /// </summary>
    public static class PartyChronicle
    {
        private sealed class Record
        {
            public Party Party;
            public string Title;
            public readonly List<string> Lines = new List<string>();
            public int Quests;
        }

        [MenuItem("GuildMaster/Sandbox/Parties/Chronicle Year Seed 1")]
        private static void Chronicle() => Run(1u);

        private static void Run(uint seed)
        {
            DataRegistry data = DataRegistry.FromConfig(EditorAssets.FindGameConfig());
            var records = new Dictionary<int, Record>();
            Simulation sim = null;
            HeadlessRun.Result result = HeadlessRun.Run(data, seed, 360, PlayerBots.Simple(), null, simulation =>
            {
                sim = simulation;
                simulation.TickCompleted += events => Observe(simulation, data, events, records);
            });

            var text = new StringBuilder();
            text.AppendLine($"Постоянные группы за год: зерно {seed}, бот «Простой», {result.Ticks} тактов, конец — {sim.World.Time}");
            text.AppendLine($"Сложилось {records.Count}, распалось {sim.World.Parties.PermanentDisbanded}, людей в гильдии на конец года {sim.World.Adventurers.Active.Count}");
            text.AppendLine();
            foreach (Record record in records.Values.OrderBy(r => r.Party.Id))
            {
                bool alive = sim.World.Parties.TryGetParty(record.Party.Id, out _);
                text.AppendLine($"=== #{record.Party.Id} «{record.Title}» — {(alive ? "на конец года есть" : "распалась")}, заданий группой {record.Quests}, " +
                                $"совместных заданий {record.Party.JointQuests} (выполнено {record.Party.JointSuccesses})");
                foreach (string line in record.Lines) text.AppendLine("  " + line);
                if (alive)
                {
                    text.AppendLine($"  Состав на конец года ({record.Party.MemberIds.Count}):");
                    foreach (int id in record.Party.MemberIds) text.AppendLine("    " + Person(sim, data, id));
                }
                text.AppendLine();
            }

            string path = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "Logs", $"parties_year_{seed}.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            File.WriteAllText(path, text.ToString(), new UTF8Encoding(true));
            Debug.Log($"[PartyChronicle] {records.Count} групп → {path}");
        }

        private static void Observe(Simulation sim, DataRegistry data, IReadOnlyList<SimEvent> events, Dictionary<int, Record> records)
        {
            foreach (SimEvent e in events)
            {
                if (!e.TryGet("partyId", out int partyId)) continue;
                string when = sim.Calendar.At(e.TimeHours).ToString();
                switch (e.Type)
                {
                    case SimEventType.PermanentPartyFormed:
                        sim.World.Parties.TryGetParty(partyId, out Party party);
                        e.TryGet("title", out string title);
                        var record = new Record { Party = party, Title = title };
                        record.Lines.Add($"{when} сложилась, {e.Participants.Count} чел.:");
                        foreach (int id in e.Participants) record.Lines.Add("    " + Person(sim, data, id));
                        records[partyId] = record;
                        break;
                    case SimEventType.PartyMemberJoined when records.TryGetValue(partyId, out Record joined):
                        joined.Lines.Add($"{when} вступил(а): {Person(sim, data, e.Participants[0])} — теперь {joined.Party.MemberIds.Count} чел.");
                        break;
                    case SimEventType.PartyMemberLeft when records.TryGetValue(partyId, out Record left):
                        e.TryGet("cause", out string cause);
                        string why = cause == "quarrel" ? "ссора с " + Name(sim, e.Participants[1])
                            : cause == "loner" ? "одиночка" : "погиб(ла) или ушёл(ла) из гильдии";
                        left.Lines.Add($"{when} вышел(а): {Person(sim, data, e.Participants[0])} — {why}; осталось {left.Party.MemberIds.Count} чел.");
                        break;
                    case SimEventType.PermanentPartyDisbanded when records.TryGetValue(partyId, out Record gone):
                        gone.Lines.Add($"{when} распалась" + (e.Participants.Count > 0 ? ", последний — " + Name(sim, e.Participants[0]) : ""));
                        break;
                    case SimEventType.PartyGathered when records.TryGetValue(partyId, out Record went):
                        went.Quests++;
                        break;
                }
            }
        }

        private static string Name(Simulation sim, int id) =>
            sim.World.Adventurers.TryGetKnown(id, out Adventurer a) ? $"#{a.Id} {a.Name}" : "#" + id;

        /// <summary>«#3 Ян, ранг E, Щит, Люди +45» — ранг и архетип сейчас; ушедший — с пометкой.</summary>
        private static string Person(Simulation sim, DataRegistry data, int id)
        {
            if (!sim.World.Adventurers.TryGetKnown(id, out Adventurer a)) return "#" + id;
            string archetype = data.TryGet(a.ArchetypeId, out ArchetypeDefinition def) ? def.DisplayName : a.ArchetypeId;
            string state = sim.World.Adventurers.IsActive(id) ? "" : a.LeaveReason == LeaveReason.Died ? " [погиб(ла)]" : " [ушёл(ла)]";
            return $"#{a.Id} {a.Name}, ранг {a.GuildRank}, {archetype}, Люди {a.GetAxis(AxisId.People):+0;-0;0}{state}";
        }
    }
}
