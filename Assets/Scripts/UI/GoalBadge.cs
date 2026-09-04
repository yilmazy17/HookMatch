using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// One scoreboard goal slot: shows a target color's icon and how many of
    /// that color are still needed. Reused for every color - ScoreBoardController
    /// instantiates one per entry in the level's target_cubes.
    /// </summary>
    public class GoalBadge : MonoBehaviour
    {
        [SerializeField] private Image frameImage;
        [SerializeField] private Image goalIcon;
        [SerializeField] private GameObject numCard;
        [SerializeField] private TextMeshProUGUI countText;
        [SerializeField] private Sprite unfinishedFrame;
        [SerializeField] private Sprite finishedFrame;

        public string ColorCode { get; private set; }
        public bool IsFinished { get; private set; }

        private int _remaining;

        public void Initialize(string colorCode, int targetCount, Sprite icon)
        {
            ColorCode = colorCode;
            _remaining = targetCount;
            IsFinished = false;

            if (goalIcon != null) goalIcon.sprite = icon;
            if (frameImage != null) frameImage.sprite = unfinishedFrame;
            if (numCard != null) numCard.SetActive(true);
            if (countText != null) countText.text = _remaining.ToString();
        }

        // Called by ScoreBoardController for every cube GridManager reports as
        // cleared; no-ops once finished or for colors this badge doesn't track.
        public void NotifyCubeCleared(string colorCode)
        {
            if (IsFinished || colorCode != ColorCode) return;

            _remaining = Mathf.Max(0, _remaining - 1);
            if (countText != null) countText.text = _remaining.ToString();

            if (_remaining == 0) MarkFinished();
        }

        private void MarkFinished()
        {
            IsFinished = true;
            if (frameImage != null) frameImage.sprite = finishedFrame;
            if (numCard != null) numCard.SetActive(false);
        }
    }
}
