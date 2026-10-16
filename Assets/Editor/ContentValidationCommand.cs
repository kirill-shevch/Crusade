using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class ContentValidationCommand
{
    [MenuItem("Crusade/Validate Content")]
    public static void ValidateContent()
    {
        var exitCode = 1;
        string report;
        try
        {
            var units = UnitCatalog.FromJson(GameContent.Read("Configs/Units"));
            var abilities = JsonUtility.FromJson<AbilityArrayWrapper>(GameContent.Read("Configs/Abilities"));
            var maps = JsonUtility.FromJson<MapConfig>(GameContent.Read("Configs/Map"));
            var errors = ContentValidator.Validate(units, abilities?.abilities, maps?.maps,
                path => Resources.Load<Sprite>(path) != null);
            report = $"Units: {units.Definitions.Count}; maps: {maps?.maps?.Length ?? 0}; errors: {errors.Count}\n" + string.Join("\n", errors);
            foreach (var map in maps?.maps ?? Array.Empty<Map>())
                if (map != null && map.finalReward == "InnerFire")
                    report += "\nNOTE: " + map.name + ": legacy terminal reward InnerFire has no combat effect; review in S03/S08.";
            exitCode = errors.Count == 0 ? 0 : 1;
        }
        catch (Exception e) { report = e.ToString(); }
        var args = Environment.GetCommandLineArgs();
        var index = Array.IndexOf(args, "-contentReport");
        if (index >= 0 && index + 1 < args.Length)
        {
            var path = Path.GetFullPath(args[index + 1]);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, report);
        }
        if (exitCode == 0) Debug.Log(report); else Debug.LogError(report);
        if (Application.isBatchMode) EditorApplication.Exit(exitCode);
    }
}
