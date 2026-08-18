using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace UI.Menu
{
    public class LevelButton : MonoBehaviour
    {
        [Header("Animation Settings")]
        [SerializeField] private float _exitDuration = 0.5f;

        [Header("Text Settings")]
        [SerializeField] private TextMeshProUGUI levelText;

        private RectTransform _rectTransform;
        private Vector2 _originalAnchoredPosition;

        private void Awake()
        {
            
            _rectTransform = GetComponent<RectTransform>();
            _originalAnchoredPosition = _rectTransform.anchoredPosition;
        }

        private void OnEnable()
        {
            // if (LevelManager.Instance != null)
            // {
            //     LevelManager.Instance.OnLevelChanged += UpdateUI;
            // UpdateUI();
            // GetButton();
            // }
        }

        private void OnDisable()
        {
            // if (LevelManager.Instance != null)
            //     LevelManager.Instance.OnLevelChanged -= UpdateUI;
        }

        private void OnPointerClick(PointerEventData eventData)
        {
            if (levelText.text  == "Finished") return;
            
            _rectTransform.DOAnchorPosY(-Screen.height, _exitDuration)
                .SetEase(Ease.InBack)
                .OnComplete(() =>
                {
                    // GameStateManager.Instance.ChangeState(GameState.GameStarted);
                    gameObject.SetActive(false);
                })
                .SetLink(gameObject);
        }

        public Tween GetButton()
        {
            // int currentLevel = LevelManager.Instance.CurrentLevel;
            // int maxLevel = LevelManager.Instance.MaxLevel;
            // levelText.text = currentLevel > maxLevel ? "Finished" : $"Level {currentLevel}";
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
            // int currentLevel = LevelManager.Instance.CurrentLevel;
            // int maxLevel = LevelManager.Instance.MaxLevel;
            // levelText.text = currentLevel > maxLevel ? "Finished" : $"Level {currentLevel}";
            
        }
    }
}