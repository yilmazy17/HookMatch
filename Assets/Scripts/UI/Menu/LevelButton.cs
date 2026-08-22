using Core;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace UI.Menu
{
    public class LevelButton : MonoBehaviour, IPointerClickHandler
    {
        [Header("Animation Settings")]
        [SerializeField] private float _exitDuration = 0.5f;
        [SerializeField] private float _clickScaleAmount = 0.9f;
        [SerializeField] private float _clickScaleDuration = 0.08f;

        [Header("Text Settings")]
        [SerializeField] private TextMeshProUGUI levelText;

        [Header("Grid")]
        [SerializeField] private GridManager gridManager;

        private RectTransform _rectTransform;
        private Vector2 _originalAnchoredPosition;

        private void Awake()
        {
            
            _rectTransform = GetComponent<RectTransform>();
            _originalAnchoredPosition = _rectTransform.anchoredPosition;
        }

        private void OnEnable()
        {
            if (LevelManager.Instance != null)
                LevelManager.Instance.OnLevelChanged += HandleLevelChanged;

            UpdateUI();
        }

        private void OnDisable()
        {
            if (LevelManager.Instance != null)
                LevelManager.Instance.OnLevelChanged -= HandleLevelChanged;
        }

        private void HandleLevelChanged(int level) => UpdateUI();

        public void OnPointerClick(PointerEventData eventData)
        {
            if (levelText.text  == "Finished") return;

            _rectTransform.DOKill();
            DOTween.Sequence()
                .Append(_rectTransform.DOScale(_clickScaleAmount, _clickScaleDuration).SetEase(Ease.OutQuad))
                .Append(_rectTransform.DOScale(1f, _clickScaleDuration).SetEase(Ease.OutQuad))
                .AppendCallback(() =>
                {
                    _rectTransform.DOAnchorPosY(-Screen.height, _exitDuration)
                        .SetEase(Ease.InBack)
                        .OnComplete(() =>
                        {
                            if (gridManager != null) gridManager.StartLevel();
                            gameObject.SetActive(false);
                        })
                        .SetLink(gameObject);
                })
                .SetLink(gameObject);
        }

        public Tween GetButton()
        {
            UpdateUI();
            gameObject.SetActive(true);
        
            if (_rectTransform == null)
            {
                _rectTransform = GetComponent<RectTransform>();
                _originalAnchoredPosition = _rectTransform.anchoredPosition;
            }
        
            _rectTransform.anchoredPosition = new Vector2(_originalAnchoredPosition.x, -Screen.height);
            return _rectTransform.DOAnchorPos(_originalAnchoredPosition, _exitDuration)
                .SetEase(Ease.OutBack)
                .SetLink(gameObject);
        }
        //
        // public Tween CloseButton()
        // {
        //     _rectTransform.DOKill();
        //     return _rectTransform.DOAnchorPosY(-Screen.height, _exitDuration)
        //         .SetEase(Ease.InBack)
        //         .OnComplete(() => gameObject.SetActive(false))
        //         .SetLink(gameObject);
        // }

        public void UpdateUI()
        {
            if (LevelManager.Instance == null || levelText == null) return;

            int currentLevel = LevelManager.Instance.CurrentLevel;
            int maxLevel = LevelManager.Instance.MaxLevel;
            levelText.text = currentLevel > maxLevel ? "Finished" : $"Level {currentLevel}";
        }
    }
}