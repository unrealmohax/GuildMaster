using System.Collections.Generic;

namespace GuildMaster.Core
{
    /// <summary>Кем родной приходится человеку.</summary>
    public enum FamilyRole
    {
        Parent,
        Spouse,
        Child,
    }

    /// <summary>Здоровье родного.</summary>
    public enum FamilyHealth
    {
        Healthy,
        Ill,
    }

    /// <summary>Весть из дома, которую человек на задании узнает по возвращении.</summary>
    public enum FamilyNews
    {
        None,
        Ill,
        Died,
    }

    /// <summary>
    /// Родной за кадром: роль, пол, имя, здоровье, жив ли. Умершие остаются в списке (для карточки).
    /// Часть мира: менять — <see cref="FamilyService"/>, генерация — <see cref="FamilyGenerator"/>.
    /// </summary>
    public sealed class FamilyMember
    {
        internal FamilyMember(FamilyRole role, Gender gender, string name)
        {
            Role = role;
            Gender = gender;
            Name = name ?? string.Empty;
        }

        public FamilyRole Role { get; }
        public Gender Gender { get; }
        public string Name { get; }

        public FamilyHealth Health { get; internal set; }

        /// <summary>Когда заболел последний раз; −1 — не болел.</summary>
        public long IllSinceHours { get; internal set; } = -1;

        /// <summary>Когда поправится (болезнь идёт, пока время меньше).</summary>
        public long IllUntilHours { get; internal set; }

        public bool Alive { get; internal set; } = true;

        /// <summary>Когда умер; −1 — жив.</summary>
        public long DiedAtHours { get; internal set; } = -1;

        /// <summary>Весть о нём ещё не дошла до человека (был на задании).</summary>
        public FamilyNews PendingNews { get; internal set; }

        public bool IsIll => Alive && Health == FamilyHealth.Ill;
    }

    /// <summary>Семья человека: родители, супруг, дети. Состав задаёт генерация, дальше он меняется только смертью.</summary>
    public sealed class Family
    {
        private readonly List<FamilyMember> members = new List<FamilyMember>();

        internal Family()
        {
        }

        /// <summary>Все родные, живые и умершие, в порядке генерации: родители, супруг, дети.</summary>
        public IReadOnlyList<FamilyMember> Members => members;

        /// <summary>Живых родных.</summary>
        public int LivingCount
        {
            get
            {
                int count = 0;
                foreach (FamilyMember member in members)
                {
                    if (member.Alive) count++;
                }
                return count;
            }
        }

        public bool HasLiving => LivingCount > 0;

        public bool HasLivingSpouse()
        {
            foreach (FamilyMember member in members)
            {
                if (member.Alive && member.Role == FamilyRole.Spouse) return true;
            }
            return false;
        }

        /// <summary>Первый больной из живых родных.</summary>
        public bool TryGetIll(out FamilyMember ill)
        {
            foreach (FamilyMember member in members)
            {
                if (!member.IsIll) continue;
                ill = member;
                return true;
            }
            ill = null;
            return false;
        }

        internal void Add(FamilyMember member) => members.Add(member);

        internal bool Remove(FamilyMember member) => members.Remove(member);
    }
}
