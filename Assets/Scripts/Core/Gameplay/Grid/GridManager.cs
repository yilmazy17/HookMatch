using System;
using System.Collections;
using System.Collections.Generic;
using Core.Gameplay.Hook;
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
        [Serializable]
        private struct CubeColorDefinition
        {
            public string code;
            public Sprite normalSprite;
            public Sprite hookedSprite;
        }

        [Header("Rope Object")]
        [SerializeField] private GameObject ropeObjectPrefab;
        [Tooltip("Reduces segment density for every additional grid-cell span. Example: 6, 5.5, 5...")]
        [SerializeField, Min(0f)] private float ropeSegmentsPerCellFalloff = 0.5f;
        [Tooltip("Lowest allowed segment density on long ropes.")]
        [SerializeField, Min(1f)] private float minimumRopeSegmentsPerCell = 3f;

        // One shared prefab for every color - adding a new color is just a new
        // entry below (code + sprites), no new prefab/variant required.
        [Header("Cube Prefab")]
        [SerializeField] private GameObject cubePrefab;
        [SerializeField] private CubeColorDefinition[] cubeColors;
        [Header("Grid Horizontal Padding")]
        [SerializeField] private float GridHorizontalPadding;

        [Header("Cube Spacing")]
        [SerializeField] private float horizontalCubePadding;
        [SerializeField] private float verticalCubePadding;

        [Header("Background")]
        [SerializeField] private SpriteRenderer gridBackground;

        [Header("Camera")]
        [SerializeField] private Camera targetCamera;

        [Header("Cube Animation")]
        [SerializeField] private float dropSpeed = 12f;
        [SerializeField] private float initialDropSpeed = 8f;
        [SerializeField] private float minDropDuration = 0.12f;
        [SerializeField] private float destroyDuration = 0.15f;
        [SerializeField] private float backgroundDropDuration = 1.2f;

        [Header("Hook Explosion")]
        [SerializeField] private float vibrateInterval = 0.5f;
        [SerializeField] private float pullSpeed = 10f;
        [SerializeField] private float homeColumnDropDelay = 0.2f;
        

        private Cube[,] _cubes;
        private int _width;
        private int _height;
        private Vector2 _cellSize;
        private Vector3 _origin;

        // Fired once a level actually starts building, so UI (e.g. UIManager)
        // can react without GridManager knowing anything about panels.
        public static event Action OnGameStarted;

        // Per-instance since they carry live gameplay data - unlike
        // OnGameStarted, a ScoreBoard listener needs the specific grid it's
        // tracking, not "any grid, anywhere".
        public event Action<string> OnCubeCleared;
        public event Action OnMoveUsed;

        private Dictionary<string, CubeColorDefinition> _colorDefinitions;

        private void Awake()
        {
            _colorDefinitions = new Dictionary<string, CubeColorDefinition>();
            foreach (CubeColorDefinition definition in cubeColors)
            {
                if (string.IsNullOrEmpty(definition.code)) continue;
                _colorDefinitions[definition.code] = definition;
            }
        }

        private void Start()
        {
            if (targetCamera == null) targetCamera = Camera.main;

            if (LevelManager.Instance == null)
            {
                Debug.LogError("[GridManager] No LevelManager found in the scene.");
                return;
            }

            LevelManager.Instance.OnLevelChanged += HandleLevelChanged;
        }

        // Called once the player picks a level (e.g. from LevelButton) instead of
        // building automatically on scene load, so the grid only appears after
        // the level-select UI has been dismissed.
        public void StartLevel()
        {
            if (LevelManager.Instance == null) return;

            gameObject.SetActive(true);
            OnGameStarted?.Invoke();

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
                    SpawnCube(col, row, colorCode, cubeScale, initialDropSpeed, Ease.InOutBack);
                }
            }

            PositionBackground();
        }

        // Instantiates a cube off-screen above its target cell, registers it in
        // the grid, and tweens it down into place. Shared by the initial build
        // and the post-explosion cascade refill - each passes its own speed/ease
        // so a mid-cascade filler can match the pace of the cubes it's chasing
        // instead of using the initial build's slower, ease-in-heavy reveal.
        private Cube SpawnCube(int col, int row, string colorCode, Vector2 cubeScale, float speed, Ease ease)
        {
            if (cubePrefab == null) return null;

            if (!_colorDefinitions.TryGetValue(colorCode, out CubeColorDefinition definition))
            {
                Debug.LogWarning($"[GridManager] Color code '{colorCode}' has no cube color definition assigned — skipping.");
                return null;
            }

            Vector3 targetPosition = GetWorldPosition(col, row, _origin);
            Vector3 spawnPosition = new Vector3(targetPosition.x, GetOffScreenTopY(), targetPosition.z);
            float fallDuration = CalculateMoveDuration(spawnPosition.y - targetPosition.y, speed);

            GameObject cubeObject = Instantiate(cubePrefab, spawnPosition, Quaternion.identity, transform);
            cubeObject.name = $"Cube_{col}_{row}";
            cubeObject.transform.localScale = new Vector3(
                cubePrefab.transform.localScale.x * cubeScale.x,
                cubePrefab.transform.localScale.y * cubeScale.y,
                cubePrefab.transform.localScale.z);

            Cube cube = cubeObject.GetComponent<Cube>();
            if (cube == null)
            {
                Debug.LogError($"[GridManager] Prefab '{cubePrefab.name}' has no Cube component.");
                Destroy(cubeObject);
                return null;
            }

            cube.Initialize(this, col, row, colorCode, definition.normalSprite, definition.hookedSprite);
            _cubes[col, row] = cube;

            cubeObject.transform.DOMove(targetPosition, fallDuration)
                .SetEase(ease)
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
            var matchingCubes = new List<Cube>();

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
                    matchingCubes.Add(cube);
                }

                col += direction.x;
                row += direction.y;
            }

            if (!foundMatch)
            {
                startCube.Shake();
                return; // nothing of the same color for the hook to attach to
            }

            OnMoveUsed?.Invoke();
            StartCoroutine(PlayHookExplosion(startCube, matchingCubes, startCol, startRow, furthestCol, furthestRow, direction));
        }

        // Telegraphs the hook by vibrating the flicked cube and every same-color
        // match along the line, nearest to furthest, vibrateInterval apart. Then
        // pulls the furthest match back toward the start one cell at a time,
        // eliminating whatever cube (any color) sits in each cell as it arrives -
        // so each column's refill fires as its own gap appears instead of every
        // column dropping together at the end.
        private IEnumerator PlayHookExplosion(Cube startCube, List<Cube> matchingCubes, int startCol, int startRow, int endCol, int endRow, Vector2Int direction)
        {
            var hookRopes = new List<Rope>();
            Cube previousHookedCube = startCube;

            startCube.Vibrate();
            foreach (Cube matchingCube in matchingCubes)
            {
                yield return new WaitForSeconds(vibrateInterval);
                matchingCube.Vibrate();

                // Build a chain between consecutive hooked cubes. This avoids
                // one long rope running directly from the first match to the
                // last when there are several same-colour cubes in the line.
                Rope hookRope = SpawnHookRope(previousHookedCube, matchingCube);
                if (hookRope != null)
                    hookRopes.Add(hookRope);

                previousHookedCube = matchingCube;
            }

            // Let the completed chain read for one beat before the pull starts.
            yield return new WaitForSeconds(vibrateInterval);

            // Snapshot the path (start to end, inclusive) without clearing the grid
            // yet - clearing every cell upfront would make DropColumn see the whole
            // line as vacated at once, so a same-column chain would drop cubes from
            // above straight through cells the pulled cube hasn't reached yet. Each
            // cell is cleared only once the pulled cube actually arrives there.
            var pathCells = new List<(int col, int row, Cube cube)>();
            int col = startCol;
            int row = startRow;
            while (true)
            {
                pathCells.Add((col, row, _cubes[col, row]));
                if (col == endCol && row == endRow) break;
                col += direction.x;
                row += direction.y;
            }

            int pathLastIndex = pathCells.Count - 1;

            // Normally the furthest match travels back and converges at the
            // start. For a straight-down flick that looks wrong - it'd mean
            // cubes flying back up against gravity - so that case is inverted:
            // the start cube is the one that travels, converging at the bottom
            // (furthest) cell instead.
            bool reversed = direction.x == 0 && direction.y > 0;
            int pulledIndex = reversed ? 0 : pathLastIndex;
            int step = reversed ? 1 : -1;
            int activeRopeIndex = reversed ? 0 : hookRopes.Count - 1;

            var hookedCubes = new HashSet<Cube>(matchingCubes) { startCube };

            Cube pulledCube = pathCells[pulledIndex].cube;
            int homeCol = pathCells[pulledIndex].col;
            int homeRow = pathCells[pulledIndex].row;

            // The pulled cube leaves its own cell immediately, but the refill is
            // timed off an inspector-tunable delay rather than the travel time.
            _cubes[homeCol, homeRow] = null;
            StartCoroutine(DropColumnAfterDelay(homeCol, homeColumnDropDelay));

            for (int i = pulledIndex + step; i >= 0 && i <= pathLastIndex; i += step)
            {
                (int cellCol, int cellRow, Cube occupant) = pathCells[i];
                Vector3 targetPosition = GetWorldPosition(cellCol, cellRow, _origin);
                float duration = CalculateMoveDuration(Vector3.Distance(pulledCube.transform.position, targetPosition), pullSpeed);

                Tween moveTween = pulledCube.transform.DOMove(targetPosition, duration)
                    .SetEase(Ease.Linear)
                    .SetLink(pulledCube.gameObject);
                yield return moveTween.WaitForCompletion(true);

                // Once the pulled cube reaches the next hooked cube, hand the
                // rope chain over to it. Example A-B-C: C first retracts B-C;
                // at B, B-C is removed and A-B's B endpoint is retargeted to C,
                // so the remaining A-C rope now retracts during the next step.
                if (occupant != null && hookedCubes.Contains(occupant))
                {
                    if (activeRopeIndex >= 0 && activeRopeIndex < hookRopes.Count)
                    {
                        DestroyHookRope(hookRopes[activeRopeIndex]);
                        hookRopes[activeRopeIndex] = null;
                    }

                    activeRopeIndex += reversed ? 1 : -1;
                    if (activeRopeIndex >= 0 && activeRopeIndex < hookRopes.Count)
                    {
                        Rope nextRope = hookRopes[activeRopeIndex];
                        if (nextRope != null)
                        {
                            if (reversed)
                                nextRope.RetargetFirstCube(pulledCube.transform);
                            else
                                nextRope.RetargetSecondCube(pulledCube.transform);
                        }
                    }
                }

                // Only clear + drop this cell once the pulled cube has actually
                // reached it, so cubes above never fall through cells it hasn't
                // passed yet.
                _cubes[cellCol, cellRow] = null;
                if (occupant != null)
                {
                    PopAndDestroy(occupant);
                    DropColumn(cellCol);
                }
            }

            PopAndDestroy(pulledCube);

            // Keep the ropes alive during the pull so their kinematic anchors
            // follow the moving cube and make it look physically rope-driven.
            foreach (Rope hookRope in hookRopes)
                DestroyHookRope(hookRope);
        }

        private void DestroyHookRope(Rope rope)
        {
            if (rope == null) return;

            rope.Clear();
            Destroy(rope.gameObject);
        }

        private Rope SpawnHookRope(Cube firstCube, Cube secondCube)
        {
            if (ropeObjectPrefab == null)
            {
                Debug.LogWarning("[GridManager] Rope Object Prefab is not assigned.", this);
                return null;
            }

            GameObject ropeObject = Instantiate(ropeObjectPrefab, transform);
            ropeObject.name = $"Rope_{firstCube.Column}_{firstCube.Row}_to_{secondCube.Column}_{secondCube.Row}";

            Rope rope = ropeObject.GetComponent<Rope>();
            if (rope == null)
            {
                Debug.LogError(
                    $"[GridManager] Rope prefab '{ropeObjectPrefab.name}' has no Rope component.",
                    ropeObject);
                Destroy(ropeObject);
                return null;
            }

            // Segment Count on the Rope prefab is the density for adjacent
            // cubes. Reduce that density slightly over longer distances: with
            // base 6 and falloff 0.5 the totals are 1*6=6, 2*5.5=11,
            // 3*5=15, etc. Round because a physical segment count is integral.
            int columnDistance = Mathf.Abs(secondCube.Column - firstCube.Column);
            int rowDistance = Mathf.Abs(secondCube.Row - firstCube.Row);
            int gridDistance = Mathf.Max(1, columnDistance + rowDistance);
            int additionalCellSpans = gridDistance - 1;
            float minimumDensity = Mathf.Min(minimumRopeSegmentsPerCell, rope.SegmentCount);
            float segmentsPerCell = Mathf.Max(
                minimumDensity,
                rope.SegmentCount - additionalCellSpans * ropeSegmentsPerCellFalloff);
            int totalSegmentCount = Mathf.Max(
                2,
                Mathf.RoundToInt(gridDistance * segmentsPerCell));

            rope.Build(firstCube.transform, secondCube.transform, totalSegmentCount);
            return rope;
        }

        private IEnumerator DropColumnAfterDelay(int col, float delay)
        {
            yield return new WaitForSeconds(delay);
            DropColumn(col);
        }

        private void PopAndDestroy(Cube cube)
        {
            if (cube == null) return;

            OnCubeCleared?.Invoke(cube.ColorCode);

            cube.transform.DOKill();
            cube.transform.DOScale(Vector3.zero, destroyDuration)
                .SetEase(Ease.InBack)
                .SetLink(cube.gameObject)
                .OnComplete(() => Destroy(cube.gameObject));
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
                    float duration = CalculateMoveDuration(Vector3.Distance(cube.transform.position, targetPosition), dropSpeed);
                    cube.transform.DOKill();
                    // Linear, not InOutBack: this cube may get re-targeted by another
                    // DropColumn call before this tween finishes (a same-column cascade
                    // fires once per step), and InOutBack always ramps up from zero
                    // velocity - repeatedly restarting that ramp is what made cubes look
                    // like they stall then jump. Linear lets a redirect continue at the
                    // same speed instead of resetting it.
                    cube.transform.DOMove(targetPosition, duration)
                        .SetEase(Ease.Linear)
                        .SetLink(cube.gameObject);
                }

                writeRow--;
            }

            if (writeRow < 0) return; // column was already full

            // New fillers use dropSpeed (not initialDropSpeed) and Linear ease so they
            // fall at the same rate as the cubes above them being compacted down,
            // instead of the initial build's slower, ease-heavy reveal getting
            // constantly interrupted by the next cascade step.
            Vector2 cubeScale = CalculateCubeScale();
            for (int row = 0; row <= writeRow; row++)
                SpawnCube(col, row, ResolveColorCode("rand"), cubeScale, dropSpeed, Ease.Linear);
        }

        // Moving further should take longer, not the same time as a short move -
        // duration scales with distance at a constant speed, floored so very
        // short moves (e.g. shifting down one cell) don't look instant. Shared
        // by cube drops, column shifts, and the hook pull.
        private float CalculateMoveDuration(float distance, float speed)
        {
            if (speed <= 0f) return minDropDuration;
            return Mathf.Max(minDropDuration, distance / speed);
        }

        // Lets other UI (e.g. ScoreBoardController's goal badges) reuse the same
        // color -> sprite mapping instead of keeping a second copy of it.
        public Sprite GetNormalSprite(string colorCode)
        {
            return _colorDefinitions.TryGetValue(colorCode, out CubeColorDefinition definition)
                ? definition.normalSprite
                : null;
        }

        // "rand" needs to resolve to one concrete color at spawn time so the
        // spawned cube has a stable ColorCode for the hook to match against.
        // Picks from whatever colors are configured in the inspector, so a
        // newly added color is automatically eligible for random spawns.
        private string ResolveColorCode(string code)
        {
            if (code != "rand") return code;
            if (cubeColors == null || cubeColors.Length == 0) return code;

            return cubeColors[UnityEngine.Random.Range(0, cubeColors.Length)].code;
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
        // up/down to match the screen-fit cell size computed above, shrunk by the
        // configured padding so neighboring cubes leave a visible gap. The cell
        // size itself (and therefore cube spacing/positions) is untouched - only
        // the cube's own rendered size shrinks within its cell.
        private Vector2 CalculateCubeScale()
        {
            Vector2 nativeSize = DetermineNativePrefabSize();
            if (nativeSize.x <= 0f || nativeSize.y <= 0f) return Vector2.one;

            Vector2 targetSize = new Vector2(
                Mathf.Max(0f, _cellSize.x - horizontalCubePadding),
                Mathf.Max(0f, _cellSize.y - verticalCubePadding));

            return new Vector2(targetSize.x / nativeSize.x, targetSize.y / nativeSize.y);
        }

        private Vector2 DetermineNativePrefabSize()
        {
            if (cubePrefab == null) return Vector2.one;

            SpriteRenderer sr = cubePrefab.GetComponentInChildren<SpriteRenderer>();
            if (sr == null) return Vector2.one;

            // The shared prefab may not have a sprite of its own assigned -
            // fall back to the first configured color so sizing still works.
            Sprite sprite = sr.sprite != null ? sr.sprite
                : cubeColors is { Length: > 0 } ? cubeColors[0].normalSprite
                : null;
            if (sprite == null) return Vector2.one;

            Vector2 spriteSize = sprite.bounds.size;
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
