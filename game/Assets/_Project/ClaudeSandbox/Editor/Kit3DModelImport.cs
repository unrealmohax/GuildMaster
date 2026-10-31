using System.Linq;
using GuildMaster.ClaudeSandbox.Kit3D;
using UnityEditor;

namespace GuildMaster.ClaudeSandbox
{
    /// <summary>
    /// После импорта модулей кита пересобирает дома на открытых сценах: генератор держит копии сеток модулей, и без сброса
    /// дома показывали бы старую геометрию до перезагрузки скриптов.
    /// </summary>
    public sealed class Kit3DModelImport : AssetPostprocessor
    {
        private const string Models = "Assets/_Project/ClaudeSandbox/Kit3D/Models/";

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (!imported.Any(path => path.StartsWith(Models))) return;

            // Пересборка — после импорта: во время него модели ещё нельзя ставить на сцену.
            EditorApplication.delayCall += () =>
            {
                HouseGenerator.ClearMeshCache();
                Kit3DMenu.RebuildHouses();
            };
        }
    }
}
