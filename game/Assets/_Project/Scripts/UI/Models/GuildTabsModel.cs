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

        /// <summary>Id строки для выбора в таблице: постройки мира — её id, не начатой — отрицательное число по порядку данных.</summary>
        public int RowId;

        public string DefinitionId;
        public int Cost;
        public int BuildDays;

        /// <summary>Не построена и не в очереди — её можно заказать.</summary>
        public bool CanOrder;

        /// <summary>В очереди (стройка не начата) — её можно двигать и снять.</summary>
        public bool Queued;
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
                var row = new BuildingRow
                {
                    Name = definition.DisplayName,
                    Capacity = string.Empty,
                    RowId = -(rows.Count + 1),
                    DefinitionId = definition.Id,
                    Cost = definition.Cost,
                    BuildDays = definition.BuildDays,
                };
                if (world.Buildings.TryGetByDefinition(definition.Id, out Building building))
                {
                    row.Id = building.Id;
                    row.RowId = building.Id;
                    row.Ready = building.IsReady;
                    row.Queued = building.State == BuildingState.Planned;
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
                    row.CanOrder = true;
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

    /// <summary>Постройка в очереди строек: по порядку, первая начнётся следующей.</summary>
    public sealed class QueueRow
    {
        public int Id;
        public string Name;
        public string Details;
    }

    /// <summary>Действия игрока с постройками: заказать стройку, порядок очереди, снять из очереди. Все — командами.</summary>
    public static class BuildingActions
    {
        public static List<QueueRow> Queue(ISimulationClient client)
        {
            var rows = new List<QueueRow>();
            foreach (Building building in client.World.Buildings.Queue)
            {
                string name = client.Data.TryGet(building.DefinitionId, out BuildingDefinition definition) ? definition.DisplayName : building.DefinitionId;
                string details = definition != null
                    ? $"{UiFormat.Money(definition.Cost)}, {string.Format(UiStrings.DaysFormat, definition.BuildDays)}"
                    : string.Empty;
                rows.Add(new QueueRow { Id = building.Id, Name = name, Details = details });
            }
            return rows;
        }

        public static void Order(ISimulationClient client, string definitionId) => client.Send(new StartBuildingCommand(definitionId));

        public static void Cancel(ISimulationClient client, int buildingId) => client.Send(new CancelBuildingCommand(buildingId));

        /// <summary>Сдвинуть стройку по очереди на <paramref name="delta"/> мест (−1 — выше). За край — ничего.</summary>
        public static void Move(ISimulationClient client, int buildingId, int delta)
        {
            List<int> order = QueueIds(client);
            int from = order.IndexOf(buildingId);
            if (from < 0) return;
            MoveTo(client, buildingId, from + delta);
        }

        /// <summary>Поставить стройку на место <paramref name="index"/> в очереди (перетаскивание). То же место или за край — ничего.</summary>
        public static void MoveTo(ISimulationClient client, int buildingId, int index)
        {
            List<int> order = QueueIds(client);
            int from = order.IndexOf(buildingId);
            if (from < 0 || index < 0 || index >= order.Count || index == from) return;
            order.RemoveAt(from);
            order.Insert(index, buildingId);
            client.Send(new ReorderBuildQueueCommand(order));
        }

        private static List<int> QueueIds(ISimulationClient client)
        {
            var ids = new List<int>();
            foreach (Building building in client.World.Buildings.Queue) ids.Add(building.Id);
            return ids;
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

    /// <summary>Кандидат на вакансию: уровень, просимая зарплата, сколько ещё ждёт; отказ — только в интерфейсе.</summary>
    public sealed class StaffCandidateRow
    {
        public int Id;
        public string Name;
        public string Role;
        public string RoleId;
        public int Level;
        public int AskedSalary;
        public string Waits;
        public string Details;
        public bool Rejected;
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

        public static List<StaffCandidateRow> Candidates(ISimulationClient client, ICollection<int> rejected = null)
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
                    RoleId = candidate.RoleId,
                    Level = candidate.Level,
                    AskedSalary = candidate.AskedSalary,
                    Waits = UiFormat.Duration(candidate.ExpiresAtHours - now, client.Calendar),
                    Rejected = rejected != null && rejected.Contains(candidate.Id),
                    Details = string.Format(UiStrings.StaffCandidateFormat, candidate.Level, UiFormat.Money(candidate.AskedSalary),
                        UiFormat.Duration(candidate.ExpiresAtHours - now, client.Calendar)),
                });
            }
            return rows;
        }

        /// <summary>
        /// Вакансии: должности без сотрудника, у которых постройка готова (или не нужна), — ждут ли кандидаты и когда придут
        /// следующие.
        /// </summary>
        public static List<string> Vacancies(ISimulationClient client)
        {
            var rows = new List<string>();
            WorldState world = client.World;
            long now = world.Time.TotalHours;
            foreach (StaffRoleDefinition role in client.Data.All<StaffRoleDefinition>())
            {
                if (!StaffRules.IsVacant(world, role)) continue;
                int waiting = 0;
                foreach (StaffCandidate candidate in world.Staff.Candidates)
                {
                    if (candidate.RoleId == role.Id) waiting++;
                }
                if (waiting > 0)
                    rows.Add(string.Format(UiStrings.VacancyCandidatesFormat, role.DisplayName, waiting));
                else if (world.Staff.TryGetNextCandidatesAt(role.Id, out long at) && at > now)
                    rows.Add(string.Format(UiStrings.VacancyWaitsFormat, role.DisplayName, UiFormat.Duration(at - now, client.Calendar)));
                else
                    rows.Add(string.Format(UiStrings.VacancyOpenFormat, role.DisplayName));
            }
            return rows;
        }

        /// <summary>Предложить кандидату зарплату: не меньше просимой — нанят, меньше — может отказаться и уйти.</summary>
        public static void Offer(ISimulationClient client, int candidateId, int amount) =>
            client.Send(new OfferSalaryCommand(candidateId, System.Math.Max(0, amount)));

        public static void Dismiss(ISimulationClient client, int staffId) => client.Send(new DismissStaffCommand(staffId));

        public static string RoleName(DataRegistry data, string roleId) =>
            data.TryGet(roleId, out StaffRoleDefinition role) ? role.DisplayName : roleId;
    }
}
