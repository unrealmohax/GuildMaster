using System.Collections.Generic;
using GuildMaster.Core;
using GuildMaster.Data;

namespace GuildMaster.UI
{
    /// <summary>Постоянная группа и её члены (со ссылками на карточки).</summary>
    public sealed class PartyRow
    {
        public int Id;
        public string Name;
        public List<(int Id, string Name)> Members = new List<(int, string)>();
        public string Stats;
        public bool OnQuest;
    }

    /// <summary>Вкладка «Группы»: постоянные группы.</summary>
    public static class PartiesModel
    {
        public static List<PartyRow> Build(ISimulationClient client)
        {
            var rows = new List<PartyRow>();
            WorldState world = client.World;
            foreach (Party party in world.Parties.Active)
            {
                if (!party.IsPermanent) continue;
                var row = new PartyRow
                {
                    Id = party.Id,
                    Name = party.Name,
                    Stats = string.Format(UiStrings.PartyStatsFormat, party.JointQuests, party.JointSuccesses),
                };
                foreach (int id in party.MemberIds)
                {
                    if (!world.Adventurers.TryGetKnown(id, out Adventurer member)) continue;
                    row.Members.Add((id, member.Name));
                    if (member.State.IsOnQuest()) row.OnQuest = true;
                }
                rows.Add(row);
            }
            return rows;
        }
    }

    /// <summary>Постройка: название, стадия (готова, строится — сколько дней, в очереди, не построена), места.</summary>
    public sealed class BuildingRow
    {
        /// <summary>Id постройки мира; 0 — не построена и не в очереди.</summary>
        public int Id;

        public string Name;
        public string State;
        public bool Ready;

        /// <summary>«7/10» — занято из мест; пусто — у постройки нет мест.</summary>
        public string Capacity;
    }

    /// <summary>Вкладка «Постройки»: все постройки из данных — построенные, стройка, очередь, не начатые.</summary>
    public static class BuildingsModel
    {
        public static List<BuildingRow> Build(ISimulationClient client)
        {
            var rows = new List<BuildingRow>();
            WorldState world = client.World;
            long now = world.Time.TotalHours;
            foreach (BuildingDefinition definition in client.Data.All<BuildingDefinition>())
            {
                var row = new BuildingRow { Name = definition.DisplayName, Capacity = string.Empty };
                if (world.Buildings.TryGetByDefinition(definition.Id, out Building building))
                {
                    row.Id = building.Id;
                    row.Ready = building.IsReady;
                    switch (building.State)
                    {
                        case BuildingState.Ready:
                            row.State = UiText.Render(client.Data, UiTextKeys.BuildingReady);
                            break;
                        case BuildingState.UnderConstruction:
                            int days = UiFormat.DaysLeft(building.ConstructionEndsAtHours, now, client.Calendar);
                            row.State = UiText.Render(client.Data, UiTextKeys.BuildingConstruction, new UiTextSource().Set("число", days));
                            break;
                        default:
                            row.State = UiText.Render(client.Data, UiTextKeys.BuildingQueued);
                            break;
                    }
                }
                else
                {
                    row.State = UiText.Render(client.Data, UiTextKeys.BuildingNotBuilt);
                }

                int capacity = BuildingRules.Capacity(definition);
                if (capacity > 0) row.Capacity = $"{(row.Ready ? Occupied(world, definition) : 0)}/{capacity}";
                rows.Add(row);
            }
            return rows;
        }

        /// <summary>Сколько мест занято: жильцы Общежития, пациенты Лазарета, тренирующиеся на дворе.</summary>
        private static int Occupied(WorldState world, BuildingDefinition definition)
        {
            switch (definition.Function)
            {
                case BuildingFunction.Dormitory: return BuildingRules.DormitoryResidents(world);
                case BuildingFunction.Infirmary: return BuildingRules.Patients(world);
                case BuildingFunction.TrainingYard: return BuildingRules.Trainees(world);
                default: return 0;
            }
        }
    }

    /// <summary>Сотрудник: должность, уровень, зарплата, долг по зарплате.</summary>
    public sealed class StaffRow
    {
        public int Id;
        public string Name;
        public string Role;
        public int Level;
        public int Salary;
        public int Unpaid;
    }

    /// <summary>Кандидат на вакансию — только для просмотра.</summary>
    public sealed class StaffCandidateRow
    {
        public int Id;
        public string Name;
        public string Role;
        public string Details;
    }

    /// <summary>Вкладка «Персонал»: сотрудники и кандидаты на вакансии.</summary>
    public static class StaffModel
    {
        public static List<StaffRow> Members(ISimulationClient client)
        {
            var rows = new List<StaffRow>();
            foreach (StaffMember member in client.World.Staff.Members)
            {
                rows.Add(new StaffRow
                {
                    Id = member.Id,
                    Name = member.Name,
                    Role = RoleName(client.Data, member.RoleId),
                    Level = member.Level,
                    Salary = member.Salary,
                    Unpaid = member.UnpaidSalary,
                });
            }
            return rows;
        }

        public static List<StaffCandidateRow> Candidates(ISimulationClient client)
        {
            var rows = new List<StaffCandidateRow>();
            long now = client.World.Time.TotalHours;
            foreach (StaffCandidate candidate in client.World.Staff.Candidates)
            {
                rows.Add(new StaffCandidateRow
                {
                    Id = candidate.Id,
                    Name = candidate.Name,
                    Role = RoleName(client.Data, candidate.RoleId),
                    Details = string.Format(UiStrings.StaffCandidateFormat, candidate.Level, UiFormat.Money(candidate.AskedSalary),
                        UiFormat.Duration(candidate.ExpiresAtHours - now, client.Calendar)),
                });
            }
            return rows;
        }

        public static string RoleName(DataRegistry data, string roleId) =>
            data.TryGet(roleId, out StaffRoleDefinition role) ? role.DisplayName : roleId;
    }
}
