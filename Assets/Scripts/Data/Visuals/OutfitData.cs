using System.Collections.Generic;
using UnityEngine;
using Characters;

namespace Data.Visuals
{
    [CreateAssetMenu(fileName = "OutfitData", menuName = "Bureau/Visuals/Outfit")]
    public class OutfitData : ScriptableObject
    {
        [Header("Идентификация")]
        public string outfitID;
        public string displayName;

        [Header("Тип одежды")]
        public OutfitType outfitType;
        public Gender gender;

        [Header("Визуал")]
        public Sprite bodySprite;
        public Sprite overlaySprite;

        [Header("Параметры")]
        [Tooltip("Цвет по умолчанию")]
        public Color baseColor = Color.white;

        [Tooltip("Доступные цвета для рандомизации")]
        public List<Color> allowedColors = new List<Color>
        {
            new Color(0.3f, 0.3f, 0.4f),  // Темно-синий
            new Color(0.5f, 0.5f, 0.5f),  // Серый
            new Color(0.6f, 0.5f, 0.4f),  // Коричневый
            new Color(0.2f, 0.3f, 0.2f),  // Темно-зеленый
            new Color(0.9f, 0.9f, 0.9f),  // Белый
            new Color(0.1f, 0.1f, 0.15f), // Почти черный
        };

        [Header("Ограничения по архетипу")]
        public List<string> allowedArchetypeIDs = new List<string>();

        [Tooltip("Если true - можно перекрашивать в любой цвет из allowedColors")]
        public bool allowColorRandomization = true;

        [Tooltip("Минимальный и максимальный уровень износа (0-1)")]
        [Range(0f, 1f)]
        public float wearLevel = 0.2f;

        public Color GetRandomColor()
        {
            if (allowedColors == null || allowedColors.Count == 0)
            {
                return baseColor;
            }
            return allowedColors[Random.Range(0, allowedColors.Count)];
        }

        public bool CanBeUsedForArchetype(string archetypeID)
        {
            if (allowedArchetypeIDs == null || allowedArchetypeIDs.Count == 0)
            {
                return true;
            }
            return allowedArchetypeIDs.Contains(archetypeID);
        }
    }
}
