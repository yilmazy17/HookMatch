using System;
using Core;
using UnityEngine;

namespace UI
{
    public enum UIPanelId
    {
        Home,
        Journey,
        Settings,
        Shop,
        LeaderBoard,
        InGame
    }

    /// <summary>
    /// Single source of truth for which top-level panel is visible. Panels are
    /// assigned in the Inspector; showing one hides all the others.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        [Serializable]
        private struct Panel
        {
            public UIPanelId Id;
            public GameObject Root;
        }

        [SerializeField] private Panel[] panels;
        [SerializeField] private UIPanelId defaultPanel = UIPanelId.Home;

        public static UIManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnEnable() => GridManager.OnGameStarted += HandleGameStarted;

        private void OnDisable() => GridManager.OnGameStarted -= HandleGameStarted;

        private void Start() => Show(defaultPanel);

        private void HandleGameStarted() => Show(UIPanelId.InGame);

        public void Show(UIPanelId id)
        {
            foreach (Panel panel in panels)
            {
                if (panel.Root != null)
                    panel.Root.SetActive(panel.Id == id);
            }
        }
    }
}
