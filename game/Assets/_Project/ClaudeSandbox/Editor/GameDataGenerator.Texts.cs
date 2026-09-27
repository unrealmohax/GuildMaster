using System.Collections.Generic;
using System.Linq;
using GuildMaster.Data;

namespace GuildMaster.ClaudeSandbox
{
    // Новые данные, которых нет в документах: имена, названия групп, тексты заказов и намёки — предложение Claude, правит автор.
    // У имён — все шесть падежей (правила склонения ниже); у названий заказов пока только именительный падеж.
    public static partial class GameDataGenerator
    {
        private static void FillNames(NameList names)
        {
            Set(names, "maleNames", MaleNames(
                "Бран", "Ян", "Годрик", "Ведран", "Мирко", "Вацлав", "Отто", "Ульрих", "Радек", "Зденек",
                "Бертольд", "Любош", "Горан", "Эрих", "Ратибор", "Дитрих", "Мирослав", "Карл", "Станек", "Вит",
                "Любор", "Ганс", "Борек", "Вальтер", "Милош", "Конрад", "Добрило", "Генрих", "Людек", "Тибор",
                "Эберхард", "Роман", "Густав", "Остромир", "Лотар", "Ярек", "Лех", "Рупрехт", "Иржи", "Фолькер",
                "Бронек", "Вольф", "Домаш", "Хельмут", "Мстиш", "Альбрехт", "Будивой", "Руперт", "Злат", "Вилем",
                "Томаш", "Людвиг", "Пшемысл", "Гюнтер", "Драган", "Анзельм", "Велимир", "Рихард", "Божек", "Ортвин",
                "Славек", "Бодо", "Матей", "Стойко"));
            Set(names, "femaleNames", FemaleNames(
                "Ида", "Мара", "Вера", "Хельга", "Ярослава", "Грета", "Мила", "Зденка", "Берта", "Люба",
                "Ирма", "Бланка", "Добрава", "Эльза", "Радка", "Гертруда", "Власта", "Марта", "Божена", "Хильда",
                "Ленка", "Ута", "Драгана", "Агнеш", "Ружена", "Фрида", "Милена", "Ганна", "Катрин", "Злата",
                "Эмма", "Стана", "Лотта", "Дана", "Гизела", "Мирослава", "Адела", "Ольга", "Бригитта", "Весна",
                "Ингрид", "Людмила", "Клара", "Горислава", "Эрна", "Ядвига", "Роза", "Цветана", "Ханна", "Любица",
                "Матильда", "Даря", "Вильма", "Беляна", "Ирена", "Зорица", "Минна", "Хедвига", "Славка", "Отилия",
                "Рада", "Магда", "Лидка", "Бета"));
            Set(names, "groupAdjectives", new List<string>
            {
                "Серые", "Ржавые", "Чёрные", "Старые", "Тихие", "Битые", "Поздние", "Хмурые", "Вольные", "Горькие",
                "Ночные", "Седые", "Кривые", "Упрямые", "Бледные", "Злые", "Последние", "Дальние", "Сырые", "Ломаные",
            });
            Set(names, "groupNouns", new List<string>
            {
                "волки", "щиты", "вороны", "псы", "клинки", "сапоги", "котлы", "топоры", "фонари", "кулаки",
                "лисы", "совы", "копья", "мосты", "камни", "шипы", "барсуки", "ножи", "гвозди", "колья",
            });
        }

        private static void FillOrderTexts(OrderTextTemplates texts, Dictionary<string, QuestTypeDefinition> q)
        {
            Set(texts, "clients", Nouns(M,
                "мельник", "староста", "купец", "кузнец", "лесничий", "пастух", "мытарь", "лавочник", "возчик", "горожанин"));

            Set(texts, "questTypes", new List<QuestTypeTexts>
            {
                TypeTexts(q["Hunt"],
                    places: Nouns(("старая мельница", F), ("Волчий лог", M), ("Чёрное болото", N), ("брод у Кривой ивы", M), ("опушка Сухого леса", F), ("Медвежий овраг", M)),
                    enemies: Nouns(M, "медведь-шатун", "волк-людоед", "секач", "матёрый волк"),
                    cargo: new List<NounForms>(),
                    "{заказчик} просит выследить {враг:в} у {место:р}",
                    "У {место:р} объявился {враг}. {заказчик} платит за шкуру",
                    "{заказчик} просит избавить округу от {враг:р}"),
                TypeTexts(q["Extermination"],
                    places: Nouns(("заброшенная каменоломня", F), ("овраг за погостом", M), ("развалины старой заставы", P), ("Гнилая балка", F), ("пустой хутор", M)),
                    enemies: Nouns(M, "волчий выводок", "выводок одичавших псов", "кабаний выводок"),
                    cargo: new List<NounForms>(),
                    "{заказчик} просит выжечь логово у {место:р}",
                    "У {место:р} расплодился {враг}. {заказчик} просит зачистить",
                    "{заказчик} платит, чтобы у {место:р} стало тихо"),
                TypeTexts(q["Escort"],
                    places: Nouns(("Северный тракт", M), ("перевал Сорочий зуб", M), ("ярмарка в Нижних Бродах", F), ("Вдовий мост", M), ("Соляная дорога", F)),
                    enemies: Nouns(M, "разбойничий отряд", "лихой люд", "главарь разбойников"),
                    cargo: Nouns(("обоз с солью", M), ("телеги с зерном", P), ("купеческий обоз", M), ("сундук с податью", M)),
                    "{заказчик} просит проводить {груз:в} до {место:р}",
                    "{заказчик} ищет охрану: {груз} идёт через {место:в}"),
                TypeTexts(q["Delivery"],
                    places: Nouns(("Нижние Броды", P), ("монастырь на Лысой горе", M), ("застава у Волчьих ворот", F), ("хутор за Чёрным болотом", M), ("Дальний погост", M)),
                    enemies: Nouns(M, "разбойничий отряд", "грабитель с большой дороги", "голодный волк"),
                    cargo: Nouns(("письмо", N), ("мешки с зерном", P), ("свёрток с лекарствами", M), ("запечатанный ларец", M)),
                    "{заказчик} просит доставить {груз:в} в {место:в}",
                    "{заказчик} просит отнести {груз:в} до {место:р}. Спешно"),
            });

            Set(texts, "hints", new List<AxisHint>
            {
                Hint(StatId.Strength, "Их там целая стая", "Придётся рубить, а не уговаривать"),
                Hint(StatId.Reaction, "Нападает внезапно", "Всё решится в один миг"),
                Hint(StatId.Marksmanship, "Близко к нему не подойти", "Бить придётся издалека"),
                Hint(StatId.Stealth, "Лишнего внимания не нужно", "Лучше, чтобы никто не видел"),
                Hint(StatId.Agility, "Тропа узкая, над обрывом", "Там легко оступиться"),
                Hint(StatId.Medicine, "Раненых там уже было немало", "Кто ходил — возвращались покалеченными"),
                Hint(StatId.Composure, "Там уже кто-то дрогнул и побежал", "Нервы понадобятся крепкие"),
                Hint(StatId.Charisma, "С местными придётся договариваться", "Заказчик — человек тяжёлый"),
                Hint(StatId.Perception, "Говорят, зверь хитёр и осторожен", "Следов почти не оставляет"),
                Hint(StatId.Survival, "Дорога идёт через глушь", "Жилья по пути нет"),
                Hint(StatId.Knowledge, "Никто толком не знает, что это за тварь", "Местные рассказывают разное, и всё — странное"),
                Hint(StatId.Crafting, "Снаряжение там быстро приходит в негодность", "Что-то придётся мастерить на месте"),
                Hint(StatId.Endurance, "Их там целая стая", "Дело долгое, отдыхать будет некогда"),
            });
            Set(texts, "farHint", "Путь неблизкий");
            Set(texts, "partySizeCeilingHint", "Лишнего внимания не нужно");
        }

        // ---------- Склонение имён ----------

        // Не склоняются: мужские на -о и -и (кроме Добрило), женские на согласную.
        private static readonly HashSet<string> IndeclinableNames = new HashSet<string> { "Отто", "Бодо", "Мирко", "Стойко", "Иржи", "Агнеш", "Ингрид", "Катрин" };

        private static readonly string Hushing = "жшчщц";
        private static readonly string VelarOrHushing = "гкхжшчщ";

        private static List<NounForms> MaleNames(params string[] names) => names.Select(DeclineMale).ToList();

        private static List<NounForms> FemaleNames(params string[] names) => names.Select(DeclineFemale).ToList();

        /// <summary>Мужское имя: на согласную — как «Ян», на -й — как «Матей», на -о (Добрило) — как «Данила».</summary>
        internal static NounForms DeclineMale(string name)
        {
            if (IndeclinableNames.Contains(name)) return Forms(name, name, name, name, name, name, M);
            char last = name[name.Length - 1];
            if (last == 'о')
            {
                string stem = name.Substring(0, name.Length - 1);
                return Forms(name, stem + "ы", stem + "е", stem + "у", stem + "ой", stem + "е", M);
            }
            if (last == 'й')
            {
                string stem = name.Substring(0, name.Length - 1);
                return Forms(name, stem + "я", stem + "ю", stem + "я", stem + "ем", stem + "е", M);
            }
            string instrumental = Hushing.IndexOf(last) >= 0 ? "ем" : "ом";
            return Forms(name, name + "а", name + "у", name + "а", name + instrumental, name + "е", M);
        }

        /// <summary>Женское имя: на -а — как «Мара» / «Ольга» / «Любица», на -я — как «Даря», на -ия — как «Отилия».</summary>
        internal static NounForms DeclineFemale(string name)
        {
            if (IndeclinableNames.Contains(name)) return Forms(name, name, name, name, name, name, F);
            if (name.EndsWith("ия"))
            {
                string stem = name.Substring(0, name.Length - 1);
                return Forms(name, stem + "и", stem + "и", stem + "ю", stem + "ей", stem + "и", F);
            }
            if (name.EndsWith("я"))
            {
                string stem = name.Substring(0, name.Length - 1);
                return Forms(name, stem + "и", stem + "е", stem + "ю", stem + "ей", stem + "е", F);
            }
            if (name.EndsWith("а"))
            {
                string stem = name.Substring(0, name.Length - 1);
                char last = stem[stem.Length - 1];
                string genitive = VelarOrHushing.IndexOf(last) >= 0 ? "и" : "ы";
                string instrumental = Hushing.IndexOf(last) >= 0 ? "ей" : "ой";
                return Forms(name, stem + genitive, stem + "е", stem + "у", stem + instrumental, stem + "е", F);
            }
            throw new System.ArgumentException($"Не знаю, как склонять женское имя «{name}»: добавьте его в IndeclinableNames или правило");
        }

        private static NounForms Forms(string nominative, string genitive, string dative, string accusative, string instrumental, string prepositional,
            GrammaticalGender gender) =>
            new NounForms(nominative, genitive, dative, accusative, instrumental, prepositional, gender);

        private static QuestTypeTexts TypeTexts(QuestTypeDefinition questType, List<NounForms> places, List<NounForms> enemies, List<NounForms> cargo, params string[] descriptions) =>
            Make<QuestTypeTexts>(("questType", questType), ("places", places), ("enemies", enemies),
                ("cargo", cargo), ("descriptions", descriptions.ToList()));

        private static AxisHint Hint(StatId stat, params string[] lines) =>
            Make<AxisHint>(("stat", stat), ("lines", lines.ToList()));
    }
}
