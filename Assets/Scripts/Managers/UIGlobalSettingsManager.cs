using UnityEngine;
using DG.Tweening;

namespace Managers
{
    public class UIGlobalSettingsManager : MonoBehaviour
    {
        public static UIGlobalSettingsManager Instance { get; private set; }

        [Header("Глобальные настройки UI анимаций")]
        public float globalDuration = 0.25f;
        public float globalStartScale = 0.95f;
        public Ease globalShowEase = Ease.OutBack;
        public Ease globalHideEase = Ease.InCubic;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }
    }
}
