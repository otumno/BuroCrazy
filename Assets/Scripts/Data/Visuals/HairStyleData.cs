using System.Collections.Generic;
using UnityEngine;

namespace Data.Visuals
{
    [CreateAssetMenu(fileName = "HairStyleData", menuName = "Bureau/Visuals/Hair Style")]
    public class HairStyleData : ScriptableObject
    {
        [Header("Идентификация")]
        public string hairID;
        public string displayName;

        [Header("Визуал")]
        public Sprite frontSprite;
        public Sprite backSprite;

        [Header("Параметры")]
        public bool isLong = false;
        public bool requiresHatCompatibility = false;

        [Header("Доступность по архетипу")]
        public List<string> allowedArchetypeIDs = new List<string>();

        [Header("Цветовые варианты")]
        public List<Color> allowedColors = new List<Color>
        {
            new Color(0.2f, 0.1f, 0f),    // Черный
            new Color(0.4f, 0.25f, 0.1f), // Темно-коричневый
            new Color(0.6f, 0.4f, 0.2f),  // Коричневый
            new Color(0.8f, 0.7f, 0.5f),  // Светлый
            new Color(0.9f, 0.9f, 0.85f), // Белый/седой
            new Color(0.5f, 0.3f, 0.2f),  // Рыжий
        };

        [Header("Ограничения")]
        public bool allowRandomTint = true;
        [Tooltip("Если false - только оригинальный цвет спрайта")]
        public bool allowColorReplacement = true;

        public Color GetRandomColor()
        {
            if (allowedColors == null || allowedColors.Count == 0)
            {
                return Color.black;
            }
            return allowedColors[Random.Range(0, allowedColors.Count)];
        }

        public bool CanBeUsedForArchetype(string archetypeID)
        {
            if (allowedArchetypeIDs == null || allowedArchetypeIDs.Count == 0)
            {
                return true; // Нет ограничений
            }
            return allowedArchetypeIDs.Contains(archetypeID);
        }
    }
}
