using System.Collections.Generic;
using UnityEngine;
using Characters;

namespace Characters
{
    public partial class CharacterVisuals : MonoBehaviour
    {
        private GameObject hairObject;
        private SpriteRenderer hairRenderer;
        private GameObject outfitObject;
        private SpriteRenderer outfitRenderer;

        public void SetupVisualDiversity(ClientArchetype archetype)
        {
            if (archetype == null)
            {
                Debug.LogWarning($"[{gameObject.name}] SetupVisualDiversity: archetype is null!");
                return;
            }

            Debug.Log($"[{gameObject.name}] SetupVisualDiversity: archetype={archetype.name}, groupID={archetype.groupID}, displayName={archetype.displayName}");

            if (bodyRenderer == null)
            {
                Debug.LogError($"[{gameObject.name}] bodyRenderer is null! Cannot setup visual diversity.");
                return;
            }

            // Тело - спрайт из архетипа
            if (archetype.bodySprite != null)
            {
                Debug.Log($"[{gameObject.name}] Setting body sprite from archetype: {archetype.bodySprite.name}");
                bodyRenderer.sprite = archetype.bodySprite;
            }
            else
            {
                Debug.LogWarning($"[{gameObject.name}] Body sprite NOT set: archetype.bodySprite is null!");
            }

            // Портрет для диалогов
            if (archetype.portraitSprite != null)
            {
                assignedPortrait = archetype.portraitSprite;
                Debug.Log($"[{gameObject.name}] Portrait set: {archetype.portraitSprite.name}");
            }

            // Одежда - СНАЧАЛА чтобы Hair мог получить правильную сортировку
            ApplyOutfitFromArchetype(archetype);

            // Причёска - ПОТОМ чтобы наследовать сортировку от обновлённого OutfitOverlay
            ApplyHairFromArchetype(archetype);

            // Face - ПОСЛЕДНИМ чтобы быть поверх волос
            ApplyFaceSortingFromHair();

            // Примечание: эмоции лица устанавливаются через visuals.Setup() в Initialize()
            // который вызывает SetEmotion(Neutral). Для работы эмоций нужен currentSpriteCollection.
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

            // Родитель - VisualsContainer
            Transform visualsContainer = transform.Find("VisualsContainer");
            if (visualsContainer != null)
            {
                hairObject.transform.SetParent(visualsContainer, false);
            }
            else
            {
                hairObject.transform.SetParent(transform, false);
                Debug.LogWarning($"[{gameObject.name}] VisualsContainer not found! Using root.");
            }

            hairObject.transform.localPosition = Vector3.zero;

            hairRenderer = hairObject.AddComponent<SpriteRenderer>();
            hairRenderer.sprite = hairSprite;
            hairRenderer.color = hairColor;

            // Наследуем sorting от OutfitOverlay (который уже обновлён)
            // СНАЧАЛА ищем существующий OutfitOverlay
            Transform outfitOverlayForSorting = transform.Find("VisualsContainer/OutfitOverlay");
            if (outfitOverlayForSorting == null)
            {
                outfitOverlayForSorting = transform.Find("OutfitOverlay");
            }
            if (outfitOverlayForSorting == null)
            {
                outfitOverlayForSorting = FindDeepChild(transform, "OutfitOverlay");
            }

            string outfitLayer = bodyRenderer != null ? bodyRenderer.sortingLayerName : "Default";
            int outfitOrder = bodyRenderer != null ? bodyRenderer.sortingOrder : 0;

            if (outfitOverlayForSorting != null)
            {
                var outfitRendererForSorting = outfitOverlayForSorting.GetComponent<SpriteRenderer>();
                if (outfitRendererForSorting != null)
                {
                    outfitLayer = outfitRendererForSorting.sortingLayerName;
                    outfitOrder = outfitRendererForSorting.sortingOrder;
                }
            }

            hairRenderer.sortingLayerName = outfitLayer;
            hairRenderer.sortingOrder = outfitOrder + 1;

            Debug.Log($"[{gameObject.name}] Hair created: sorting from OutfitOverlay: layer={outfitLayer}, order={outfitOrder}, hairOrder={hairRenderer.sortingOrder}");
        }

        private void ApplyFaceSortingFromHair()
        {
            if (faceRenderer == null || hairRenderer == null) return;

            // Face должен быть поверх волос
            faceRenderer.sortingLayerName = hairRenderer.sortingLayerName;
            faceRenderer.sortingOrder = hairRenderer.sortingOrder + 1;

            Debug.Log($"[{gameObject.name}] Face sorting set: layer={faceRenderer.sortingLayerName}, order={faceRenderer.sortingOrder} (above hair)");
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

            // СНАЧАЛА ищем существующий OutfitOverlay
            Transform existingOverlay = transform.Find("VisualsContainer/OutfitOverlay");
            if (existingOverlay == null)
            {
                existingOverlay = transform.Find("OutfitOverlay");
            }
            if (existingOverlay == null)
            {
                existingOverlay = FindDeepChild(transform, "OutfitOverlay");
            }

            Debug.Log($"[{gameObject.name}] Existing OutfitOverlay search: {(existingOverlay != null ? "FOUND at " + GetFullPath(existingOverlay) : "NOT FOUND")}");

            if (existingOverlay != null)
            {
                // Получаем сортировку из существующего
                var existingRenderer = existingOverlay.GetComponent<SpriteRenderer>();
                string outfitLayer = "Default";
                int outfitOrder = 0;

                if (existingRenderer != null)
                {
                    outfitLayer = existingRenderer.sortingLayerName;
                    outfitOrder = existingRenderer.sortingOrder;
                    Debug.Log($"[{gameObject.name}] Original OutfitOverlay sorting: layer={outfitLayer}, order={outfitOrder}");
                }

                // Обновляем существующий OutfitOverlay
                outfitRenderer = existingRenderer;
                outfitRenderer.sprite = outfitSprite;
                outfitRenderer.color = archetype.GetRandomOutfitColor();
                outfitRenderer.sortingLayerName = outfitLayer;
                outfitRenderer.sortingOrder = outfitOrder;

                Debug.Log($"[{gameObject.name}] OutfitOverlay updated: sprite={outfitSprite.name}, layer={outfitLayer}, order={outfitOrder}");
            }
            else
            {
                // Создаем новый если нет
                Debug.LogWarning($"[{gameObject.name}] OutfitOverlay NOT FOUND, creating new one!");

                if (outfitObject != null)
                {
                    Destroy(outfitObject);
                }

                outfitObject = new GameObject("OutfitOverlay");

                Transform visualsContainer = transform.Find("VisualsContainer");
                if (visualsContainer != null)
                {
                    outfitObject.transform.SetParent(visualsContainer, false);
                }
                else
                {
                    outfitObject.transform.SetParent(transform, false);
                }

                outfitObject.transform.localPosition = Vector3.zero;

                outfitRenderer = outfitObject.AddComponent<SpriteRenderer>();
                outfitRenderer.sprite = outfitSprite;
                outfitRenderer.color = archetype.GetRandomOutfitColor();
                outfitRenderer.sortingLayerName = bodyRenderer != null ? bodyRenderer.sortingLayerName : "Default";
                outfitRenderer.sortingOrder = bodyRenderer != null ? bodyRenderer.sortingOrder + 1 : 1;

                Debug.Log($"[{gameObject.name}] OutfitOverlay created: parent={visualsContainer?.name ?? "root"}, sprite={outfitSprite.name}, layer={outfitRenderer.sortingLayerName}, order={outfitRenderer.sortingOrder}");
            }
        }

        private string GetFullPath(Transform t)
        {
            string path = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }
            return path;
        }

        // OLD METHOD - DEPRECATED
        // private void GetOutfitSorting(SpriteRenderer currentOutfit, out string layer, out int order)
        // {
        //     // По умолчанию из bodyRenderer
        //     layer = bodyRenderer != null ? bodyRenderer.sortingLayerName : "Default";
        //     order = bodyRenderer != null ? bodyRenderer.sortingOrder : 0;
        //
        //     // Ищем OutfitOverlay для получения правильных настроек
        //     Transform overlay = transform.Find("VisualsContainer/OutfitOverlay");
        //     if (overlay == null)
        //     {
        //         overlay = transform.Find("OutfitOverlay");
        //     }
        //     if (overlay == null)
        //     {
        //         overlay = FindDeepChild(transform, "OutfitOverlay");
        //     }
        //
        //     if (overlay != null)
        //     {
        //         var renderer = overlay.GetComponent<SpriteRenderer>();
        //         if (renderer != null)
        //         {
        //             layer = renderer.sortingLayerName;
        //             order = renderer.sortingOrder;
        //         }
        //     }
        // }
    }
}
