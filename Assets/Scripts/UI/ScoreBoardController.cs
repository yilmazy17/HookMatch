using System.Collections.Generic;
using Core;
using TMPro;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// Drives the scoreboard: move counter and one GoalBadge per color in the
    /// current level's target_cubes, rebuilt fresh every time a level starts.
    /// </summary>
    public class ScoreBoardController : MonoBehaviour
    {
        [SerializeField] private GridManager gridManager;
        [SerializeField] private TextMeshProUGUI moveCountText;
        [SerializeField] private GoalBadge goalBadgePrefab;
        [SerializeField] private RectTransform goalsParent;
        [Tooltip("Extra breathing room added on top of the badge's own width/height, so resizing the prefab in the editor can't make badges overlap again.")]
        [SerializeField] private float goalSlotGap = 16f;
        [Tooltip("Wraps to a new row after this many goals, like the cube grid.")]
        [SerializeField] private int maxGoalsPerRow = 3;

        private readonly List<GoalBadge> _goalBadges = new List<GoalBadge>();
        private int _movesRemaining;

        // Awake/OnDestroy, not OnEnable/OnDisable: this component lives on
        // ScoreBoard, which is itself inside the InGame panel that UIManager
        // shows/hides *in response to* GridManager.OnGameStarted. Subscribing in
        // OnEnable meant UIManager's Show(Home) at Start disabled ScoreBoard
        // (unsubscribing this) before the level ever started, so the real
        // OnGameStarted firing - which re-enables ScoreBoard - always found this
        // listener already missing from the invocation list. Awake/OnDestroy
        // subscribe once for the object's whole lifetime, independent of the
        // panel's active state.
        private void Awake()
        {
            GridManager.OnGameStarted += HandleGameStarted;
            if (gridManager != null)
            {
                gridManager.OnCubeCleared += HandleCubeCleared;
                gridManager.OnMoveUsed += HandleMoveUsed;
            }
        }

        private void OnDestroy()
        {
            GridManager.OnGameStarted -= HandleGameStarted;
            if (gridManager != null)
            {
                gridManager.OnCubeCleared -= HandleCubeCleared;
                gridManager.OnMoveUsed -= HandleMoveUsed;
            }
        }

        private void HandleGameStarted()
        {
            LevelData levelData = LevelManager.Instance != null ? LevelManager.Instance.LoadCurrentLevelData() : null;
            if (levelData == null) return;

            _movesRemaining = levelData.move_count;
            UpdateMoveCountText();
            BuildGoals(levelData.target_cubes);
        }

        private void BuildGoals(Dictionary<string, int> targetCubes)
        {
            foreach (GoalBadge badge in _goalBadges)
                if (badge != null) Destroy(badge.gameObject);
            _goalBadges.Clear();

            if (goalBadgePrefab == null || goalsParent == null || targetCubes == null || targetCubes.Count == 0) return;

            // Spacing is derived from the prefab's own current size (+ a gap)
            // instead of a fixed number, so resizing the badge in the editor
            // can never leave the old spacing too small for the new badge again.
            RectTransform prefabRect = goalBadgePrefab.GetComponent<RectTransform>();
            float columnSpacing = prefabRect.sizeDelta.x + goalSlotGap;
            float rowSpacing = prefabRect.sizeDelta.y + goalSlotGap;

            // Same idea as GridManager's cube grid: a fixed-width row that wraps
            // after maxGoalsPerRow, with the whole block centered on the panel.
            int columns = Mathf.Min(maxGoalsPerRow, targetCubes.Count);
            int rows = Mathf.CeilToInt(targetCubes.Count / (float)maxGoalsPerRow);
            Vector2 topLeft = new Vector2(-(columns - 1) * columnSpacing * 0.5f, (rows - 1) * rowSpacing * 0.5f);

            int index = 0;
            foreach (KeyValuePair<string, int> entry in targetCubes)
            {
                int row = index / maxGoalsPerRow;
                int col = index % maxGoalsPerRow;

                GoalBadge badge = Instantiate(goalBadgePrefab, goalsParent);
                RectTransform badgeRect = badge.GetComponent<RectTransform>();
                // Force center anchoring regardless of how the prefab is
                // authored, since the position math above assumes it.
                badgeRect.anchorMin = new Vector2(0.5f, 0.5f);
                badgeRect.anchorMax = new Vector2(0.5f, 0.5f);
                badgeRect.anchoredPosition = topLeft + new Vector2(col * columnSpacing, -row * rowSpacing);

                Sprite icon = gridManager != null ? gridManager.GetNormalSprite(entry.Key) : null;
                badge.Initialize(entry.Key, entry.Value, icon);

                _goalBadges.Add(badge);
                index++;
            }
        }

        private void HandleCubeCleared(string colorCode)
        {
            foreach (GoalBadge badge in _goalBadges)
                badge.NotifyCubeCleared(colorCode);
        }

        private void HandleMoveUsed()
        {
            _movesRemaining = Mathf.Max(0, _movesRemaining - 1);
            UpdateMoveCountText();
        }

        private void UpdateMoveCountText()
        {
            if (moveCountText != null) moveCountText.text = _movesRemaining.ToString();
        }
    }
}
