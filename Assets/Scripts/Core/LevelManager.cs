using System;
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

            return JsonUtility.FromJson<LevelData>(json.text);
        }
    }
}
