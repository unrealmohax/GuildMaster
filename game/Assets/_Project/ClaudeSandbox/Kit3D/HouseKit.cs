using System;
using System.Collections.Generic;
using UnityEngine;

namespace GuildMaster.ClaudeSandbox.Kit3D
{
    /// <summary>
    /// Набор модулей домов: имя модуля из Blender → модель. Заполняется меню GuildMaster → Sandbox → Kit3D → Build Kit Catalog
    /// по папке моделей кита.
    /// </summary>
    [CreateAssetMenu(menuName = "GuildMaster/Sandbox/House Kit")]
    public sealed class HouseKit : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string name;
            public GameObject model;
        }

        [SerializeField] private List<Entry> modules = new List<Entry>();

        private Dictionary<string, GameObject> byName;

        public IReadOnlyList<Entry> Modules => modules;

        public GameObject Get(string name)
        {
            if (byName == null)
            {
                byName = new Dictionary<string, GameObject>();
                foreach (var e in modules)
                {
                    if (e.model != null) byName[e.name] = e.model;
                }
            }

            return byName.TryGetValue(name, out var model) ? model : null;
        }

        public void SetModules(List<Entry> entries)
        {
            modules = entries;
            byName = null;
        }

        private void OnValidate() => byName = null;
    }
}
