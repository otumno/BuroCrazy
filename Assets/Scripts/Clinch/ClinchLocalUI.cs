using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Clinch;

namespace Clinch
{
    public class ClinchLocalUI : MonoBehaviour
    {
        [Header("UI References")]
        public Image centerLock;
        public Button[] keyButtons = new Button[3];
        
        [Header("Sprites")]
        public Sprite[] lockSprites;  // 0-Красный, 1-Желтый, 2-Фиолетовый
        public Sprite[] keySprites;
        public Sprite successCenterSprite; // Спрайт успеха (+)
        public Sprite failCenterSprite;    // Спрайт провала (-)
        
        [Header("Звуки")]
        public Scriptables.Audio.SoundID successSound = Scriptables.Audio.SoundID.Success;
        public Scriptables.Audio.SoundID failSound = Scriptables.Audio.SoundID.Fail;
        
        private ClinchTarget currentTarget;
        private int correctKeyIndex;
        
        public void Show(Clinch.ClinchTarget target, int requiredColor)
        {
            currentTarget = target;
            correctKeyIndex = requiredColor;
            
            // Устанавливаем спрайт замка по индексу
            if (centerLock != null && lockSprites.Length > requiredColor)
            {
                centerLock.sprite = lockSprites[requiredColor];
            }
            
            // Перемешивание ключей (0, 1, 2) - Фишер-Йейтс
            List<int> colors = new List<int> { 0, 1, 2 };
            for (int i = 0; i < colors.Count; i++)
            {
                int temp = colors[i];
                int rand = UnityEngine.Random.Range(i, colors.Count);
                colors[i] = colors[rand];
                colors[rand] = temp;
            }
            
            for (int i = 0; i < keyButtons.Length; i++)
            {
                int colorID = colors[i];
                if (keySprites.Length > colorID)
                {
                    keyButtons[i].GetComponent<UnityEngine.UI.Image>().sprite = keySprites[colorID];
                }
                
                keyButtons[i].interactable = true;
                keyButtons[i].onClick.RemoveAllListeners();
                keyButtons[i].onClick.AddListener(() => StartCoroutine(ResolveRoutine(colorID)));
            }
            
            gameObject.SetActive(true);
        }
        
        private System.Collections.IEnumerator ResolveRoutine(int clickedColor)
        {
            foreach (var btn in keyButtons) btn.interactable = false;

            bool isCorrect = (clickedColor == correctKeyIndex);
            
            if (centerLock != null)
            {
                centerLock.sprite = isCorrect ? successCenterSprite : failCenterSprite;
            }

            if (Managers.AudioManager.Instance != null)
            {
                Managers.AudioManager.Instance.PlaySound(isCorrect ? successSound : failSound, transform.position);
            }

            // Задержка реального времени, чтобы игрок увидел результат (+ или -)
            yield return new WaitForSecondsRealtime(0.5f);

            gameObject.SetActive(false);
            Managers.MainUIManager.Instance?.PopPause();
            
            if (currentTarget != null)
            {
                currentTarget.ResolveClinch(isCorrect, false);
            }
        }
        
        public void Hide()
        {
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Отменяет мини-игру клинча (например, при таймауте).
        /// Вызывается из ClinchTarget или DirectorAvatarController.
        /// </summary>
        public void CancelMiniGame()
        {
            foreach (var btn in keyButtons)
                if (btn != null) btn.interactable = false;
            
            gameObject.SetActive(false);
            Managers.MainUIManager.Instance?.PopPause();
            if (currentTarget != null) currentTarget.CancelClinchUI();
        }
    }
}
