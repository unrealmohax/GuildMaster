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
        private const string GeneratedName = "Generated";

        // Как FBX из Blender ложится в Unity: X кита → −X Unity, Y кита (вглубь) → −Z Unity, Z кита (вверх) → Y Unity.
        // Лицевая сторона модуля (−Y кита) смотрит в Unity на +Z.
        private const float SignX = -1f;
        private const float SignY = -1f;

        [SerializeField] private HouseKit kit;
        [SerializeField, Range(2, 8)] private int width = 4;
        [SerializeField, Range(3, 6)] private int depth = 4;
        [SerializeField, Range(1, 3)] private int floors = 1;
        [SerializeField] private int seed = 1;
        [SerializeField] private HouseStage stage = HouseStage.Ready;
        [SerializeField, Range(0f, 1f)] private float decay = 0.7f;
        [SerializeField, Range(0f, 1f)] private float chimneyChance = 0.8f;

        private Transform root;
        private bool dirty;

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
            Build(new System.Random(seed));
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
                foreach (var c in Sides(w, d)) Put("rope_1", c.X, c.Y, 0, c.Rotation);
                foreach (var b in Boundaries(w, d)) Put("stake", b.X, b.Y, 0, 0);
                foreach (var c in Corners(w, d)) Put("stake", c.X, c.Y, 0, 0);
                return;
            }

            foreach (var c in Sides(w, d)) Put("plinth", c.X, c.Y, 0, c.Rotation);
            foreach (var c in Corners(w, d)) Put("plinth_corner", c.X, c.Y, 0, c.Rotation);

            var decayLevel = stage == HouseStage.Abandoned ? decay : 0f;
            var builtFloors = stage == HouseStage.Construction ? 1 : floors;
            var door = rng.Next(w);
            for (var f = 0; f < builtFloors; f++)
            {
                var z = PlinthHeight + f * FloorHeight;
                foreach (var c in Corners(w, d)) Put("corner_post", c.X, c.Y, z, c.Rotation);
                foreach (var b in Boundaries(w, d)) Put("post", b.X, b.Y, z, b.Rotation);
                var frontIndex = 0;
                foreach (var c in Sides(w, d))
                {
                    if (stage == HouseStage.Construction)
                    {
                        Put("beam_1", c.X, c.Y, z, c.Rotation);
                        if (rng.NextDouble() < 0.35) Put(Pick(rng, "wall_plain_a", "wall_plain_b"), c.X, c.Y, z, c.Rotation);
                        if (c.Front || c.Rotation > 0) Put("scaffold_1", c.X, c.Y, z, c.Rotation);
                        continue;
                    }

                    string module;
                    var windowChance = (c.Front ? 0.45 : 0.25) + (f > 0 ? 0.1 : 0);
                    if (c.Front && f == 0 && frontIndex == door) module = "wall_door";
                    else if (rng.NextDouble() < windowChance)
                        module = rng.NextDouble() < decayLevel * 0.7 ? "wall_window_boarded" : "wall_window";
                    else
                        module = rng.NextDouble() < decayLevel * 0.5 ? "wall_broken" : Pick(rng, "wall_plain_a", "wall_plain_b");
                    Put(module, c.X, c.Y, z, c.Rotation);
                    if (f == 0 && rng.NextDouble() < decayLevel * 0.3) Put("prop", c.X, c.Y, 0, c.Rotation);
                    if (c.Front) frontIndex++;
                }
            }

            if (stage == HouseStage.Construction) return;

            var top = PlinthHeight + floors * FloorHeight;
            Put($"gable_d{d}", 0, d / 2f, top, -90);
            Put($"gable_d{d}", w, d / 2f, top, 90);
            for (var i = 0; i < w; i++)
            {
                var hole = rng.NextDouble() < decayLevel * 0.4;
                Put(hole ? $"roof_hole_d{d}" : $"roof_d{d}", i + 0.5f, d / 2f, top, 0);
            }

            Put($"roof_ovh_d{d}", -GableOverhang / 2, d / 2f, top, 0);
            Put($"roof_ovh_d{d}", w + GableOverhang / 2, d / 2f, top, 0);
            if (rng.NextDouble() < chimneyChance) Put($"chimney_d{d}", rng.Next(w) + 0.5f, d * 0.75f, top, 0);
        }

        private static string Pick(System.Random rng, string a, string b) => rng.Next(2) == 0 ? a : b;

        /// <summary>Ставит модуль: точка и поворот вокруг вертикали — в осях кита, против часовой стрелки при взгляде сверху.</summary>
        private void Put(string module, float x, float y, float z, float rotation)
        {
            var model = kit.Get(module);
            if (model == null)
            {
                Debug.LogWarning($"В ките нет модуля {module}", kit);
                return;
            }

            var go = Instantiate(model, root);
            go.name = module;
            go.hideFlags = HideFlags.DontSaveInEditor | HideFlags.NotEditable;
            go.transform.localPosition = new Vector3(SignX * x, z, SignY * y);
            go.transform.localRotation = Quaternion.Euler(0, -rotation * SignX * SignY, 0);
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

        /// <summary>Клетки фасадов по кругу: перед, зад, левый и правый торцы.</summary>
        private static IEnumerable<Cell> Sides(int w, int d)
        {
            for (var i = 0; i < w; i++)
            {
                yield return new Cell(i + 0.5f, 0, 0, true);
                yield return new Cell(w - i - 0.5f, d, 180);
            }

            for (var j = 0; j < d; j++)
            {
                yield return new Cell(0, d - j - 0.5f, -90);
                yield return new Cell(w, j + 0.5f, 90);
            }
        }

        /// <summary>Границы клеток на каждой стороне — места столбов.</summary>
        private static IEnumerable<Cell> Boundaries(int w, int d)
        {
            for (var i = 1; i < w; i++)
            {
                yield return new Cell(i, 0, 0);
                yield return new Cell(i, d, 180);
            }

            for (var j = 1; j < d; j++)
            {
                yield return new Cell(0, j, -90);
                yield return new Cell(w, j, 90);
            }
        }

        private static IEnumerable<Cell> Corners(int w, int d)
        {
            yield return new Cell(0, 0, 0);
            yield return new Cell(w, 0, 90);
            yield return new Cell(w, d, 180);
            yield return new Cell(0, d, -90);
        }
    }
}
