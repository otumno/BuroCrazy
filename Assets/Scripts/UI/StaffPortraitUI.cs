using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// Портрет сотрудника в UI: слои тела, одежды, волос, лица и аксессуара копируются с его спрайтов в мире.
    /// </summary>
    // Та же логика есть в TeamMemberCardUI.UpdatePortrait. Её не трогаем, чтобы не менять префаб карточки;
    // новый UI собирает портрет этим компонентом.
    public class StaffPortraitUI : MonoBehaviour
    {
        [SerializeField] private Image body;
        [SerializeField] private Image outfit;
        [SerializeField] private Image hair;
        [SerializeField] private Image face;
        [SerializeField] private Image accessory;

        public void Show(StaffController staff)
        {
            var visuals = staff != null ? staff.visuals : null;
            if (visuals == null)
            {
                SetSprite(body, null, Color.white);
                SetSprite(outfit, null, Color.white);
                SetSprite(hair, null, Color.white);
                SetSprite(face, null, Color.white);
                SetSprite(accessory, null, Color.white);
                return;
            }

            CopyRenderer(body, visuals.GetBodyRenderer());
            CopyRenderer(outfit, visuals.GetOutfitRenderer());
            CopyRenderer(hair, visuals.GetHairRenderer());
            CopyRenderer(accessory, visuals.GetAccessoryRenderer());

            // Лицо берём нейтральное: в мире эмоция меняется постоянно, а в шапке меню она только отвлекает.
            Sprite faceSprite = null;
            if (visuals.currentSpriteCollection != null)
            {
                faceSprite = visuals.currentSpriteCollection.GetFaceSprite(Emotion.Neutral, staff.gender);
            }
            SetSprite(face, faceSprite, Color.white);
        }

        private static void CopyRenderer(Image image, SpriteRenderer source)
        {
            if (source == null)
            {
                SetSprite(image, null, Color.white);
                return;
            }

            // Цвет копируем вместе со спрайтом: волосы и одежда перекрашиваются через него.
            SetSprite(image, source.sprite, source.color);
        }

        private static void SetSprite(Image image, Sprite sprite, Color color)
        {
            if (image == null) return;

            image.enabled = sprite != null;
            if (sprite == null) return;

            image.sprite = sprite;
            image.color = color;
        }
    }
}
