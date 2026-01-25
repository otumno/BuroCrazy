using System.Collections.Generic;
using UnityEngine;
using Data.Visuals;
using Characters;

namespace Characters
{
    public partial class CharacterVisuals : MonoBehaviour
    {
        [Header("Визуальное разнообразие")]
        [Tooltip("База данных причесок")]
        public HairStyleDatabase hairDatabase;

        [Tooltip("База данных одежды")]
        public OutfitDatabase outfitDatabase;

        [Tooltip("Текущая прическа")]
        public HairStyleData currentHair;

        [Tooltip("Текущая одежда")]
        public OutfitData currentOutfit;

        private GameObject hairObject;
        private SpriteRenderer hairRenderer;

        public void SetupVisualDiversity(ClientArchetype archetype)
        {
            if (archetype == null) return;

            SetupHair(archetype);
            SetupOutfit(archetype);
        }

        private void SetupHair(ClientArchetype archetype)
        {
            if (hairDatabase == null || hairDatabase.allHairStyles == null || hairDatabase.allHairStyles.Count == 0)
            {
                return;
            }

            var availableHair = new List<HairStyleData>();

            foreach (var hair in hairDatabase.allHairStyles)
            {
                if (hair != null && hair.CanBeUsedForArchetype(archetype.archetypeID))
                {
                    availableHair.Add(hair);
                }
            }

            if (availableHair.Count == 0)
            {
                availableHair.AddRange(hairDatabase.allHairStyles);
            }

            if (availableHair.Count > 0)
            {
                currentHair = availableHair[Random.Range(0, availableHair.Count)];
                ApplyHair(currentHair, archetype);
            }
        }

        private void ApplyHair(HairStyleData hairData, ClientArchetype archetype)
        {
            if (hairData == null) return;

            if (hairObject != null)
            {
                Destroy(hairObject);
            }

            if (hairData.frontSprite != null || hairData.backSprite != null)
            {
                hairObject = new GameObject("Hair");
                hairObject.transform.SetParent(transform, false);

                hairRenderer = hairObject.AddComponent<SpriteRenderer>();

                if (hairData.frontSprite != null)
                {
                    hairRenderer.sprite = hairData.frontSprite;
                }

                hairRenderer.sortingOrder = GetHairSortingOrder();
                hairRenderer.color = hairData.GetRandomColor();
            }
        }

        private int GetHairSortingOrder()
        {
            var bodySprite = GetComponent<SpriteRenderer>();
            if (bodySprite != null)
            {
                return bodySprite.sortingOrder + 1;
            }
            return 100;
        }

        private void SetupOutfit(ClientArchetype archetype)
        {
            if (outfitDatabase == null || outfitDatabase.allOutfits == null || outfitDatabase.allOutfits.Count == 0)
            {
                return;
            }

            var currentGender = GetGender();
            var availableOutfits = new List<OutfitData>();

            foreach (var outfit in outfitDatabase.allOutfits)
            {
                if (outfit != null &&
                    outfit.CanBeUsedForArchetype(archetype.archetypeID) &&
                    (outfit.gender == currentGender || outfit.gender == (Gender)99)) // 99 = Undefined in some enums
                {
                    availableOutfits.Add(outfit);
                }
            }

            if (availableOutfits.Count > 0)
            {
                currentOutfit = availableOutfits[Random.Range(0, availableOutfits.Count)];
                ApplyOutfit(currentOutfit);
            }
        }

        private Gender GetGender()
        {
            // Try to get gender from the main CharacterVisuals class
            var genderField = typeof(CharacterVisuals).GetField("gender", 
                System.Reflection.BindingFlags.NonPublic | 
                System.Reflection.BindingFlags.Instance);
            
            if (genderField != null)
            {
                return (Gender)genderField.GetValue(this);
            }
            
            return Gender.Male; // Default
        }

        private void ApplyOutfit(OutfitData outfitData)
        {
            if (outfitData == null) return;

            var overlaySprite = transform.Find("OutfitOverlay");
            if (overlaySprite != null)
            {
                var overlayRenderer = overlaySprite.GetComponent<SpriteRenderer>();
                if (overlayRenderer != null && outfitData.overlaySprite != null)
                {
                    overlayRenderer.sprite = outfitData.overlaySprite;
                    overlayRenderer.color = outfitData.GetRandomColor();
                }
            }
        }

        public void RandomizeAppearance()
        {
            if (hairDatabase != null)
            {
                var randomHair = hairDatabase.allHairStyles[Random.Range(0, hairDatabase.allHairStyles.Count)];
                currentHair = randomHair;
            }

            if (outfitDatabase != null)
            {
                var randomOutfit = outfitDatabase.allOutfits[Random.Range(0, outfitDatabase.allOutfits.Count)];
                currentOutfit = randomOutfit;
            }
        }
    }
}
