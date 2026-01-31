using System.Collections.Generic;
using UnityEngine;
using Characters;

namespace Characters
{
    public partial class CharacterVisuals : MonoBehaviour
    {
        private GameObject hairObject;
        private SpriteRenderer hairRenderer;

        public void SetupVisualDiversity(ClientArchetype archetype)
        {
            if (archetype == null)
            {
                Debug.LogWarning($"[{gameObject.name}] SetupVisualDiversity: archetype is null!");
                return;
            }

            Debug.Log($"[{gameObject.name}] SetupVisualDiversity: archetype={archetype.name}, groupID={archetype.groupID}, displayName={archetype.displayName}");

            // Тело - спрайт из архетипа
            if (archetype.bodySprite != null && bodyRenderer != null)
            {
                Debug.Log($"[{gameObject.name}] Setting body sprite from archetype: {archetype.bodySprite.name}");
                bodyRenderer.sprite = archetype.bodySprite;
            }
            else
            {
                Debug.LogWarning($"[{gameObject.name}] Body sprite NOT set: archetype.bodySprite={(archetype.bodySprite != null)}, bodyRenderer={(bodyRenderer != null)}");
            }

            // Портрет для диалогов
            if (archetype.portraitSprite != null)
            {
                assignedPortrait = archetype.portraitSprite;
                Debug.Log($"[{gameObject.name}] Portrait set: {archetype.portraitSprite.name}");
            }

            // Причёска - случайная из списка
            ApplyHairFromArchetype(archetype);

            // Одежда - случайная из списка
            ApplyOutfitFromArchetype(archetype);
        }

        private void ApplyHairFromArchetype(ClientArchetype archetype)
        {
            Sprite hairSprite = archetype.GetRandomHairSprite();
            Debug.Log($"[{gameObject.name}] ApplyHairFromArchetype: hairSprite={(hairSprite != null ? hairSprite.name : "NULL")}, count={archetype.hairSprites?.Count ?? 0}");

            if (hairSprite == null)
            {
                Debug.LogWarning($"[{gameObject.name}] No hair sprite in archetype!");
                return;
            }

            Color hairColor = archetype.GetRandomHairColor();
            Debug.Log($"[{gameObject.name}] Hair color: {hairColor}");

            if (hairObject != null)
            {
                Destroy(hairObject);
            }

            hairObject = new GameObject("Hair");
            hairObject.transform.SetParent(transform, false);
            hairObject.transform.localPosition = Vector3.zero;

            hairRenderer = hairObject.AddComponent<SpriteRenderer>();
            hairRenderer.sprite = hairSprite;
            hairRenderer.sortingOrder = GetHairSortingOrder();
            hairRenderer.color = hairColor;

            Debug.Log($"[{gameObject.name}] Hair created: sprite={hairSprite.name}, color={hairColor}");
        }

        private void ApplyOutfitFromArchetype(ClientArchetype archetype)
        {
            Sprite outfitSprite = archetype.GetRandomOutfitSprite();
            Debug.Log($"[{gameObject.name}] ApplyOutfitFromArchetype: outfitSprite={(outfitSprite != null ? outfitSprite.name : "NULL")}, count={archetype.outfitSprites?.Count ?? 0}");

            if (outfitSprite == null)
            {
                Debug.LogWarning($"[{gameObject.name}] No outfit sprite in archetype!");
                return;
            }

            // Try multiple search methods for OutfitOverlay
            Transform overlaySprite = transform.Find("VisualsContainer/OutfitOverlay");
            if (overlaySprite == null)
            {
                overlaySprite = transform.Find("OutfitOverlay");
            }
            if (overlaySprite == null)
            {
                // Deep search
                overlaySprite = FindDeepChild(transform, "OutfitOverlay");
            }

            Debug.Log($"[{gameObject.name}] OutfitOverlay search result: {(overlaySprite != null ? "FOUND at " + overlaySprite.name : "NOT FOUND")}");

            if (overlaySprite != null)
            {
                var overlayRenderer = overlaySprite.GetComponent<SpriteRenderer>();
                if (overlayRenderer != null)
                {
                    Color outfitColor = archetype.GetRandomOutfitColor();
                    overlayRenderer.sprite = outfitSprite;
                    overlayRenderer.color = outfitColor;
                    Debug.Log($"[{gameObject.name}] Outfit applied: sprite={outfitSprite.name}, color={outfitColor}");
                }
                else
                {
                    Debug.LogWarning($"[{gameObject.name}] OutfitOverlay has no SpriteRenderer!");
                }
            }
            else
            {
                Debug.LogWarning($"[{gameObject.name}] OutfitOverlay not found! Creating dynamic outfit...");
                CreateDynamicOutfit(archetype, outfitSprite);
            }
        }

        private void CreateDynamicOutfit(ClientArchetype archetype, Sprite outfitSprite)
        {
            var outfitObject = new GameObject("OutfitOverlay");
            outfitObject.transform.SetParent(transform.Find("VisualsContainer"), false);
            outfitObject.transform.localPosition = Vector3.zero;

            var outfitRenderer = outfitObject.AddComponent<SpriteRenderer>();
            outfitRenderer.sprite = outfitSprite;
            outfitRenderer.color = archetype.GetRandomOutfitColor();
            outfitRenderer.sortingOrder = 1;

            Debug.Log($"[{gameObject.name}] Dynamic outfit created: sprite={outfitSprite.name}, color={outfitRenderer.color}");
        }

            var overlaySprite = transform.Find("VisualsContainer/OutfitOverlay");
            Debug.Log($"[{gameObject.name}] OutfitOverlay search result: {(overlaySprite != null ? "FOUND" : "NOT FOUND")}");

            if (overlaySprite != null)
            {
                var overlayRenderer = overlaySprite.GetComponent<SpriteRenderer>();
                if (overlayRenderer != null)
                {
                    Color outfitColor = archetype.GetRandomOutfitColor();
                    overlayRenderer.sprite = outfitSprite;
                    overlayRenderer.color = outfitColor;
                    Debug.Log($"[{gameObject.name}] Outfit applied: sprite={outfitSprite.name}, color={outfitColor}");
                }
                else
                {
                    Debug.LogWarning($"[{gameObject.name}] OutfitOverlay has no SpriteRenderer!");
                }
            }
            else
            {
                Debug.LogWarning($"[{gameObject.name}] OutfitOverlay not found under VisualsContainer!");
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
    }
}
