using Oculus.Interaction.Locomotion;
using Oculus.Interaction.Surfaces;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Işınlanma yalnızca NavMesh'e bakıyordu; duvar ve döşemeler ışını durdurmadığı için üst kattan
/// alt kata, duvarların arkasına ışınlanılabiliyordu. Bu menü açık sahneye Meta'nın önerdiği engeli ekler:
/// Allow Teleport kapalı bir TeleportInteractable + PhysicsLayerSurface (Default katmanındaki tüm collider'lar).
/// Işın önce bir duvara / döşemeye çarparsa ışınlanma engellenir. Hedefte NavMesh ile döşeme aynı mesafede
/// olduğunda eşitlik NavMesh'e (tie breaker 0 > -100) bozulduğu için normal ışınlanma çalışır.
/// Su (Water katmanı) ve oyuncu engel sayılmaz.
/// </summary>
public static class TeleportBlockerSetup
{
    private const string ObjectName = "Teleport Blocker (Duvar ve Döşeme)";

    [MenuItem("Tools/Işınlanma/Duvar ve Döşeme Engelini Ekle (Açık Sahne)")]
    public static void AddBlocker()
    {
        GameObject go = GameObject.Find(ObjectName);
        if (go == null)
        {
            go = new GameObject(ObjectName);
            Undo.RegisterCreatedObjectUndo(go, "Işınlanma engeli");
        }

        PhysicsLayerSurface surface = go.GetComponent<PhysicsLayerSurface>();
        if (surface == null) surface = Undo.AddComponent<PhysicsLayerSurface>(go);
        var surfaceSo = new SerializedObject(surface);
        surfaceSo.FindProperty("_layerMask").intValue = LayerMask.GetMask("Default");
        surfaceSo.ApplyModifiedProperties();

        TeleportInteractable blocker = go.GetComponent<TeleportInteractable>();
        if (blocker == null) blocker = Undo.AddComponent<TeleportInteractable>(go);
        var blockerSo = new SerializedObject(blocker);
        blockerSo.FindProperty("_allowTeleport").boolValue = false;
        blockerSo.FindProperty("_tieBreakerScore").intValue = -100;
        blockerSo.FindProperty("_surface").objectReferenceValue = surface;
        blockerSo.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(go.scene);
        Selection.activeGameObject = go;
        EditorUtility.DisplayDialog("Işınlanma Engeli",
            $"'{ObjectName}' eklendi: Default katmanındaki duvar ve döşemeler artık ışınlanma ışınını durduruyor.\n\n" +
            "Sahneyi kaydetmeyi unutma (Cmd+S). Eğitim sahnesinde de aynı menüyü çalıştır.", "Tamam");
    }
}
