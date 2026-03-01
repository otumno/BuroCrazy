using UnityEngine;
using DG.Tweening;
using Scriptables.Audio;

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

        [Header("Глобальные звуки окон")]
        public SoundID defaultOpenSound = SoundID.UI_Popup_Open;
        public SoundID defaultCloseSound = SoundID.UI_Click_Default;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }
    }
}
