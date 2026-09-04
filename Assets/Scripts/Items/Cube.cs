using Core;
using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Items
{
    public class Cube : MonoBehaviour
    {
        [SerializeField] private float minFlickDistance = 50f;
        [SerializeField] private float shakeDuration = 0.3f;
        [SerializeField] private float shakeStrength = 0.15f;
        [SerializeField] private float vibrateDuration = 0.2f;
        [SerializeField] private float vibrateStrength = 0.08f;

        private bool isHooked { get; set; }

        public int Column { get; private set; }
        public int Row { get; private set; }
        public string ColorCode { get; private set; }

        private GridManager _gridManager;
        private Vector2 _pressScreenPosition;
        private SpriteRenderer _spriteRenderer;
        private Sprite _hookedSprite;

        private void Awake()
        {
            _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        // normalSprite/hookedSprite come from GridManager's per-color-code
        // definitions rather than a field on this prefab, so one Cube prefab
        // can be reused for every color instead of needing a prefab variant
        // per color.
        public void Initialize(GridManager gridManager, int column, int row, string colorCode, Sprite normalSprite, Sprite hookedSprite)
        {
            _gridManager = gridManager;
            Column = column;
            Row = row;
            ColorCode = colorCode;
            _hookedSprite = hookedSprite;

            if (_spriteRenderer != null && normalSprite != null)
                _spriteRenderer.sprite = normalSprite;
        }

        public void SetGridPosition(int column, int row)
        {
            Column = column;
            Row = row;
        }

        // Feedback for a flick that had no same-color cube to hook onto in that
        // direction - shakes in place rather than leaving the flick unacknowledged.
        public void Shake()
        {
            transform.DOKill();
            transform.DOShakePosition(shakeDuration, shakeStrength, vibrato: 20, randomness: 90, fadeOut: true)
                .SetLink(gameObject);
        }

        // Telegraph pulse marking this cube as part of an in-progress hook chain.
        // Shakes scale rather than position so it can freely overlap with the
        // pull movement that may start on this cube right after. Also swaps in
        // the hooked-look sprite so a vibrating cube reads as "caught" even
        // before it starts moving.
        public void Vibrate()
        {
            isHooked = true;
            if (_spriteRenderer != null && _hookedSprite != null)
                _spriteRenderer.sprite = _hookedSprite;

            transform.DOShakeScale(vibrateDuration, vibrateStrength, vibrato: 10, randomness: 90, fadeOut: true)
                .SetLink(gameObject);
        }

        private void OnMouseDown()
        {
            _pressScreenPosition = GetPointerScreenPosition();
        }

        private void OnMouseUp()
        {
            Vector2 delta = GetPointerScreenPosition() - _pressScreenPosition;
            if (delta.magnitude < minFlickDistance) return;

            // Snap the flick to whichever axis moved further, then to a single
            // grid step along that axis (row 0 is the top, so screen-up means
            // the row index decreases).
            Vector2Int direction = Mathf.Abs(delta.x) > Mathf.Abs(delta.y)
                ? new Vector2Int(delta.x > 0 ? 1 : -1, 0)
                : new Vector2Int(0, delta.y > 0 ? -1 : 1);

            _gridManager.RequestHookExplosion(this, direction);
        }

        private static Vector2 GetPointerScreenPosition()
        {
            return Pointer.current != null ? Pointer.current.position.ReadValue() : Vector2.zero;
        }
    }
}
