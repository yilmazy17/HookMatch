using DG.Tweening;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// Slides the scoreboard down from above the screen each time it's
    /// activated (i.e. whenever the InGame panel is shown).
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class ScoreBoardIntro : MonoBehaviour
    {
        [SerializeField] private float slideInDuration = 0.5f;

        private RectTransform _rectTransform;
        private Vector2 _restingAnchoredPosition;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _restingAnchoredPosition = _rectTransform.anchoredPosition;
        }

        private void OnEnable()
        {
            _rectTransform.DOKill();
            _rectTransform.anchoredPosition =
                new Vector2(_restingAnchoredPosition.x, _restingAnchoredPosition.y + Screen.height);

            _rectTransform.DOAnchorPos(_restingAnchoredPosition, slideInDuration)
                .SetEase(Ease.OutBack)
                .SetLink(gameObject);
        }
    }
}
