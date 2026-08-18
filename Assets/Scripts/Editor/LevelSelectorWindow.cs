using System.IO;
using System.Linq;
using Core;
using UnityEditor;
using UnityEngine;

namespace CoreEditor
{
    /// <summary>
    /// Editor-only tool for jumping between levels without playing through them.
    /// Writes the same PlayerPrefs key LevelManager reads, and pushes live
    /// updates into a running LevelManager instance during Play mode.
    /// </summary>
    public class LevelSelectorWindow : EditorWindow
    {
        private const string LevelsFolderPath = "Assets/Resources/Levels";

        private int[] _availableLevels = System.Array.Empty<int>();
        private int _targetLevel = 1;

        [MenuItem("HookMatch/Level Selector")]
        private static void Open()
        {
            GetWindow<LevelSelectorWindow>("Level Selector");
        }

        private void OnEnable()
        {
            RefreshAvailableLevels();
            int maxLevel = _availableLevels.Length > 0 ? _availableLevels.Max() : 1;
            _targetLevel = Mathf.Clamp(PlayerPrefs.GetInt(LevelManager.LevelPrefsKey, 1), 1, maxLevel);
        }

        private void RefreshAvailableLevels()
        {
            if (!Directory.Exists(LevelsFolderPath))
            {
                _availableLevels = System.Array.Empty<int>();
                return;
            }

            _availableLevels = Directory.GetFiles(LevelsFolderPath, "level_*.json")
                .Select(Path.GetFileNameWithoutExtension)
                .Select(name => name.Replace("level_", ""))
                .Where(suffix => int.TryParse(suffix, out _))
                .Select(int.Parse)
                .OrderBy(n => n)
                .ToArray();
        }

        private void OnGUI()
        {
            int currentSaved = PlayerPrefs.GetInt(LevelManager.LevelPrefsKey, 1);
            EditorGUILayout.LabelField("Current Saved Level", currentSaved.ToString());
            EditorGUILayout.LabelField("Levels Found In Resources", _availableLevels.Length.ToString());

            EditorGUILayout.Space();

            _targetLevel = EditorGUILayout.IntField("Target Level", _targetLevel);
            if (_availableLevels.Length > 0)
                _targetLevel = Mathf.Clamp(_targetLevel, _availableLevels.Min(), _availableLevels.Max());

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Set Level")) SetLevel(_targetLevel);
            if (GUILayout.Button("Previous")) SetLevel(_targetLevel - 1);
            if (GUILayout.Button("Next")) SetLevel(_targetLevel + 1);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            if (GUILayout.Button("Refresh Level List")) RefreshAvailableLevels();
            if (GUILayout.Button("Reset Progress To Level 1")) SetLevel(1);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Quick Jump", EditorStyles.boldLabel);

            const int buttonsPerRow = 5;
            for (int i = 0; i < _availableLevels.Length; i += buttonsPerRow)
            {
                EditorGUILayout.BeginHorizontal();
                for (int j = i; j < Mathf.Min(i + buttonsPerRow, _availableLevels.Length); j++)
                {
                    int level = _availableLevels[j];
                    if (GUILayout.Button(level.ToString())) SetLevel(level);
                }
                EditorGUILayout.EndHorizontal();
            }

            if (Application.isPlaying && LevelManager.Instance != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox(
                    $"Play mode LevelManager.CurrentLevel = {LevelManager.Instance.CurrentLevel}",
                    MessageType.Info);
            }
        }

        private void SetLevel(int level)
        {
            if (_availableLevels.Length > 0)
                level = Mathf.Clamp(level, _availableLevels.Min(), _availableLevels.Max());

            _targetLevel = level;
            PlayerPrefs.SetInt(LevelManager.LevelPrefsKey, level);
            PlayerPrefs.Save();

            if (Application.isPlaying && LevelManager.Instance != null)
                LevelManager.Instance.SetLevel(level);

            Repaint();
        }
    }
}
