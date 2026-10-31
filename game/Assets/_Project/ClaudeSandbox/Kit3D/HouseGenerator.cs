using System.Collections.Generic;
using UnityEngine;

namespace GuildMaster.ClaudeSandbox.Kit3D
{
    /// <summary>Стадия дома: какие модули кита идут в сборку.</summary>
    public enum HouseStage
    {
        /// <summary>Заложен: колышки и верёвка по контуру.</summary>
        Planned,

        /// <summary>Строится: цоколь, каркас нижнего этажа, часть стен, леса.</summary>
        Construction,

        /// <summary>Готов.</summary>
        Ready,

        /// <summary>Заброшен: дыры в стенах и крыше, заколоченные окна, подпорки.</summary>
        Abandoned,
    }

    /// <summary>Из чего первый этаж готового дома.</summary>
    public enum GroundFloor
    {
        /// <summary>Фахверк, как верхние этажи.</summary>
        Timber,

        /// <summary>Штукатурка с угловыми камнями, арочная дверь и окна в каменном обрамлении.</summary>
        Plaster,

        /// <summary>Каменная кладка с угловыми камнями, проёмы — как у штукатурки.</summary>
        Stone,
    }

    /// <summary>
    /// Собирает дом из модулей кита по параметрам и сиду: один сид — всегда один и тот же дом. Меняешь поле в инспекторе —
    /// дом пересобирается. Сборка не сохраняется в сцену: дом строится заново при включении.
    /// Расчёт идёт в осях кита (как в Blender: X — вдоль конька, Y — вглубь дома, Z — вверх, лицевая сторона смотрит на −Y)
    /// и переводится в оси Unity одной функцией.
    /// </summary>
    [ExecuteAlways]
    public sealed class HouseGenerator : MonoBehaviour
    {
        private const float FloorHeight = 2.6f;
        private const float PlinthHeight = 0.35f;
        private const float GableOverhang = 0.3f;

        /// <summary>Пол этажа: насколько он заходит внутрь стены от её наружной грани (стена — 0,2 м) и его толщина.</summary>
        private const float FloorInset = 0.1f;
        private const float FloorThickness = 0.08f;

        /// <summary>Пол чуть выше верха цоколя и нижнего бруса стены: иначе грани совпадают и мерцают.</summary>
        private const float FloorLift = 0.01f;

        /// <summary>Насколько верхний этаж выступает над нижним спереди и сзади: вместе — ровно клетка глубины.</summary>
        private const float Jetty = 0.5f;

        /// <summary>Кронштейн у угла стоит на угловом столбе — на столько метров внутрь от угла.</summary>
        private const float CornerBracket = 0.1f;

        /// <summary>
        /// Дверь (0,9 м в свету) шире промежутка между столбами клетки, поэтому занимает две клетки — door и door + 1: модуль стоит
        /// на границе между ними, столба там нет.
        /// </summary>
        private const int DoorCells = 2;

        /// <summary>Ширина пристройки в клетках: средняя клетка для двери на задней стене есть всегда.</summary>
        private const int WingWidth = 3;
        private const string GeneratedName = "Generated";
        private static readonly string[] RoofVariants = { "", "_b", "_c" };

        /// <summary>Повороты сторон дома: перед, зад, левый и правый торцы.</summary>
        private static readonly float[] SideRotations = { 0, 180, -90, 90 };

        // Как FBX из Blender ложится в Unity: X кита → −X Unity, Y кита (вглубь) → −Z Unity, Z кита (вверх) → Y Unity.
        // Лицевая сторона модуля (−Y кита) смотрит в Unity на +Z.
        private const float SignX = -1f;
        private const float SignY = -1f;

        [SerializeField] private HouseKit kit;
        [SerializeField, Range(2, 12)] private int width = 6;
        [SerializeField, Range(3, 8)] private int depth = 6;
        [SerializeField, Range(1, 3)] private int floors = 1;
        [SerializeField] private int seed = 1;
        [SerializeField] private HouseStage stage = HouseStage.Ready;
        [SerializeField, Range(0f, 1f)] private float decay = 0.7f;

        [Tooltip("Возраст дома: с годами черепица зарастает мхом и травой (0 — новая, 1 — пятна мха наибольшие)")]
        [SerializeField, Range(0f, 1f)] private float age = 0.3f;

        [Tooltip("Износ побелки: 0 — стена целая, 1 — побелка осыпалась до камня; у заброшенного дома износ полный всегда")]
        [SerializeField, Range(0f, 1f)] private float wear = 0.3f;

        [SerializeField, Range(0f, 1f)] private float chimneyChance = 0.8f;

        [Tooltip("Шанс соломенной крыши вместо сланцевой; у заброшенного дома крыша соломенная всегда")]
        [SerializeField, Range(0f, 1f)] private float thatchChance = 0.5f;

        [Tooltip("Шанс, что верхние этажи выступают над нижним (только у домов в 2–3 этажа глубиной до 7 клеток)")]
        [SerializeField, Range(0f, 1f)] private float jettyChance = 0.7f;

        [Tooltip("Шанс оштукатуренного первого этажа у готового дома")]
        [SerializeField, Range(0f, 1f)] private float plasterChance = 0.35f;

        [Tooltip("Шанс каменного первого этажа у готового дома")]
        [SerializeField, Range(0f, 1f)] private float stoneChance = 0.25f;

        [Tooltip("Шанс подвального окна в цоколе лицевой стороны (не под дверью)")]
        [SerializeField, Range(0f, 1f)] private float cellarChance = 0.4f;

        [Tooltip("Шанс слухового окна на клетке сланцевой крыши: не у торцов, не рядом с другим, не больше двух на дом, крыша глубиной от 4")]
        [SerializeField, Range(0f, 1f)] private float dormerChance = 0.35f;

        [Tooltip("Шанс пристройки на этаж ниже у одного из торцов, вровень с задней стеной (только у домов от двух этажей)")]
        [SerializeField, Range(0f, 1f)] private float wingChance = 0.4f;

        [Tooltip("Шанс навеса на столбах вдоль торца первого этажа (только у готового дома)")]
        [SerializeField, Range(0f, 1f)] private float leanToChance = 0.3f;

        [Tooltip("Шанс козырька над фахверковой дверью (у одноэтажного дома дверь под карнизом, у дома с выступом этажа — под ним)")]
        [SerializeField, Range(0f, 1f)] private float doorHoodChance = 0.5f;

        // Пристройку собирает такой же генератор на дочернем объекте. joint — торец, которым она примыкает к основному дому
        // (−90 — левый, 90 — правый; у самостоятельного дома 0), jointThatch и jointGround — крыша и первый этаж как у основного дома,
        // doorSide — сторона с дверью (поворот стороны; 0 — перед), дверь в её средней паре клеток; jointPlaster — побелка как у основного дома.
        [SerializeField, HideInInspector] private int joint;
        [SerializeField, HideInInspector] private bool jointThatch;
        [SerializeField, HideInInspector] private GroundFloor jointGround;
        [SerializeField, HideInInspector] private int doorSide;
        [SerializeField, HideInInspector] private int jointPlaster;

        private Transform root;
        private bool dirty;
        private bool thatch;
        private int plaster;
        private bool jetty;
        private float sag;
        private int textureShift;

        /// <summary>Пристройка другого дома: её пересобирает основной дом.</summary>
        public bool IsWing => joint != 0;

        public void Set(int width, int depth, int floors, int seed, HouseStage stage)
        {
            this.width = width;
            this.depth = depth;
            this.floors = floors;
            this.seed = seed;
            this.stage = stage;
            Rebuild();
        }

        private void OnEnable() => Rebuild();

        private void OnValidate() => dirty = true;

        private void Update()
        {
            if (!dirty) return;
            dirty = false;
            Rebuild();
        }

        public void Rebuild()
        {
            Clear();
            if (kit == null) return;
            root = new GameObject(GeneratedName).transform;
            root.gameObject.hideFlags = HideFlags.DontSaveInEditor | HideFlags.NotEditable;
            root.SetParent(transform, false);

            // Облик — крыша, выступ этажа, провис конька — своим потоком, чтобы не сдвигать броски стен и окон.
            var look = new System.Random(seed ^ 0x5F3759DF);
            thatch = stage == HouseStage.Abandoned || look.NextDouble() < thatchChance;
            if (IsWing) thatch = jointThatch;
            jetty = floors > 1 && depth <= 7 && look.NextDouble() < jettyChance;
            sag = stage == HouseStage.Abandoned ? 0.08f + 0.07f * (float)look.NextDouble() : 0.06f * (float)look.NextDouble();
            textureShift = look.Next(8);
            // Побелка — своим потоком: новый бросок не сдвигает облик домов, собранных раньше.
            plaster = IsWing ? jointPlaster : new System.Random(seed ^ 0x510E527F).Next(Mathf.Max(1, kit.Plasters.Count));
            Build(new System.Random(seed));
            ApplyAge();
        }

        /// <summary>Сколько ступеней у старения материалов: на каждую — своя копия материала, общая для всех домов этой ступени.</summary>
        private const int AgeSteps = 20;

        private static readonly int GrowthId = Shader.PropertyToID("_Growth");
        private static readonly int WearId = Shader.PropertyToID("_Wear");

        private static readonly Dictionary<(Material, int, int), Material> AgedMaterials = new Dictionary<(Material, int, int), Material>();

        /// <summary>Все копии со старением: пристройка внутри дома уже получила свои, дом их не копирует второй раз.</summary>
        private static readonly HashSet<Material> AgedCopies = new HashSet<Material>();

        /// <summary>
        /// Старение дома на материалах, которые его умеют: черепица с числом _Growth зарастает мхом по возрасту, побелка с числом _Wear
        /// осыпается по износу (у заброшенного дома — полностью). Побелка сначала заменяется той, что выпала дому. Копии общие для всех
        /// домов одной ступени, поэтому дома и дальше рисуются пачкой; в сцену не сохраняются.
        /// </summary>
        private void ApplyAge()
        {
            var growthStep = Mathf.RoundToInt(age * AgeSteps);
            var wearStep = stage == HouseStage.Abandoned ? AgeSteps : Mathf.RoundToInt(wear * AgeSteps);
            var plasters = kit.Plasters;
            var housePlaster = plasters.Count > 0 ? plasters[plaster % plasters.Count] : null;
            var copies = new Dictionary<Material, Material>();
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var materials = renderer.sharedMaterials;
                var changed = false;
                for (var i = 0; i < materials.Length; i++)
                {
                    var source = materials[i];
                    if (source == null || AgedCopies.Contains(source)) continue;
                    if (!copies.TryGetValue(source, out var copy))
                    {
                        var own = housePlaster != null && kit.IsPlaster(source) ? housePlaster : source;
                        copy = own.HasProperty(GrowthId) ? AgedMaterial(own, GrowthId, growthStep)
                            : own.HasProperty(WearId) ? AgedMaterial(own, WearId, wearStep)
                            : own;
                        copies[source] = copy;
                    }

                    if (copy == source) continue;
                    materials[i] = copy;
                    changed = true;
                }

                if (changed) renderer.sharedMaterials = materials;
            }
        }

        /// <summary>
        /// Копия материала со старением (число property) на ступени step. Свойства берутся у исходного материала заново: его могли поменять.
        /// </summary>
        private static Material AgedMaterial(Material source, int property, int step)
        {
            var key = (source, property, step);
            if (!AgedMaterials.TryGetValue(key, out var copy) || copy == null)
            {
                var kind = property == GrowthId ? "growth" : "wear";
                copy = new Material(source) { name = $"{source.name} {kind}{step}", hideFlags = HideFlags.DontSave };
                AgedMaterials[key] = copy;
                AgedCopies.Add(copy);
            }

            copy.CopyPropertiesFromMaterial(source);
            copy.shaderKeywords = source.shaderKeywords;
            copy.SetFloat(property, (float)step / AgeSteps);
            return copy;
        }

        private void Clear()
        {
            for (var i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child.name != GeneratedName) continue;
                if (Application.isPlaying) Destroy(child.gameObject);
                else DestroyImmediate(child.gameObject);
            }

            root = null;
        }

        private void Build(System.Random rng)
        {
            var w = width;
            var d = depth;
            if (stage == HouseStage.Planned)
            {
                foreach (var c in Sides(w, 0, d)) Put("rope_1", c.X, c.Y, 0, c.Rotation);
                foreach (var b in Boundaries(w, 0, d)) Put("stake", b.X, b.Y, 0, 0);
                foreach (var c in Corners(w, 0, d)) Put("stake", c.X, c.Y, 0, 0);
                return;
            }

            var door = rng.Next(w);

            // Пристройка, навес и козырёк над дверью — своим потоком: броски стен и окон от них не сдвигаются.
            var mass = new System.Random(seed ^ 0x3C6EF372);
            var wingRoll = mass.NextDouble();
            var wingSide = mass.Next(2) == 0 ? -90 : 90;
            // Дверь пристройки — сзади или на внешнем торце: спереди она встала бы у стены основного дома.
            var wingDoor = mass.Next(2) == 0 ? 180 : wingSide < 0 ? 90 : -90;
            var wingDepth = Mathf.Max(3, d - 1 - mass.Next(2));
            var leanRoll = mass.NextDouble();
            var leanSide = mass.Next(2) == 0 ? -90 : 90;
            var doorHood = mass.NextDouble() < doorHoodChance;
            if (IsWing || floors < 2 || d < 4 || stage == HouseStage.Construction || wingRoll >= wingChance) wingSide = 0;
            if (IsWing || stage != HouseStage.Ready || leanRoll >= leanToChance) leanSide = 0;
            else if (leanSide == wingSide) leanSide = -leanSide;

            // Вид первого этажа и подвальное окно — своим потоком: броски стен и окон от них не сдвигаются.
            var detail = new System.Random(seed ^ 0x6A09E667);
            var roll = detail.NextDouble();
            var timberUpTo = 1 - plasterChance - stoneChance;
            // Дверь и окна — только в средних клетках: у угла на них наезжают угловые камни, а открытая ставня торчит за угол.
            // Дверь у края переезжает в случайную среднюю пару клеток; у дома уже 4 клеток средней пары нет — дверь в первых двух
            // клетках, а нижний этаж фахверковый.
            var ground = stage != HouseStage.Ready || w < 4 || roll < timberUpTo ? GroundFloor.Timber
                : roll < timberUpTo + plasterChance ? GroundFloor.Plaster : GroundFloor.Stone;
            if (w >= 4 && (door < 1 || door > w - 3)) door = 1 + detail.Next(w - 3);
            else if (w < 4) door = 0;
            if (IsWing) ground = jointGround;
            if (doorSide != 0) door = Mathf.Max(0, ((doorSide == 180 ? w : d) - DoorCells) / 2);
            // Вид окон на каждой стороне этажа — ещё одним потоком.
            var panes = new System.Random(seed ^ 0x1F83D9AB);
            var cellar = -1;
            if (w > DoorCells && detail.NextDouble() < cellarChance)
            {
                cellar = detail.Next(w - DoorCells);
                if (cellar >= door) cellar += DoorCells;
            }

            foreach (var c in Sides(w, 0, d))
            {
                if (JoinedSide(c)) continue;
                Put(c.Front && (int)c.X == cellar ? "plinth_cellar" : "plinth", c.X, c.Y, 0, c.Rotation);
            }

            foreach (var c in Corners(w, 0, d))
            {
                if (!JoinedCorner(c)) Put("plinth_corner", c.X, c.Y, 0, c.Rotation);
            }

            var decayLevel = stage == HouseStage.Abandoned ? decay : 0f;
            var builtFloors = stage == HouseStage.Construction ? 1 : floors;
            for (var f = 0; f < builtFloors; f++)
            {
                var z = PlinthHeight + f * FloorHeight;

                // Верхние этажи дома с выступом стоят на полклетки дальше спереди и сзади — на клетку глубже нижнего.
                var upper = f > 0 && jetty;
                var y0 = upper ? -Jetty : 0f;
                var fd = upper ? d + 1 : d;
                if (f == 1 && jetty)
                {
                    foreach (var c in Sides(w, 0, d))
                    {
                        if (c.Rotation == 0 || c.Rotation == 180) Put("joists_1", c.X, c.Y, z, c.Rotation);
                    }

                    // Кронштейны под выступом: на каждом столбе нижнего этажа и у углов, на угловом столбе. Посреди двери столба нет —
                    // нет и кронштейна.
                    foreach (var b in Boundaries(w, 0, d))
                    {
                        if ((b.Rotation == 0 || b.Rotation == 180) && !DoorMiddle(b, 0, door)) Put("jetty_bracket", b.X, b.Y, z, b.Rotation);
                    }

                    foreach (var y in new float[] { 0, d })
                    {
                        var rotation = y == 0 ? 0 : 180;
                        Put("jetty_bracket", CornerBracket, y, z, rotation);
                        Put("jetty_bracket", w - CornerBracket, y, z, rotation);
                    }
                }

                if (stage != HouseStage.Construction) PutFloor(w, y0, fd, z, f);

                // Каменный или оштукатуренный этаж — без каркаса: на углах угловые камни, столбов между клетками нет.
                var masonry = f == 0 && ground != GroundFloor.Timber;
                foreach (var c in Corners(w, y0, fd))
                {
                    if (!JoinedCorner(c)) Put(masonry ? "quoin" : "corner_post", c.X, c.Y, z, c.Rotation);
                }

                if (!masonry)
                {
                    foreach (var b in Boundaries(w, y0, fd))
                    {
                        var doorMiddle = f == 0 && stage != HouseStage.Construction && DoorMiddle(b, y0, door);
                        if (!JoinedSide(b) && !doorMiddle) Put("post", b.X, b.Y, z, b.Rotation);
                    }
                }

                // Одна сторона этажа — один вид окна. Два окна подряд не ставятся, кроме ленточных: те складываются в одну ленту.
                var kinds = new Dictionary<float, string>();
                foreach (var rotation in SideRotations)
                    kinds[rotation] = WindowModule(panes.NextDouble(), f, rotation == 0 || rotation == 180, masonry);
                var windowBefore = new Dictionary<float, bool>();
                foreach (var c in Sides(w, y0, fd))
                {
                    // Примыкающий торец пристройки не строится: его закрывает стена основного дома.
                    if (JoinedSide(c)) continue;
                    if (stage == HouseStage.Construction)
                    {
                        Put("beam_1", c.X, c.Y, z, c.Rotation);
                        if (rng.NextDouble() < 0.35) Put(Pick(rng, "wall_plain_a", "wall_plain_b"), c.X, c.Y, z, c.Rotation);
                        if (c.Front || c.Rotation > 0) Put("scaffold_1", c.X, c.Y, z, c.Rotation);
                        continue;
                    }

                    // Вторая клетка двери — часть модуля двери.
                    var onDoorSide = f == 0 && Mathf.Approximately(c.Rotation, doorSide);
                    if (onDoorSide && (int)Along(c, y0) == door + 1)
                    {
                        windowBefore[c.Rotation] = false;
                        continue;
                    }

                    string module;
                    var windowChance = (c.Front ? 0.35 : 0.2) + (f > 0 ? 0.05 : 0);
                    var isDoor = onDoorSide && (int)Along(c, y0) == door;
                    if (isDoor) module = "wall_door";
                    else if (rng.NextDouble() < windowChance)
                        module = rng.NextDouble() < decayLevel * 0.7 ? "wall_window_boarded" : kinds[c.Rotation];
                    else
                        module = rng.NextDouble() < decayLevel * 0.5 ? "wall_broken" : PlainWall(rng.Next(4), c, w, y0, fd);
                    // Окно у угла или сразу за другим — глухая стена; бросок окна сделан, поэтому броски следующих клеток не сдвигаются.
                    var crowded = windowBefore.TryGetValue(c.Rotation, out var before) && before && module != "wall_window_ribbon";
                    // Окно, которое закрыла бы пристройка, — тоже глухая стена.
                    var covered = c.Rotation == wingSide && f < floors - 1 && c.Y > d - wingDepth;
                    if (module.StartsWith("wall_window") && (AtCorner(c, w, y0, fd) || crowded || covered)) module = PlainWall(1, c, w, y0, fd);
                    windowBefore[c.Rotation] = module.StartsWith("wall_window");
                    if (masonry) module = MasonryModule(module, ground);
                    if (isDoor)
                    {
                        // Модуль двери стоит на границе своих двух клеток.
                        var x = c.Rotation == 0 || c.Rotation == 180 ? door + 1 : c.X;
                        var y = c.Rotation == 0 || c.Rotation == 180 ? c.Y : y0 + door + 1;
                        Put(module, x, y, z, c.Rotation);
                        Put("steps", x, y, 0, c.Rotation);
                        // Пыль у двери — без сдвига развёртки: её прозрачность идёт снизу вверх по развёртке модуля.
                        Put(masonry ? "door_grime_arch" : "door_grime", x, y, z, c.Rotation, false);
                        if (doorHood && !masonry && !jetty && floors > 1) Put("door_hood", x, y, z, c.Rotation);
                    }
                    else
                    {
                        Put(module, c.X, c.Y, z, c.Rotation);
                    }

                    if (f == 0 && rng.NextDouble() < decayLevel * 0.3) Put("prop", c.X, c.Y, 0, c.Rotation);
                }
            }

            if (stage == HouseStage.Construction) return;

            if (leanSide != 0) BuildLeanTo(leanSide);
            if (wingSide != 0) BuildWing(wingSide, wingDepth, wingDoor, ground);

            var top = PlinthHeight + floors * FloorHeight;
            var rd = jetty ? d + 1 : d;
            var ry = (jetty ? -Jetty : 0f) + rd / 2f;
            var cover = thatch ? "thatch" : "roof";
            // Пол чердака — над верхним этажом, по его размеру; снизу он же — потолок верхнего этажа.
            PutFloor(w, ry - rd / 2f, rd, top, floors);
            if (joint != -90) Put($"gable_d{rd}", 0, ry, top, -90);
            if (joint != 90) Put($"gable_d{rd}", w, ry, top, 90);

            // Вариант клетки сланца и слуховые окна — своим потоком: броски стен и дыр в крыше от них не сдвигаются.
            var tiles = new System.Random(seed ^ 0x2545F491);
            var dormers = 0;
            var lastDormer = -2;
            for (var i = 0; i < w; i++)
            {
                var hole = rng.NextDouble() < decayLevel * 0.4;
                if (thatch || hole)
                {
                    PutRoof(hole ? $"{cover}_hole_d{rd}" : $"{cover}_d{rd}", i + 0.5f, ry, top, 0);
                    continue;
                }

                // Плитки клетки одинаковы у всех клеток одного варианта — три варианта, чтобы рисунок не повторялся каждый метр.
                PutRoof($"roof_d{rd}{RoofVariants[tiles.Next(RoofVariants.Length)]}", i + 0.5f, ry, top, 0);
                if (rd < 4 || i == 0 || i == w - 1 || i <= lastDormer + 1 || dormers >= 2 || tiles.NextDouble() >= dormerChance) continue;

                dormers++;
                lastDormer = i;
                // Слуховое окно — под уклон крыши этой глубины.
                var dormer = (tiles.NextDouble() < 0.5 ? "dormer_big" : "dormer_small") + $"_d{rd}";
                var t = (i + 0.5f) / w;
                Put(dormer, i + 0.5f, ry - rd / 2f, top - sag * 4 * t * (1 - t), 0);
            }

            // Свесы у торцов. У сланца — свой модуль на каждый торец (плитки лежат по общей сетке дома), у соломы — один,
            // правый развёрнут, чтобы ветровая доска была снаружи.
            if (joint != -90) PutRoof($"{cover}_ovh_d{rd}", -GableOverhang / 2, ry, top, 0);
            if (joint != 90)
            {
                if (thatch) PutRoof($"thatch_ovh_d{rd}", w + GableOverhang / 2, ry, top, 180);
                else PutRoof($"roof_ovhr_d{rd}", w + GableOverhang / 2, ry, top, 0);
            }
            if (rng.NextDouble() < chimneyChance) Put($"chimney_d{rd}", rng.Next(w) + 0.5f, ry - rd / 2f + rd * 0.75f, top, 0);
        }

        /// <summary>Граница клеток посреди двери нижнего этажа: столба и кронштейна на ней нет.</summary>
        private bool DoorMiddle(Cell b, float y0, int door) => Mathf.Approximately(b.Rotation, doorSide) && Mathf.Approximately(Along(b, y0), door + 1);

        /// <summary>Сторона пристройки, которой она примыкает к основному дому.</summary>
        private bool JoinedSide(Cell c) => joint != 0 && Mathf.Approximately(c.Rotation, joint);

        /// <summary>Угол пристройки на примыкающем торце.</summary>
        private bool JoinedCorner(Cell c) => joint < 0 && c.X == 0 || joint > 0 && c.X == width;

        /// <summary>
        /// Навес вдоль торца первого этажа: клетки навеса по всей длине торца, столбы — на концах и через две клетки.
        /// Крайние столбы с одним подкосом — внутрь навеса.
        /// </summary>
        private void BuildLeanTo(int side)
        {
            foreach (var c in Sides(width, 0, depth))
            {
                if (Mathf.Approximately(c.Rotation, side)) Put("lean_to", c.X, c.Y, PlinthHeight, side);
            }

            // Ось X модуля у правого торца идёт к задней стене (+Y), у левого — к передней.
            var x = side < 0 ? 0f : width;
            var towardBack = side > 0 ? "lean_to_post_r" : "lean_to_post_l";
            var towardFront = side > 0 ? "lean_to_post_l" : "lean_to_post_r";
            for (var y = 0; y <= depth; y += 2)
            {
                Put(y == 0 ? towardBack : y == depth ? towardFront : "lean_to_post", x, y, PlinthHeight, side);
            }

            if (depth % 2 == 1) Put(towardFront, x, depth, PlinthHeight, side);
        }

        /// <summary>
        /// Пристройка на этаж ниже — такой же генератор на дочернем объекте, вплотную к торцу и вровень с задней стеной; крыша и
        /// первый этаж как у основного дома. Шириной в три клетки, мельче основного дома; дверь — в средней паре клеток задней стены
        /// или внешнего торца.
        /// </summary>
        private void BuildWing(int side, int wingDepth, int wingDoor, GroundFloor ground)
        {
            var go = new GameObject("Wing") { hideFlags = HideFlags.DontSaveInEditor | HideFlags.NotEditable };
            go.SetActive(false);
            go.transform.SetParent(root, false);
            go.transform.localPosition = new Vector3(SignX * (side < 0 ? -WingWidth : width), 0, SignY * (depth - wingDepth));
            var wing = go.AddComponent<HouseGenerator>();
            wing.kit = kit;
            wing.width = WingWidth;
            wing.depth = wingDepth;
            wing.floors = floors - 1;
            wing.seed = seed * 7919 + 13;
            wing.stage = stage;
            wing.decay = decay;
            wing.age = age;
            wing.wear = wear;
            wing.jointPlaster = plaster;
            wing.chimneyChance = chimneyChance;
            wing.jettyChance = 0f;
            wing.plasterChance = plasterChance;
            wing.stoneChance = stoneChance;
            wing.cellarChance = cellarChance;
            wing.dormerChance = dormerChance;
            wing.doorHoodChance = doorHoodChance;
            wing.joint = side < 0 ? 90 : -90;
            wing.jointThatch = thatch;
            wing.jointGround = ground;
            wing.doorSide = wingDoor;
            go.SetActive(true);
        }

        /// <summary>
        /// Вид окна клетки по броску от 0 до 1. Каменный и оштукатуренный этаж — обычное и малое окно. Торцы — больше малых окон
        /// и окон под козырьком. Нижний фахверковый этаж — ещё закрытые ставни, верхние — ленточные окна и окна под козырьком.
        /// </summary>
        private static string WindowModule(double roll, int floor, bool longSide, bool masonry)
        {
            if (masonry) return roll < 0.35 ? "wall_window_small" : "wall_window";
            if (!longSide)
                return roll < 0.35 ? "wall_window_small" : roll < 0.6 ? "wall_window_hood" : roll < 0.7 ? "wall_window_closed" : "wall_window";
            if (floor == 0) return roll < 0.2 ? "wall_window_small" : roll < 0.45 ? "wall_window_closed" : "wall_window";
            return roll < 0.25 ? "wall_window_ribbon" : roll < 0.4 ? "wall_window_hood" : roll < 0.5 ? "wall_window_closed"
                : roll < 0.65 ? "wall_window_small" : "wall_window";
        }

        /// <summary>
        /// Глухая клетка фахверка. В крайних клетках стороны — длинный раскос, верхним концом к углу дома: на двух концах стороны
        /// раскосы зеркальны. В средних — ригель, в одном случае из четырёх (roll == 0) с косым крестом под ним.
        /// </summary>
        private static string PlainWall(int roll, Cell c, int w, float y0, int d)
        {
            if (AtCorner(c, w, y0, d))
            {
                var atStart = Along(c, y0) < 1;
                // Ось X модуля идёт вдоль стороны у переднего и правого фасадов и против неё — у заднего и левого.
                var alongSide = c.Rotation == 0 || c.Rotation == 90;
                return atStart == alongSide ? "wall_plain_c" : "wall_plain_b";
            }

            return roll == 0 ? "wall_plain_x" : "wall_plain_a";
        }

        /// <summary>Модуль каменного или оштукатуренного этажа вместо фахверкового: дверь, окно, малое окно или глухая стена.</summary>
        private static string MasonryModule(string module, GroundFloor ground)
        {
            var prefix = ground == GroundFloor.Stone ? "stone" : "render";
            return module switch
            {
                "wall_door" => $"{prefix}_door",
                "wall_window" => $"{prefix}_window",
                "wall_window_small" => $"{prefix}_window_small",
                _ => $"{prefix}_plain",
            };
        }

        private static string Pick(System.Random rng, string a, string b) => rng.Next(2) == 0 ? a : b;

        /// <summary>Положение клетки вдоль её стороны от начала координат дома: по X у переднего и заднего фасадов, по Y у торцов.</summary>
        private static float Along(Cell c, float y0) => c.Rotation == 0 || c.Rotation == 180 ? c.X : c.Y - y0;

        /// <summary>
        /// Модуль крыши (повёрнут на 0 или 180°, ось X — вдоль конька) на своём месте: развёртка сдвинута на положение клетки, чтобы
        /// текстура шла по скату без швов, а вершины опущены по провису конька — одной кривой на весь дом, стыки клеток совпадают.
        /// </summary>
        private void PutRoof(string module, float x, float y, float z, float rotation)
        {
            var go = Put(module, x, y, z, rotation, false);
            if (go == null) return;
            var along = Mathf.Approximately(rotation, 180) ? -1 : 1;
            foreach (var filter in go.GetComponentsInChildren<MeshFilter>())
            {
                filter.sharedMesh = RoofMesh(filter.sharedMesh, x, along, width, sag);
            }
        }

        /// <summary>Копии сеток крыши — одна на сетку, клетку, ширину и провис для всех домов; в сцену не сохраняются.</summary>
        private static readonly Dictionary<(Mesh, int, int, int, int), Mesh> RoofMeshes = new Dictionary<(Mesh, int, int, int, int), Mesh>();

        private static Mesh RoofMesh(Mesh source, float cellX, int along, int houseWidth, float ridgeSag)
        {
            var sagCm = Mathf.RoundToInt(ridgeSag * 100);
            var key = (source, Mathf.RoundToInt(cellX * 100), along, houseWidth, sagCm);
            if (RoofMeshes.TryGetValue(key, out var mesh) && mesh != null) return mesh;

            mesh = Instantiate(source);
            mesh.name = $"{source.name} x{cellX:0.##} w{houseWidth} sag{sagCm}";
            mesh.hideFlags = HideFlags.DontSave;
            var uv = mesh.uv;
            var vertices = mesh.vertices;
            var s = sagCm / 100f;
            for (var i = 0; i < vertices.Length; i++)
            {
                // Вершина в осях кита: X модуля — это −X Unity; положение вдоль конька дома — клетка плюс смещение в модуле.
                var local = SignX * vertices[i].x;
                var t = (cellX + along * local) / houseWidth;
                vertices[i].y -= s * 4 * t * (1 - t);
                uv[i].x += along * cellX;
            }

            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.RecalculateBounds();
            RoofMeshes[key] = mesh;
            return mesh;
        }

        /// <summary>
        /// Пол этажа — доски по всему дому, вдоль конька. Края уходят в стены; у пристройки со стороны основного дома стены нет — там
        /// пол доходит до стены дома. Снизу та же плита — потолок нижнего этажа. Рисунок досок сдвинут по дому и этажу.
        /// </summary>
        private void PutFloor(int w, float y0, int d, float z, int floor)
        {
            if (kit.Floor == null) return;
            var go = new GameObject("floor") { hideFlags = HideFlags.DontSaveInEditor | HideFlags.NotEditable };
            go.transform.SetParent(root, false);
            go.transform.localPosition = new Vector3(0, z + FloorLift, 0);
            go.AddComponent<MeshFilter>().sharedMesh = FloorMesh(w, y0, d, joint, textureShift + floor * 0.37f);
            go.AddComponent<MeshRenderer>().sharedMaterial = kit.Floor;
        }

        /// <summary>Сетки пола — одна на размер, примыкание и сдвиг рисунка для всех домов; в сцену не сохраняются.</summary>
        private static readonly Dictionary<(int, int, int, int, int), Mesh> FloorMeshes = new Dictionary<(int, int, int, int, int), Mesh>();

        /// <summary>
        /// Плита пола от верха (0) вниз на толщину. Развёртка — в метрах: сверху и снизу — X и Y кита (доски вдоль конька), по краям —
        /// вдоль края и вниз.
        /// </summary>
        private static Mesh FloorMesh(int w, float y0, int d, int joinedSide, float shift)
        {
            var key = (w, Mathf.RoundToInt(y0 * 100), d, joinedSide, Mathf.RoundToInt(shift * 100));
            if (FloorMeshes.TryGetValue(key, out var mesh) && mesh != null) return mesh;

            var x0 = joinedSide < 0 ? 0f : FloorInset;
            var x1 = joinedSide > 0 ? w : w - FloorInset;
            var ya = y0 + FloorInset;
            var yb = y0 + d - FloorInset;
            var t = -FloorThickness;
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();

            // Грань из четырёх точек в осях кита с развёрткой; обход выбирается по нормали — оси кита в Unity зеркальны.
            void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 e, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ue, Vector3 normal)
            {
                var start = vertices.Count;
                var n = ToUnity(normal);
                foreach (var p in new[] { a, b, c, e }) vertices.Add(ToUnity(p));
                foreach (var q in new[] { ua, ub, uc, ue }) uv.Add(q + new Vector2(shift, 0));
                for (var i = 0; i < 4; i++) normals.Add(n);
                var cross = Vector3.Cross(vertices[start + 1] - vertices[start], vertices[start + 2] - vertices[start]);
                if (Vector3.Dot(cross, n) >= 0) triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
                else triangles.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
            }

            foreach (var h in new[] { 0f, t })
            {
                Face(new Vector3(x0, ya, h), new Vector3(x1, ya, h), new Vector3(x1, yb, h), new Vector3(x0, yb, h),
                    new Vector2(x0, ya), new Vector2(x1, ya), new Vector2(x1, yb), new Vector2(x0, yb), new Vector3(0, 0, h == 0 ? 1 : -1));
            }

            foreach (var y in new[] { ya, yb })
            {
                Face(new Vector3(x0, y, 0), new Vector3(x1, y, 0), new Vector3(x1, y, t), new Vector3(x0, y, t),
                    new Vector2(x0, 0), new Vector2(x1, 0), new Vector2(x1, t), new Vector2(x0, t), new Vector3(0, y == ya ? -1 : 1, 0));
            }

            foreach (var x in new[] { x0, x1 })
            {
                Face(new Vector3(x, ya, 0), new Vector3(x, yb, 0), new Vector3(x, yb, t), new Vector3(x, ya, t),
                    new Vector2(ya, 0), new Vector2(yb, 0), new Vector2(yb, t), new Vector2(ya, t), new Vector3(x == x0 ? -1 : 1, 0, 0));
            }

            mesh = new Mesh { name = $"floor w{w} d{d} y{y0:0.##} j{joinedSide} uv{shift:0.##}", hideFlags = HideFlags.DontSave };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            FloorMeshes[key] = mesh;
            return mesh;
        }

        /// <summary>Точка или направление из осей кита в оси Unity.</summary>
        private static Vector3 ToUnity(Vector3 p) => new Vector3(SignX * p.x, p.z, SignY * p.y);

        /// <summary>Ставит модуль: точка и поворот вокруг вертикали — в осях кита, против часовой стрелки при взгляде сверху.</summary>
        /// <param name="shiftTexture">Сдвинуть развёртку по месту модуля: соседние клетки стены показывают разные куски текстуры,
        /// а не один и тот же. Крыша сдвигает развёртку сама, вместе с провисом.</param>
        private GameObject Put(string module, float x, float y, float z, float rotation, bool shiftTexture = true)
        {
            var model = kit.Get(module);
            if (model == null)
            {
                Debug.LogWarning($"В ките нет модуля {module}", kit);
                return null;
            }

            var go = Instantiate(model, root);
            go.name = module;
            go.hideFlags = HideFlags.DontSaveInEditor | HideFlags.NotEditable;
            go.transform.localPosition = new Vector3(SignX * x, z, SignY * y);
            go.transform.localRotation = Quaternion.Euler(0, -rotation * SignX * SignY, 0);
            if (shiftTexture)
            {
                // Развёртка модуля — в метрах вдоль его X; сдвиг — положение клетки вдоль стены, высота этажа и сдвиг дома.
                var angle = rotation * Mathf.Deg2Rad;
                var along = x * Mathf.Cos(angle) + y * Mathf.Sin(angle) + textureShift;
                foreach (var filter in go.GetComponentsInChildren<MeshFilter>())
                {
                    filter.sharedMesh = ShiftedMesh(filter.sharedMesh, along, z);
                }
            }

            return go;
        }

        private static readonly Dictionary<(Mesh, int, int), Mesh> ShiftedMeshes = new Dictionary<(Mesh, int, int), Mesh>();

        /// <summary>
        /// Забывает копии сеток. Переимпорт модели меняет её сетку на месте — тот же объект, новая геометрия, — и без сброса дома
        /// собирались бы из копий старой геометрии. Дома, собранные из сброшенных копий, надо пересобрать.
        /// </summary>
        public static void ClearMeshCache()
        {
            foreach (var mesh in ShiftedMeshes.Values) DestroyCopy(mesh);
            foreach (var mesh in RoofMeshes.Values) DestroyCopy(mesh);
            foreach (var mesh in FloorMeshes.Values) DestroyCopy(mesh);
            ShiftedMeshes.Clear();
            RoofMeshes.Clear();
            FloorMeshes.Clear();
        }

        private static void DestroyCopy(Mesh mesh)
        {
            if (mesh == null) return;
            if (Application.isPlaying) Destroy(mesh);
            else DestroyImmediate(mesh);
        }

        /// <summary>Копия меша модуля с развёрткой, сдвинутой на (u, v) метров. Копии общие для всех домов и в сцену не сохраняются.</summary>
        private static Mesh ShiftedMesh(Mesh source, float u, float v)
        {
            var key = (source, Mathf.RoundToInt(u * 100), Mathf.RoundToInt(v * 100));
            if (ShiftedMeshes.TryGetValue(key, out var mesh) && mesh != null) return mesh;

            mesh = Instantiate(source);
            mesh.name = $"{source.name} uv{u:0.##},{v:0.##}";
            mesh.hideFlags = HideFlags.DontSave;
            var uv = mesh.uv;
            var shift = new Vector2(key.Item2 / 100f, key.Item3 / 100f);
            for (var i = 0; i < uv.Length; i++) uv[i] += shift;
            mesh.uv = uv;
            ShiftedMeshes[key] = mesh;
            return mesh;
        }

        private readonly struct Cell
        {
            public Cell(float x, float y, float rotation, bool front = false)
            {
                X = x;
                Y = y;
                Rotation = rotation;
                Front = front;
            }

            public float X { get; }
            public float Y { get; }
            public float Rotation { get; }
            public bool Front { get; }
        }

        /// <summary>Клетки фасадов по кругу: перед (на y0), зад, левый и правый торцы.</summary>
        private static IEnumerable<Cell> Sides(int w, float y0, int d)
        {
            for (var i = 0; i < w; i++)
            {
                yield return new Cell(i + 0.5f, y0, 0, true);
                yield return new Cell(w - i - 0.5f, y0 + d, 180);
            }

            for (var j = 0; j < d; j++)
            {
                yield return new Cell(0, y0 + d - j - 0.5f, -90);
                yield return new Cell(w, y0 + j + 0.5f, 90);
            }
        }

        /// <summary>Границы клеток на каждой стороне — места столбов.</summary>
        private static IEnumerable<Cell> Boundaries(int w, float y0, int d)
        {
            for (var i = 1; i < w; i++)
            {
                yield return new Cell(i, y0, 0);
                yield return new Cell(i, y0 + d, 180);
            }

            for (var j = 1; j < d; j++)
            {
                yield return new Cell(0, y0 + j, -90);
                yield return new Cell(w, y0 + j, 90);
            }
        }

        /// <summary>Крайняя клетка стороны — соседняя с углом дома.</summary>
        private static bool AtCorner(Cell c, int w, float y0, int d)
        {
            var along = c.Rotation == 0 || c.Rotation == 180 ? c.X : c.Y - y0;
            var length = c.Rotation == 0 || c.Rotation == 180 ? w : d;
            return along < 1 || along > length - 1;
        }

        private static IEnumerable<Cell> Corners(int w, float y0, int d)
        {
            yield return new Cell(0, y0, 0);
            yield return new Cell(w, y0, 90);
            yield return new Cell(w, y0 + d, 180);
            yield return new Cell(0, y0 + d, -90);
        }
    }
}
