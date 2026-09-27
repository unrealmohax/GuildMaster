using System.Collections.Generic;
using System.Linq;
using GuildMaster.Data;

namespace GuildMaster.ClaudeSandbox
{
    // Шаблоны лент: docs/content/event-feed.md один в один, с падежами {метка:р} и привязкой скобок рода @метка,
    // кроме утверждённых правок (помечены «Правка») и новых строк для систем, у которых строк не было.
    public static partial class GameDataGenerator
    {
        private const FeedImportance O = FeedImportance.Normal;
        private const FeedImportance Z = FeedImportance.Notable;
        private const FeedImportance V = FeedImportance.Important;

        private static void FillFeed(FeedTemplateSet feed, Dictionary<string, QuestTypeDefinition> q)
        {
            var t = new List<FeedTemplate>();

            // ===== Лента задания =====

            t.Add(Quest("quest.departed", O, If(FeedConditionKind.Group),
                "{группа} вышла из ворот на рассвете",
                "{группа} ушла в сторону {место:р}. Провожать никто не вышел",
                "{имя} последн[им|ей] затянул[|а] ремни, и группа тронулась"));
            t.Add(Quest("quest.departed", O, If(FeedConditionKind.Solo),
                "{имя} уш[ёл|ла] од[ин|на]. Сказал[|а]@имя, что так быстрее"));

            t.Add(Quest("quest.travel", O, None,
                "К вечеру {группа} добралась до {место:р}. Разбили лагерь",
                "Шли весь день. Молчали",
                "Дорогу развезло после дождя. Идут медленно",
                "{имя} наш[ёл|ла] брод. Сэкономили полдня"));
            t.Add(Quest("quest.travel", O, If(FeedConditionKind.Far),
                "Третий день пути. Припасы тают"));

            t.Add(Quest("quest.camp", O, None,
                "Ночь прошла тихо",
                "Ночью кто-то ходил вокруг лагеря. Утром следов не нашли",
                "{имя} не спал[|а] до рассвета — сторожил[|а]"));

            t.Add(Quest("quest.event.ambush.start", Z, None,
                "На дороге у {место:р} ждали. Разбойники вышли из-за деревьев",
                "Стрела ударила в дерево рядом с {имя:т}. Засада"));
            t.Add(Quest("quest.event.ambush.success", O, None,
                "Отбились. Разбойники ушли в лес, унося своих",
                "Двое остались лежать на дороге. Остальные бежали"));
            t.Add(Quest("quest.event.ambush.fail", Z, None,
                "{имя} ранен[|а] в засаде. Дальше идти не может — повернул[|а]@имя назад",
                "После засады {имя} отстал[|а] от группы. До места дошли без [него|неё]@имя"));

            t.Add(Quest("quest.event.beasts.start", Z, None,
                "Из кустов вылетела стая. Волки, голодные",
                "{враг} напал[|а|о|и]@враг на привале"));
            t.Add(Quest("quest.event.beasts.success", O, None,
                "Звери отступили, оставив двоих"));
            t.Add(Quest("quest.event.beasts.fail", Z, None,
                "{имя} искусан[|а]. Дальше [его|её]@имя несут"));

            t.Add(Quest("quest.discovery.found", Z, None,
                "{имя} заметил[|а] вход в пещеру у скалы. Оттуда тянет холодом",
                "В овраге — тёмный лаз. Кто-то рыл его давно"));
            t.Add(Quest("quest.discovery.explore", Z, None, "{решающий} решил[|а] заглянуть внутрь. Факелы зажгли у входа"));
            t.Add(Quest("quest.discovery.skip", O, None, "Отметили место на карте. Сначала — дело"));
            t.Add(Quest("quest.discovery.empty", Z, None, "Пещера пуста. Только старые кости и сырость"));
            t.Add(Quest("quest.discovery.loot", Z, None, "В глубине нашли тайник. Кто-то не вернулся за своим"));
            t.Add(Quest("quest.discovery.fail", V, None, "Из темноты вышло то, чего не ждали. Выбрались не все"));
            t.Add(Quest("quest.discovery.reported", O, None, "{имя} рассказал[|а] Регистратору о пещере. Получил[|а]@имя несколько монет"));

            // Раунды: ключи по типам задания (QuestTypeDefinition). «Общие» строки провала — в каждом типе.
            string[] commonFail = { "Не вышло. Попробуют ещё раз", "Всё пошло не так с самого начала" };

            t.Add(Quest("quest.round.success.hunt", O, None,
                "{имя} перв[ым|ой] заметил[|а] след. К полудню зверь был найден",
                "Выследили у водопоя. Одна стрела — и всё",
                "Зверь оказался старым и больным. Добили быстро"));
            t.Add(Quest("quest.round.success.extermination", O, None,
                "Логово выжгли. Никто не ушёл",
                "Короткая схватка. {враг} повержен[|а|о|ы]@враг",
                "К закату в логове стало тихо"));
            t.Add(Quest("quest.round.success.escort", O, None,
                "{заказчик} добрал[ся|ась|ось|ись]@заказчик до места цел[ым|ой|ым|ыми]@заказчик. Расплатил[ся|ась|ось|ись]@заказчик не торгуясь",
                "Обоз прошёл перевал без потерь",
                "Спокойный участок. {заказчик} даже шутил[|а|о|и]@заказчик"));
            t.Add(Quest("quest.round.success.delivery", O, None,
                "Груз доставлен. Заказчик пересчитал мешки и кивнул",
                "{имя} отдал[|а] письмо в руки. Ответа ждать не стали",
                "Успели до срока"));

            t.Add(Quest("quest.round.fail.hunt", Z, None, new[]
            {
                "Зверь ушёл по ручью. След потерян, придётся искать снова",
                "Весь день ходили кругами. Зверь где-то рядом, но не показывается",
            }.Concat(commonFail).ToArray()));
            t.Add(Quest("quest.round.fail.extermination", Z, None, new[]
            {
                "{враг} оказал[ся|ась|ось|ись]@враг сильнее, чем говорили. Отступили к деревьям",
                "Логово больше, чем думали. Пришлось отойти",
            }.Concat(commonFail).ToArray()));
            t.Add(Quest("quest.round.fail.escort", Z, None, new[]
            {
                "Обоз застрял. Ось сломалась, чинили до ночи",
                "На обоз напали. Отбились, но одна телега осталась на дороге",
            }.Concat(commonFail).ToArray()));
            t.Add(Quest("quest.round.fail.delivery", Z, None, new[]
            {
                "Мост снесло. Пришлось искать обход",
                "Часть груза промокла при переправе",
            }.Concat(commonFail).ToArray()));

            t.Add(Quest("quest.loss.time", O, None, "Потеряли день"));
            t.Add(Quest("quest.loss.stress", O, None, "У костра почти не говорили"));
            t.Add(Quest("quest.loss.bonus", Z, None, "О премии можно забыть"));
            // Правка: «тетива / щит / клинок» — три варианта одной строки.
            t.Add(Quest("quest.loss.gear", Z, None,
                "У {имя:р} лопнула тетива. Чинить за свой счёт",
                "У {имя:р} треснул щит. Чинить за свой счёт",
                "У {имя:р} сломался клинок. Чинить за свой счёт"));
            t.Add(Quest("quest.loss.loot", Z, None, "Добычу пришлось бросить, чтобы уйти"));

            t.Add(Quest("quest.wound.shield", Z, None,
                "{щит} принял[|а] удар на щит. Устоял[|а]@щит",
                "{щит} встал[|а] впереди. Досталось [ему|ей]@щит"));
            t.Add(Quest("quest.wound.light", Z, None,
                "{имя} получил[|а] удар по руке. Кость цела",
                "У {имя:р} рассечена бровь. Кровь заливает глаз, но [он|она]@имя держится"));
            t.Add(Quest("quest.wound.heavy", V, None,
                "{имя} тяжело ранен[|а]. {напарник} тащит [его|её]@имя на себе",
                "{имя} не может встать. Рана глубокая"));
            t.Add(Quest("quest.wound.maimed", V, None,
                "{лекарь} сделал[|а] что мог[|ла]. {имя} выживет, но нога больше не будет прежней"));

            t.Add(Quest("quest.decision.continue", Z, None, "{решающий} сказал[|а]: идём дальше. Никто не спорил"));
            t.Add(Quest("quest.decision.doubt", Z, None, "{имя} предложил[|а] повернуть назад. Остальные промолчали. Идут дальше"));
            t.Add(Quest("quest.decision.retreat", Z, None,
                "Решили отступить. Никто не спорил",
                "{решающий} посмотрел[|а] на раненых и повернул[|а] назад"));
            t.Add(Quest("quest.decision.argue", Z, None, "{имя} рвал[ся|ась] вперёд, {напарник} — назад. Решил[|а] {решающий}"));

            t.Add(Quest("quest.tension.panic", Z, None,
                "У {имя:р} дрожали руки. Стрела ушла мимо",
                "{имя} замер[|ла] и не двигал[ся|ась], пока всё не кончилось"));
            t.Add(Quest("quest.tension.flee", V, None,
                "{имя} бросил[|а] оружие и побежал[|а]. [Его|Её]@имя не стали догонять",
                "Когда {враг} выш[ел|ла|ло|ли]@враг из темноты, {имя:р} уже не было рядом"));
            t.Add(Quest("quest.tension.rush", Z, None,
                "{имя} кинул[ся|ась] на {враг:в} од[ин|на], не дожидаясь остальных",
                "{имя} не стал[|а] ждать сигнала"));
            t.Add(Quest("quest.tension.hero", V, None,
                "{имя} закрыл[|а] собой {напарник:в}",
                "{имя} вернул[ся|ась] за раненым под ударами"));
            t.Add(Quest("quest.tension.hold", O, None,
                "{имя} даже не поднял[|а] голоса. Просто делал[|а]@имя своё",
                "Когда все дрогнули, {имя} остал[ся|ась] на месте"));
            t.Add(Quest("quest.tension.breakdown", V, None,
                "{имя} сел[|а] на землю и не смог[|ла] подняться. Не от раны",
                "{имя} закричал[|а] на всех и уш[ёл|ла] в сторону. Вернул[ся|ась]@имя через час, молча"));

            t.Add(Quest("quest.ceiling", Z, None, "Слишком много людей для тихой дороги. Их заметили издалека"));

            t.Add(Quest("quest.synergy.friends", O, None, "{имя} и {напарник} работали молча — понимали друг друга без слов"));
            t.Add(Quest("quest.synergy.lovers", O, None, "{имя} всё время держал[ся|ась] рядом с {напарник:т}"));
            t.Add(Quest("quest.synergy.rivals", Z, None, "{имя} и {напарник} опять не поделили, кто пойдёт первым. Потеряли время"));

            t.Add(Quest("quest.death.one", V, None,
                "{имя} погиб[|ла] у {место:р}. Тело забрали с собой",
                "{имя} погиб[|ла] у {место:р}. Тело пришлось оставить"));
            t.Add(Quest("quest.death.medicSaved", V, None, "{лекарь} остановил[|а] кровь. {имя} выживет, но рука больше не поднимется"));
            t.Add(Quest("quest.death.all", V, None, "{группа} не вернулась. Последний раз их видели у {место:р}"));
            t.Add(Quest("quest.death.onlyFugitive", V, None, "Из {группа:р} вернул[ся|ась] только {имя}. [Он|Она]@имя ничего не рассказывает"));

            t.Add(Quest("quest.returnTrip.hard", Z, None,
                "Возвращаются. Медленно — несут раненых",
                "На обратном пути {имя:д} стало хуже"));
            t.Add(Quest("quest.returnTrip", O, None, "К ночи вышли на знакомую дорогу"));

            t.Add(Quest("quest.returned.brilliant", Z, None, "{группа} вернулась с победой и без царапины. В таверне их встретили стоя"));
            t.Add(Quest("quest.returned.success", O, None, "{группа} вернулась. Задание выполнено"));
            t.Add(Quest("quest.returned.partial", Z, None, "Задание выполнено, но премии не будет. {имя} хромает"));
            t.Add(Quest("quest.returned.fail", Z, None, "{группа} вернулась ни с чем"));
            // Правка: [числа ушедших] → {всего}.
            t.Add(Quest("quest.returned.catastrophe", V, None, "Вернулись {число} из {всего}. Задание провалено"));

            t.Add(Quest("quest.loot.handedIn", O, None, "Добычу сдали Скупщику"));
            t.Add(Quest("quest.loot.skimmed", O, None, "{имя} что-то долго перекладывал[|а] в своём мешке"));

            // ===== Лента гильдии =====

            t.Add(Guild("guild.board.newOrders", O, None, "На доске {число} новых заказов"));
            t.Add(Guild("guild.board.accepted", O, IfType(q["Hunt"]), "Регистратор принял заказ на охоту у {место:р}"));
            t.Add(Guild("guild.board.awaitingPlayer", Z, None, "Регистратор отложил один заказ для вас: слишком опасный или слишком дорогой"));
            t.Add(Guild("guild.board.expired", O, IfType(q["Delivery"]), "Заказ на доставку сняли — никто не взял"));

            t.Add(Guild("guild.order.taken", O, IfType(q["Hunt"]), "{имя} взял[|а] охоту у {место:р}"));
            t.Add(Guild("guild.party.formed", O, None, "{имя} собрал[|а] группу: {напарник} и ещё двое"));
            t.Add(Guild("guild.party.noPartners", O, None, "{имя} искал[|а] напарников весь день. Никто не согласился"));
            t.Add(Guild("guild.order.solo", O, None, "{имя} пош[ёл|ла] на задание од[ин|на]"));

            t.Add(Guild("guild.day.training", O, None, "{имя} весь день на тренировочном дворе"));
            t.Add(Guild("guild.day.resting", O, None, "{имя} отсыпается после задания"));
            t.Add(Guild("guild.day.tavern", O, None, "В таверне шумно. {имя} угощает"));

            t.Add(Guild("guild.relation.quarrel", Z, None,
                "{имя} и {напарник} поспорили в таверне. Разошлись молча",
                "Драка во дворе. {имя} и {напарник} — оба с синяками"));
            t.Add(Guild("guild.relation.friendship", O, None, "{имя} и {напарник} теперь ходят вместе"));
            t.Add(Guild("guild.relation.lovers", Z, None, "{имя} и {напарник} всё чаще сидят за одним столом"));
            t.Add(Guild("guild.relation.rivalry", Z, None, "{имя} не может спокойно смотреть, как хвалят {напарник:в}"));

            t.Add(Guild("guild.health.recovered", O, None, "{имя} поправил[ся|ась] и вернул[ся|ась] в строй"));
            // Правка: Лазарета и Лекаря нет — строка без Лекаря («Лекарь не отходит» → «Лежит в жару»).
            t.Add(Guild("guild.health.complication", Z, None, "Рана {имя:р} воспалилась. Лежит в жару"));
            t.Add(Guild("guild.health.infirmaryFull", Z, None, "В лазарете нет мест"));

            t.Add(Guild("guild.stress.binge", Z, None, "{имя} третий день не выходит из таверны"));
            t.Add(Guild("guild.stress.nightmares", Z, None, "{имя} плохо спит. Кричит по ночам"));
            t.Add(Guild("guild.stress.breakdown", V, None, "{имя} разбил[|а] кружку о стену и уш[ёл|ла], не сказав ни слова"));

            t.Add(Guild("guild.money.walletEmpty", Z, None,
                "У {имя:р} кончились деньги. Спит в общей зале",
                "{имя} продал[|а] свой второй клинок"));
            t.Add(Guild("guild.salary.paid", O, None, "Жалованье выплачено"));
            t.Add(Guild("guild.salary.unpaid", V, None, "Жалованье не выплачено. Персонал недоволен"));
            t.Add(Guild("guild.bankruptcy.started", V, None, "Казна пуста уже месяц. Начинается банкротство"));

            // Правка: «пришла новичок» — в женском роде «новенькая».
            t.Add(Guild("guild.adventurer.joined", O, None, "В гильдию приш[ёл новичок|ла новенькая] — {имя}"));
            t.Add(Guild("guild.adventurer.leaving", Z, None, "{имя} собрал[|а] вещи. Говорит, хватит"));
            // Правка: причина ухода видна игроку — {причина} (тексты причин ниже).
            t.Add(Guild("guild.adventurer.left", V, None, "{имя} уш[ёл|ла] из гильдии: {причина}"));

            // ===== Причины ухода (TechJob/08-decision-model.md → «Причины»): не строки ленты, а текст для {причина} =====
            // Шаблон с условием «черта раскрыта» называет черту; без него — текст без черты.

            t.Add(Guild("reason.lowLoyalty", O, None, "ничто [его|её]@имя здесь не держит"));
            t.Add(Guild("reason.emptyWallet", O, None, "пустой кошелёк"));
            t.Add(Guild("reason.lowPay", O, None, "мало платят для [его|её]@имя ранга"));
            t.Add(Guild("reason.lowPay", O, IfPole(AxisId.Money, AxisPole.Positive), "жадн[ый|ая]@имя — мало платят"));
            t.Add(Guild("reason.glory", O, None, "здесь не вырасти"));
            t.Add(Guild("reason.glory", O, IfPole(AxisId.Work, AxisPole.Positive), "амбициозн[ый|ая]@имя — здесь не вырасти"));
            t.Add(Guild("reason.danger", O, None, "слишком опасно, по [его|её]@имя мнению"));
            t.Add(Guild("reason.danger", O, IfPole(AxisId.Risk, AxisPole.Negative), "[трус|трусиха]@имя — не пойдёт на такое"));
            t.Add(Guild("reason.wounded", O, None, "рана ещё не зажила"));
            t.Add(Guild("reason.companions", O, None, "не с кем здесь быть"));
            t.Add(Guild("reason.companions", O, IfPole(AxisId.People, AxisPole.Positive), "командн[ый|ая]@имя — не с кем здесь быть"));
            t.Add(Guild("reason.tired", O, None, "устал[|а]@имя"));
            t.Add(Guild("reason.hardAfterWound", O, None, "тяжело после ранения"));
            t.Add(Guild("reason.hardAfterBreakdown", O, None, "тяжело после срыва"));
            t.Add(Guild("reason.heavyHeart", O, None, "тяжело на душе"));

            t.Add(Guild("guild.decree.enabled", O, None, "Объявлено: {распоряжение}"));
            t.Add(Guild("guild.decree.benefitCancelled", Z, None, "Новость об отмене {распоряжение:р} встретили молча"));
            t.Add(Guild("guild.decree.prohibition", Z, None, "Трактирщик убрал бочки в погреб. {имя} смотрел[|а] на это долго"));

            t.Add(Guild("guild.building.started", O, None, "Начали строить {постройка:в}"));
            // Правка: род постройки из данных — «Общежитие готово».
            t.Add(Guild("guild.building.ready", Z, None, "{постройка} готов[|а|о|ы]@постройка"));

            // Правка: «…» → {доход}, {расход}.
            t.Add(Guild("guild.month.summary", Z, None, "Прошёл месяц. Заработано {доход}, потрачено {расход}, погибших — {число}"));

            // ===== Новое: строки для систем без строк в event-feed.md, тексты — предложение =====

            t.Add(Guild("guild.candidate.arrived", O, None, "{имя} просится в гильдию. Ждёт ответа у ворот"));
            t.Add(Guild("guild.candidate.left", O, None, "{имя} не дождал[ся|ась] ответа и уш[ёл|ла]"));
            t.Add(Guild("guild.staff.hired", O, None, "{имя} принят[|а] на службу гильдии"));
            t.Add(Guild("guild.staff.quit", V, None, "{имя} уш[ёл|ла] со службы, так и не дождавшись жалованья"));
            t.Add(Guild("guild.staff.fired", Z, None, "{имя} больше не служит гильдии"));
            t.Add(Guild("guild.building.queued", O, None, "{постройка} — в очереди на стройку"));
            t.Add(Guild("guild.eventQuest.found", Z, None, "Регистратор записал находку у {место:р}. Какой ранг назначить — решать вам"));
            t.Add(Guild("guild.dilemma.arrived", Z, None, "{имя} просит о разговоре"));
            t.Add(Guild("guild.archetype.changed", O, None, "{имя} теперь — {архетип}"));
            t.Add(Guild("guild.party.permanentFormed", Z, None, "{имя} и {напарник} с товарищами теперь ходят вместе: {группа}"));
            t.Add(Guild("guild.party.permanentDisbanded", Z, None, "{группа} распалась"));
            t.Add(Guild("guild.decree.expired", O, None, "Срок вышел: {распоряжение} больше не действует"));
            t.Add(Guild("guild.bankruptcy.lifted", Z, None, "Казна снова в плюсе. Из долговой ямы выбрались"));

            // Срыв: своя строка на каждый вид срыва, все [В]. Драка без противника рядом — строка «Стресс и срывы» из event-feed.md.
            t.Add(Guild("guild.breakdown.binge", V, None, "{имя} запил[|а]. Из таверны не выходит"));
            t.Add(Guild("guild.breakdown.brawl", V, None, "{имя} полез[|ла] в драку с {напарник:т}. Разнимали втроём"));
            t.Add(Guild("guild.breakdown.brawlAlone", V, None, "{имя} разбил[|а] кружку о стену и уш[ёл|ла], не сказав ни слова"));
            t.Add(Guild("guild.breakdown.refuse", V, None, "{имя} больше не берёт заданий. На вопросы не отвечает"));
            t.Add(Guild("guild.breakdown.collapse", V, None, "{имя} сел[|а] и не смог[|ла] подняться. Не от раны"));

            t.Add(Guild("dilemma.loan.full", O, None, "{имя} получил[|а] в долг {сумма}"));
            t.Add(Guild("dilemma.loan.half", O, None, "{имя} получил[|а] половину того, что просил[|а]"));
            t.Add(Guild("dilemma.loan.refuse", O, None, "{имя} уш[ёл|ла] ни с чем"));
            t.Add(Guild("dilemma.deserter.forgive", Z, None, "{имя} прощ[ён|ена] и остаётся в гильдии. Не все этому рады"));
            t.Add(Guild("dilemma.deserter.fine", Z, None, "{имя} заплатил[|а] штраф и остаётся в гильдии"));
            t.Add(Guild("dilemma.deserter.expel", Z, None, "{имя} уш[ёл|ла] из гильдии. Никто не провожал"));
            t.Add(Guild("dilemma.loot.subject", O, None, "Спор о добыче решён в пользу {имя:р}. {напарник} промолчал[|а]"));
            t.Add(Guild("dilemma.loot.partner", O, None, "Спор о добыче решён в пользу {напарник:р}. {имя} промолчал[|а]"));
            t.Add(Guild("dilemma.loot.split", O, None, "Добычу поделили поровну. Гильдия доплатила"));
            t.Add(Guild("dilemma.loot.timeout", Z, None, "{имя} и {напарник} так и не договорились. Теперь не разговаривают"));
            t.Add(Guild("dilemma.wounded.allow", O, None, "{имя} снова берёт задания, хоть рана и не зажила"));
            t.Add(Guild("dilemma.wounded.forbid", O, None, "{имя} остаётся лечиться и не скрывает недовольства"));
            t.Add(Guild("dilemma.wounded.pay", O, None, "Гильдия оплатила лечение {имя:р} и дала денег на неделю"));
            t.Add(Guild("dilemma.lovers.allow", O, None, "{имя} и {напарник} теперь ходят в одной группе"));
            t.Add(Guild("dilemma.lovers.forbid", Z, None, "{имя} и {напарник} больше не ходят вместе. Так решила гильдия"));
            t.Add(Guild("dilemma.feast.full", Z, None, "В таверне праздник. Пили до утра"));
            t.Add(Guild("dilemma.feast.modest", O, None, "Вечером в таверне налили всем за счёт гильдии"));
            t.Add(Guild("dilemma.feast.refuse", O, None, "Праздника не будет"));

            // ===== Раскрытие черт: всегда [В] =====

            t.Add(Reveal("reveal.risk.negative", "Теперь ясно: {имя} не выдерживает, когда становится по-настоящему страшно"));
            t.Add(Reveal("reveal.risk.positive", "{имя} не думает о риске. Это уже не смелость"));
            t.Add(Reveal("reveal.money.negative", "{имя} снова взял[|а] задание почти даром. Деньги [его|её]@имя, похоже, не волнуют"));
            t.Add(Reveal("reveal.money.positive", "{имя} считает каждую монету. И чужие тоже"));
            t.Add(Reveal("reveal.people.negative", "{имя} лучше всего, когда рядом никого"));
            t.Add(Reveal("reveal.people.positive", "Без своих {имя} теряется"));
            t.Add(Reveal("reveal.work.negative", "{имя} не торопится ни на задания, ни на двор"));
            t.Add(Reveal("reveal.work.positive", "{имя} хочет большего. И не скрывает"));
            t.Add(Reveal("reveal.loyalty.negative", "{имя} останется, пока платят. Не дольше"));
            t.Add(Reveal("reveal.loyalty.positive", "Все ушли. {имя} остал[ся|ась]"));
            t.Add(Reveal("reveal.principles.negative", "У {имя:р} в мешке нашлось то, чего не было в отчёте"));
            t.Add(Reveal("reveal.principles.positive", "{имя} отказал[ся|ась] брать это дело. Сказал[|а]@имя — грязное"));

            // Нейтральная ось — «уравновешен»: [З], без автопаузы. Ключ — balancedFeedKey оси.
            t.Add(Guild("reveal.risk.balanced", Z, None, "{имя} не лезет на рожон, но и за спины не прячется"));
            t.Add(Guild("reveal.money.balanced", Z, None, "{имя} считает деньги, но не больше других"));
            t.Add(Guild("reveal.people.balanced", Z, None, "{имя} ладит с людьми, но и без них не пропадёт"));
            t.Add(Guild("reveal.work.balanced", Z, None, "{имя} работает как все — не больше и не меньше"));
            t.Add(Guild("reveal.loyalty.balanced", Z, None, "{имя} держится за гильдию, пока гильдия держится за [него|неё]"));
            t.Add(Guild("reveal.principles.balanced", Z, None, "{имя} не свят[ой|ая], но и не вор[|овка]"));

            t.Add(Reveal("reveal.trait.veteran", "{имя} держится так, будто уже видел[|а] и не такое. Видимо, видел[|а]@имя"));
            t.Add(Reveal("reveal.trait.deserter", "{имя} уже бежал[|а] однажды. Это было задолго до гильдии"));
            t.Add(Reveal("reveal.trait.drunkard", "{имя} не вы[шел|шла] на задание. Нашли в таверне"));
            t.Add(Reveal("reveal.trait.braggart", "{имя} обещал[|а], что справится. Не справил[ся|ась]@имя"));
            t.Add(Reveal("reveal.trait.ironNerves", "Все дрогнули. {имя} — нет"));
            t.Add(Reveal("reveal.trait.family", "{имя} просит не посылать [его|её] туда, откуда не возвращаются. Дома ждут"));
            t.Add(Reveal("reveal.trait.rival", "{имя} и {напарник} — это уже не спор, а соперничество"));
            t.Add(Reveal("reveal.trait.lover", "{имя} просит ставить [его|её] в одну группу с {напарник:т}"));
            t.Add(Reveal("reveal.trait.nightmares", "{имя} отказал[ся|ась] от задания. Похожего на то, после которого не спит"));
            // Правка: строка Калеки — по варианту на снижаемую характеристику.
            t.Add(Reveal("reveal.trait.maimed", IfStat(StatId.Strength), "{имя} больше не сможет держать щит как раньше"));
            t.Add(Reveal("reveal.trait.maimed", IfStat(StatId.Endurance), "{имя} больше не выдержит долгий переход, как раньше"));
            t.Add(Reveal("reveal.trait.maimed", IfStat(StatId.Agility), "{имя} больше не сможет бегать как раньше"));
            t.Add(Reveal("reveal.trait.maimed", IfStat(StatId.Reaction), "{имя} больше не успеет увернуться, как раньше"));
            t.Add(Reveal("reveal.trait.grieving", "После гибели {напарник:р} {имя} друг[ой|ая]@имя"));
            t.Add(Reveal("reveal.trait.tested", "Когда стало тяжело, {имя} остал[ся|ась]. На таких держится гильдия"));

            Set(feed, "templates", t);
        }

        private static readonly FeedCondition[] None = new FeedCondition[0];

        private static FeedCondition[] If(FeedConditionKind kind) => new[] { Make<FeedCondition>(("kind", kind)) };

        private static FeedCondition[] IfType(QuestTypeDefinition questType) =>
            new[] { Make<FeedCondition>(("kind", FeedConditionKind.QuestType), ("questType", questType)) };

        private static FeedCondition[] IfPole(AxisId axis, AxisPole pole) =>
            new[] { Make<FeedCondition>(("kind", FeedConditionKind.RevealedAxisPole), ("axis", axis), ("pole", pole)) };

        private static FeedCondition[] IfStat(StatId stat) =>
            new[] { Make<FeedCondition>(("kind", FeedConditionKind.MaimedStat), ("stat", stat)) };

        private static FeedTemplate Quest(string key, FeedImportance importance, FeedCondition[] conditions, params string[] variants) =>
            Template(key, FeedKind.Quest, importance, conditions, variants);

        private static FeedTemplate Guild(string key, FeedImportance importance, FeedCondition[] conditions, params string[] variants) =>
            Template(key, FeedKind.Guild, importance, conditions, variants);

        private static FeedTemplate Reveal(string key, params string[] variants) =>
            Template(key, FeedKind.Guild, V, None, variants);

        private static FeedTemplate Reveal(string key, FeedCondition[] conditions, params string[] variants) =>
            Template(key, FeedKind.Guild, V, conditions, variants);

        private static FeedTemplate Template(string key, FeedKind feed, FeedImportance importance, FeedCondition[] conditions, string[] variants) =>
            Make<FeedTemplate>(("key", key), ("feed", feed), ("importance", importance),
                ("conditions", conditions.ToList()), ("variants", variants.ToList()));
    }
}
