using System.Collections.Generic;
using DG.Tweening;
using Items;
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
        [Header("Grid Horizontal Padding")]
        [SerializeField] private float GridHorizontalPadding;

        [Header("Background")]
        [SerializeField] private SpriteRenderer gridBackground;

        [Header("Floor")]
        [SerializeField] private BoxCollider2D _boxCollider2D;
        [SerializeField] private Camera targetCamera;

        [Header("Cube Animation")]
        [SerializeField] private float dropSpeed = 12f;
        [SerializeField] private float initialDropSpeed = 8f;
        [SerializeField] private float minDropDuration = 0.12f;
        [SerializeField] private float destroyDuration = 0.15f;
        [SerializeField] private float backgroundDropDuration = 1.2f;

        private Cube[,] _cubes;
        private int _width;
        private int _height;
        private Vector2 _cellSize;
        private Vector3 _origin;

        private void Start()
        {
            if (targetCamera == null) targetCamera = Camera.main;

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
            _cellSize = CalculateCellSize();
            _cubes = new Cube[_width, _height];

            Vector2 cubeScale = CalculateCubeScale();
            _origin = GetGridOrigin();

            for (int row = 0; row < _height; row++)
            {
                for (int col = 0; col < _width; col++)
                {
                    int index = row * _width + col;
                    if (index >= levelData.grid.Length) continue;

                    string colorCode = ResolveColorCode(levelData.grid[index]);
                    SpawnCube(col, row, colorCode, cubeScale);
                }
            }

            PositionBackground();
            PositionFloorCollider();
        }

        // Instantiates a cube off-screen above its target cell, registers it in
        // the grid, and tweens it down into place. Shared by the initial build
        // and the post-explosion cascade refill.
        private Cube SpawnCube(int col, int row, string colorCode, Vector2 cubeScale)
        {
            GameObject prefab = GetPrefabForColorCode(colorCode);
            if (prefab == null) return null;

            Vector3 targetPosition = GetWorldPosition(col, row, _origin);
            Vector3 spawnPosition = new Vector3(targetPosition.x, GetOffScreenTopY(), targetPosition.z);
            float fallDuration = CalculateDropDuration(spawnPosition.y - targetPosition.y, initialDropSpeed);

            GameObject cubeObject = Instantiate(prefab, spawnPosition, Quaternion.identity, transform);
            cubeObject.name = $"Cube_{col}_{row}";
            cubeObject.transform.localScale = new Vector3(
                prefab.transform.localScale.x * cubeScale.x,
                prefab.transform.localScale.y * cubeScale.y,
                prefab.transform.localScale.z);

            Cube cube = cubeObject.GetComponent<Cube>();
            if (cube == null)
            {
                Debug.LogError($"[GridManager] Prefab '{prefab.name}' has no Cube component.");
                Destroy(cubeObject);
                return null;
            }

            cube.Initialize(this, col, row, colorCode);
            _cubes[col, row] = cube;

            cubeObject.transform.DOMove(targetPosition, fallDuration)
                .SetEase(Ease.InOutBack)
                .SetLink(cubeObject);

            return cube;
        }

        // Called by a Cube when it's flicked. Scans in a straight line for the
        // furthest same-color cube the hook can chain to, then sweeps every
        // cell from the flicked cube up to that point (regardless of color)
        // back to the starting point to explode.
        public void RequestHookExplosion(Cube startCube, Vector2Int direction)
        {
            if (direction == Vector2Int.zero) return;

            int startCol = startCube.Column;
            int startRow = startCube.Row;

            if (startCol < 0 || startCol >= _width || startRow < 0 || startRow >= _height) return;
            if (_cubes[startCol, startRow] != startCube) return; // stale flick

            int furthestCol = startCol;
            int furthestRow = startRow;
            bool foundMatch = false;

            int col = startCol + direction.x;
            int row = startRow + direction.y;

            while (col >= 0 && col < _width && row >= 0 && row < _height)
            {
                Cube cube = _cubes[col, row];
                if (cube != null && cube.ColorCode == startCube.ColorCode)
                {
                    furthestCol = col;
                    furthestRow = row;
                    foundMatch = true;
                }

                col += direction.x;
                row += direction.y;
            }

            if (!foundMatch)
            {
                startCube.Shake();
                return; // nothing of the same color for the hook to attach to
            }

            ExplodeLine(startCol, startRow, furthestCol, furthestRow, direction);
        }

        // Pulls every cube from the start cell to the furthest hook cell
        // (inclusive, any color) back to the start position and destroys it,
        // then drops each column that lost a cube.
        private void ExplodeLine(int startCol, int startRow, int endCol, int endRow, Vector2Int direction)
        {
            Vector3 startWorldPosition = GetWorldPosition(startCol, startRow, _origin);
            var affectedColumns = new HashSet<int>();

            int col = startCol;
            int row = startRow;

            while (true)
            {
                Cube cube = _cubes[col, row];
                if (cube != null)
                {
                    _cubes[col, row] = null;
                    affectedColumns.Add(col);

                    cube.transform.DOKill();
                    cube.transform.DOMove(startWorldPosition, destroyDuration)
                        .SetEase(Ease.InOutBack)
                        .SetLink(cube.gameObject)
                        .OnComplete(() => Destroy(cube.gameObject));
                }

                if (col == endCol && row == endRow) break;
                col += direction.x;
                row += direction.y;
            }

            foreach (int affectedColumn in affectedColumns)
                DropColumn(affectedColumn);
        }

        // Compacts a column so every remaining cube ends up stacked at the
        // bottom with no gaps, animating each one that had to move, then fills
        // whatever's left empty at the top with fresh random cubes dropped in
        // from off-screen.
        private void DropColumn(int col)
        {
            int writeRow = _height - 1;

            for (int readRow = _height - 1; readRow >= 0; readRow--)
            {
                Cube cube = _cubes[col, readRow];
                if (cube == null) continue;

                if (writeRow != readRow)
                {
                    _cubes[col, writeRow] = cube;
                    _cubes[col, readRow] = null;
                    cube.SetGridPosition(col, writeRow);

                    Vector3 targetPosition = GetWorldPosition(col, writeRow, _origin);
                    float duration = CalculateDropDuration(Vector3.Distance(cube.transform.position, targetPosition), dropSpeed);
                    cube.transform.DOKill();
                    cube.transform.DOMove(targetPosition, duration)
                        .SetEase(Ease.InOutBack)
                        .SetLink(cube.gameObject);
                }

                writeRow--;
            }

            if (writeRow < 0) return; // column was already full

            Vector2 cubeScale = CalculateCubeScale();
            for (int row = 0; row <= writeRow; row++)
                SpawnCube(col, row, ResolveColorCode("rand"), cubeScale);
        }

        // Falling further should take longer, not the same time as a short drop -
        // duration scales with distance at a constant fall speed, floored so very
        // short drops (e.g. shifting down one cell) don't look instant.
        private float CalculateDropDuration(float distance, float speed)
        {
            if (speed <= 0f) return minDropDuration;
            return Mathf.Max(minDropDuration, distance / speed);
        }

        // "rand" needs to resolve to one concrete color at spawn time so the
        // spawned cube has a stable ColorCode for the hook to match against.
        private static string ResolveColorCode(string code)
        {
            if (code != "rand") return code;

            string[] colors = { "r", "g", "y", "b" };
            return colors[Random.Range(0, colors.Length)];
        }

        private GameObject GetPrefabForColorCode(string colorCode)
        {
            switch (colorCode)
            {
                case "r": return redCubePrefab;
                case "g": return greenCubePrefab;
                case "y": return yellowCubePrefab;
                case "b": return blueCubePrefab;
                default:
                    Debug.LogWarning($"[GridManager] Cell type '{colorCode}' has no prefab assigned yet — skipping.");
                    return null;
            }
        }

        // Cells are sized off the camera's view width so grid_width cubes always
        // span exactly one screen width (e.g. grid_width 5 => 5 cubes per row on screen).
        private Vector2 CalculateCellSize()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            if (targetCamera == null || _width <= 0) return Vector2.one;

            float camHeight = targetCamera.orthographicSize * 2f - GridHorizontalPadding;
            float camWidth = camHeight * targetCamera.aspect;
            float size = camWidth / _width;
            return new Vector2(size, size);
        }

        // Cube prefabs are authored at their own native sprite size, so scale them
        // up/down to match the screen-fit cell size computed above.
        private Vector2 CalculateCubeScale()
        {
            Vector2 nativeSize = DetermineNativePrefabSize();
            if (nativeSize.x <= 0f || nativeSize.y <= 0f) return Vector2.one;
            return new Vector2(_cellSize.x / nativeSize.x, _cellSize.y / nativeSize.y);
        }

        private Vector2 DetermineNativePrefabSize()
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

        private void PositionBackground()
        {
            if (gridBackground == null || gridBackground.sprite == null) return;

            float gridWorldWidth = _width * _cellSize.x;
            float gridWorldHeight = _height * _cellSize.y;

            // The sprite's pivot is centered, so it's positioned at the grid's
            // center (this transform), not the bottom-left origin.
            Vector3 targetPosition = transform.position;
            gridBackground.transform.position = new Vector3(targetPosition.x, GetOffScreenTopY(), targetPosition.z);
            gridBackground.transform.localScale = Vector3.one;

            if (gridBackground.drawMode == SpriteDrawMode.Simple)
            {
                // Simple mode has no size field — it can only be resized via scale.
                Vector2 spriteSize = gridBackground.sprite.bounds.size;
                gridBackground.transform.localScale = new Vector3(
                    gridWorldWidth / spriteSize.x,
                    gridWorldHeight / spriteSize.y,
                    1f);
            }
            else
            {
                // Sliced/Tiled modes resize via the size field so the 9-slice
                // border (rounded corners) isn't stretched out of shape.
                gridBackground.size = new Vector2(gridWorldWidth +(gridWorldWidth * 0.06f), gridWorldHeight +(gridWorldWidth * 0.06f));
            }

            gridBackground.transform.DOKill();
            gridBackground.transform.DOMove(targetPosition, backgroundDropDuration)
                .SetEase(Ease.InOutBack);
        }

        private float GetOffScreenTopY()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            if (targetCamera == null) return transform.position.y + 20f;

            return targetCamera.transform.position.y + targetCamera.orthographicSize + _cellSize.y;
        }

        private void PositionFloorCollider()
        {
            if (_boxCollider2D == null || targetCamera == null) return;

            float camHeight = targetCamera.orthographicSize * 2f;
            float camWidth = camHeight * targetCamera.aspect;
            float screenBottom = targetCamera.transform.position.y - targetCamera.orthographicSize;

            // Thickness scales with cell size so it stays proportional to the cubes
            // it needs to catch, whatever the current level's grid looks like.
            float colliderHeight = _cellSize.y;

            _boxCollider2D.size = new Vector2(camWidth, colliderHeight);
            _boxCollider2D.transform.position = new Vector3(
                targetCamera.transform.position.x,
                screenBottom - colliderHeight * 0.5f,
                0f);
        }

        private void ClearGrid()
        {
            if (_cubes == null) return;

            foreach (Cube cube in _cubes)
            {
                if (cube == null) continue;
                cube.transform.DOKill();
                if (Application.isPlaying) Destroy(cube.gameObject);
                else DestroyImmediate(cube.gameObject);
            }

            _cubes = null;
        }
    }
}
