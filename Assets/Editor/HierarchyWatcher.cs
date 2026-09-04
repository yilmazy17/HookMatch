using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
internal static class HierarchyWatcher
{
    private const string OutputFolder = "Assets/HierarchySnapshots";
    private const bool Enabled = false;

    private static bool s_Dirty;

    static HierarchyWatcher()
    {
        if (!Enabled)
        {
            return;
        }

        EditorApplication.hierarchyChanged += OnHierarchyChanged;
        EditorApplication.update += OnUpdate;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            s_Dirty = true; // capture initial state
        }
    }

    private static void OnHierarchyChanged()
    {
        if (Application.isPlaying)
        {
            s_Dirty = true;
        }
    }

    private static void OnUpdate()
    {
        if (!s_Dirty || !Application.isPlaying)
        {
            return;
        }

        s_Dirty = false;
        CaptureSnapshot();
    }

    private static void CaptureSnapshot()
    {
        Scene scene = SceneManager.GetActiveScene();

        HierarchySnapshot snapshot = new HierarchySnapshot
        {
            sceneName = scene.name,
            timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
            objects = new List<ObjectSnapshot>()
        };

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            CollectObjects(root.transform, root.name, "", snapshot.objects);
        }

        string json = JsonUtility.ToJson(snapshot, true);

        string absoluteFolder = Path.Combine(Directory.GetCurrentDirectory(), OutputFolder);
        if (!Directory.Exists(absoluteFolder))
        {
            Directory.CreateDirectory(absoluteFolder);
        }

        string fileName = "snapshot_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss-fff") + ".json";
        string fullPath = Path.Combine(absoluteFolder, fileName);
        File.WriteAllText(fullPath, json);

        AssetDatabase.Refresh();
    }

    private static void CollectObjects(Transform t, string path, string parentPath, List<ObjectSnapshot> list)
    {
        list.Add(new ObjectSnapshot
        {
            name = t.name,
            path = path,
            parentPath = parentPath,
            position = new[] { t.position.x, t.position.y, t.position.z },
            rotation = new[] { t.eulerAngles.x, t.eulerAngles.y, t.eulerAngles.z },
            scale = new[] { t.localScale.x, t.localScale.y, t.localScale.z },
            components = GetComponentNames(t.gameObject)
        });

        foreach (Transform child in t)
        {
            CollectObjects(child, path + "/" + child.name, path, list);
        }
    }

    private static string[] GetComponentNames(GameObject go)
    {
        Component[] components = go.GetComponents<Component>();
        string[] names = new string[components.Length];
        for (int i = 0; i < components.Length; i++)
        {
            names[i] = components[i] != null ? components[i].GetType().Name : "Missing";
        }

        return names;
    }

    [Serializable]
    private class HierarchySnapshot
    {
        public string sceneName;
        public string timestamp;
        public List<ObjectSnapshot> objects;
    }

    [Serializable]
    private class ObjectSnapshot
    {
        public string name;
        public string path;
        public string parentPath;
        public float[] position;
        public float[] rotation;
        public float[] scale;
        public string[] components;
    }
}
