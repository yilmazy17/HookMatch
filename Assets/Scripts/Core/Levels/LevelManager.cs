using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Core
{
    /// <summary>
    /// Tracks which level the player is on and loads level JSON from Resources/Levels.
    /// Persists across scenes and survives play sessions via PlayerPrefs.
    /// </summary>
    public class LevelManager : MonoBehaviour
    {
        public const string LevelPrefsKey = "HookMatch_CurrentLevel";

        private const string LevelsResourcesFolder = "Levels";
        private const string LevelFilePrefix = "level_";

        private static readonly Regex TargetCubesBlockRegex =
            new Regex("\"target_cubes\"\\s*:\\s*\\{([^}]*)\\}", RegexOptions.Compiled);
        private static readonly Regex TargetCubeEntryRegex =
            new Regex("\"(\\w+)\"\\s*:\\s*(\\d+)", RegexOptions.Compiled);

        public static LevelManager Instance { get; private set; }

        public event Action<int> OnLevelChanged;

        public int CurrentLevel { get; private set; } = 1;
        public int MaxLevel { get; private set; } = 1;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            Application.targetFrameRate = 120;
            QualitySettings.vSyncCount = 0;

            MaxLevel = Mathf.Max(1, Resources.LoadAll<TextAsset>(LevelsResourcesFolder).Length);
            CurrentLevel = Mathf.Clamp(PlayerPrefs.GetInt(LevelPrefsKey, 1), 1, MaxLevel);
        }

        public void SetLevel(int level)
        {
            CurrentLevel = Mathf.Clamp(level, 1, MaxLevel);
            PlayerPrefs.SetInt(LevelPrefsKey, CurrentLevel);
            PlayerPrefs.Save();
            OnLevelChanged?.Invoke(CurrentLevel);
        }

        public void NextLevel() => SetLevel(CurrentLevel + 1);

        public LevelData LoadCurrentLevelData() => LoadLevelData(CurrentLevel);

        public LevelData LoadLevelData(int level)
        {
            string path = $"{LevelsResourcesFolder}/{LevelFilePrefix}{level:00}";
            TextAsset json = Resources.Load<TextAsset>(path);
            if (json == null)
            {
                Debug.LogError($"[LevelManager] Could not find level file at Resources/{path}.json");
                return null;
            }

            LevelData data = JsonUtility.FromJson<LevelData>(json.text);
            data.target_cubes = ParseTargetCubes(json.text);
            return data;
        }

        private static Dictionary<string, int> ParseTargetCubes(string json)
        {
            var result = new Dictionary<string, int>();

            Match block = TargetCubesBlockRegex.Match(json);
            if (!block.Success) return result;

            foreach (Match entry in TargetCubeEntryRegex.Matches(block.Groups[1].Value))
                result[entry.Groups[1].Value] = int.Parse(entry.Groups[2].Value);

            return result;
        }
    }
}
