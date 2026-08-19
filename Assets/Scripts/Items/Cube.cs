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

        public int Column { get; private set; }
        public int Row { get; private set; }
        public string ColorCode { get; private set; }

        private GridManager _gridManager;
        private Vector2 _pressScreenPosition;

        public void Initialize(GridManager gridManager, int column, int row, string colorCode)
        {
            _gridManager = gridManager;
            Column = column;
            Row = row;
            ColorCode = colorCode;
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
