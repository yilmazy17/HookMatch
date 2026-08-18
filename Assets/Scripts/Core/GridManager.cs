using UnityEngine;

namespace Core
{
    /// <summary>
    /// Builds the play grid from LevelData: spawns colored cubes and fits the
    /// background sprite to the grid bounds. Cell types without an assigned
    /// prefab (obstacles/boosters not built yet) are skipped with a warning
    /// instead of throwing, so in-progress level JSON never breaks the grid.
    /// </summary>
    public class GridManager : MonoBehaviour
    {
        [Header("Cube Prefabs")]
        [SerializeField] private GameObject redCubePrefab;
        [SerializeField] private GameObject greenCubePrefab;
        [SerializeField] private GameObject yellowCubePrefab;
        [SerializeField] private GameObject blueCubePrefab;

        [Header("Background")]
        [SerializeField] private SpriteRenderer gridBackground;

        [Header("Grid Settings")]
        [SerializeField] private Transform gridContainer;

        private GameObject[,] _cubes;
        private int _width;
        private int _height;
        private Vector2 _cellSize;

        private void Start()
        {
            if (LevelManager.Instance == null)
            {
                Debug.LogError("[GridManager] No LevelManager found in the scene.");
                return;
            }

            LevelManager.Instance.OnLevelChanged += HandleLevelChanged;

            LevelData levelData = LevelManager.Instance.LoadCurrentLevelData();
            if (levelData != null) BuildGrid(levelData);
        }

        private void OnDestroy()
        {
            if (LevelManager.Instance != null)
                LevelManager.Instance.OnLevelChanged -= HandleLevelChanged;
        }

        private void HandleLevelChanged(int level)
        {
            LevelData levelData = LevelManager.Instance.LoadLevelData(level);
            if (levelData != null) BuildGrid(levelData);
        }

        public void BuildGrid(LevelData levelData)
        {
            ClearGrid();

            _width = levelData.grid_width;
            _height = levelData.grid_height;
            _cellSize = DetermineCellSize();
            _cubes = new GameObject[_width, _height];

            Transform parent = gridContainer != null ? gridContainer : transform;
            Vector3 origin = GetGridOrigin();

            for (int row = 0; row < _height; row++)
            {
                for (int col = 0; col < _width; col++)
                {
                    int index = row * _width + col;
                    if (index >= levelData.grid.Length) continue;

                    GameObject prefab = GetPrefabForCode(levelData.grid[index]);
                    if (prefab == null) continue;

                    Vector3 position = GetWorldPosition(col, row, origin);
                    GameObject cube = Instantiate(prefab, position, Quaternion.identity, parent);
                    cube.name = $"Cube_{col}_{row}";
                    _cubes[col, row] = cube;
                }
            }

            PositionBackground(origin);
        }

        private GameObject GetPrefabForCode(string code)
        {
            switch (code)
            {
                case "r": return redCubePrefab;
                case "g": return greenCubePrefab;
                case "y": return yellowCubePrefab;
                case "b": return blueCubePrefab;
                case "rand":
                    GameObject[] colors = { redCubePrefab, greenCubePrefab, yellowCubePrefab, blueCubePrefab };
                    return colors[Random.Range(0, colors.Length)];
                default:
                    Debug.LogWarning($"[GridManager] Cell type '{code}' has no prefab assigned yet — skipping.");
                    return null;
            }
        }

        private Vector2 DetermineCellSize()
        {
            GameObject reference = redCubePrefab != null ? redCubePrefab
                : greenCubePrefab != null ? greenCubePrefab
                : yellowCubePrefab != null ? yellowCubePrefab
                : blueCubePrefab;

            if (reference == null) return Vector2.one;

            SpriteRenderer sr = reference.GetComponentInChildren<SpriteRenderer>();
            if (sr == null || sr.sprite == null) return Vector2.one;

            Vector2 spriteSize = sr.sprite.bounds.size;
            Vector3 scale = sr.transform.lossyScale;
            return new Vector2(spriteSize.x * scale.x, spriteSize.y * scale.y);
        }

        private Vector3 GetGridOrigin()
        {
            float gridWorldWidth = _width * _cellSize.x;
            float gridWorldHeight = _height * _cellSize.y;
            return transform.position - new Vector3(gridWorldWidth * 0.5f, gridWorldHeight * 0.5f, 0f);
        }

        private Vector3 GetWorldPosition(int col, int row, Vector3 origin)
        {
            // row 0 in the JSON grid is the top row.
            float x = origin.x + (col + 0.5f) * _cellSize.x;
            float y = origin.y + (_height - row - 0.5f) * _cellSize.y;
            return new Vector3(x, y, 0f);
        }

        private void PositionBackground(Vector3 origin)
        {
            if (gridBackground == null || gridBackground.sprite == null) return;

            float gridWorldWidth = _width * _cellSize.x;
            float gridWorldHeight = _height * _cellSize.y;
            Vector2 spriteSize = gridBackground.sprite.bounds.size;

            gridBackground.transform.localScale = new Vector3(
                gridWorldWidth / spriteSize.x,
                gridWorldHeight / spriteSize.y,
                1f);
            gridBackground.transform.position = origin;
        }

        private void ClearGrid()
        {
            if (_cubes == null) return;

            foreach (GameObject cube in _cubes)
            {
                if (cube == null) continue;
                if (Application.isPlaying) Destroy(cube);
                else DestroyImmediate(cube);
            }

            _cubes = null;
        }
    }
}
