using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 2026-11-06 (user: "a button in the SteveTheTroll dropdown for this exact scene with the ride"): one click to the troll + rider + halter workbench (Assets/RideFit.unity).
public static class RideSceneMenu
{
    const string ScenePath = "Assets/RideFit.unity";

    static bool OpenRideScene()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("[Ride] leave play mode first");
            return false;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return false;
        }

        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        return true;
    }

    [MenuItem("SteveTheTroll/Ride/Open ride scene (troll + rider + halter)")]
    public static void Open()
    {
        if (OpenRideScene())
        {
            Debug.Log($"[Ride] opened {ScenePath}");
        }
    }

    [MenuItem("SteveTheTroll/Ride/Rebuild ride scene (straps, seat, reins, textures)")]
    public static void Rebuild()
    {
        if (OpenRideScene())
        {
            HalterLeatherBuilder.Run();
            Debug.Log("[Ride] ride scene rebuilt and saved");
        }
    }

    [MenuItem("SteveTheTroll/Ride/Rebuild ride scene and export bundle to the mod")]
    public static void RebuildAndExport()
    {
        if (OpenRideScene())
        {
            HalterLeatherBuilder.Run();
            EditorApplication.ExecuteMenuItem("Valheim/Build AssetBundles (and copy to Mod)");
            Debug.Log("[Ride] ride scene rebuilt, bundle exported");
        }
    }
}