using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace BuroDebug
{
    /// <summary>
    /// Утилита для настройки визуальной системы клиентов
    /// </summary>
    public class ClientVisualSetup : MonoBehaviour
    {
        [Header("Префаб клиента")]
        public GameObject clientPrefab;

        [Header("Настройки для теста")]
        public Sprite testHairSprite;
        public Color testHairColor = new Color(0.65f, 0.4f, 0.2f);
        public Sprite testOutfitSprite;
        public Color testOutfitColor = Color.gray;

        [ContextMenu("Setup Client Prefab")]
        public void SetupClientPrefab()
        {
            if (clientPrefab == null)
            {
                Debug.LogError("Префаб клиента не назначен!");
                return;
            }

            // 1. Добавляем OutfitOverlay если нет
            var outfitOverlay = clientPrefab.transform.Find("OutfitOverlay");
            if (outfitOverlay == null)
            {
                var overlayGO = new GameObject("OutfitOverlay");
                overlayGO.transform.SetParent(clientPrefab.transform, false);
                overlayGO.transform.localPosition = Vector3.zero; // совпадает с телом

                var spriteRenderer = overlayGO.AddComponent<SpriteRenderer>();
                spriteRenderer.sortingOrder = 1; // над телом
                spriteRenderer.color = Color.white;

                Debug.Log("✓ OutfitOverlay создан на префабе");
            }
            else
            {
                Debug.Log("✓ OutfitOverlay уже есть");
            }

            // 2. Добавляем CharacterVisuals если нет
            var visuals = clientPrefab.GetComponent<Characters.CharacterVisuals>();
            if (visuals == null)
            {
                clientPrefab.AddComponent<Characters.CharacterVisuals>();
                Debug.Log("✓ CharacterVisuals добавлен");
            }

            // 3. Добавляем CharacterVisuals (Diversity часть) если нет
            var visualsDiversity = clientPrefab.GetComponent<Characters.CharacterVisuals>();
            if (visualsDiversity == null)
            {
                clientPrefab.AddComponent<Characters.CharacterVisuals>();
                Debug.Log("✓ CharacterVisuals (Diversity) добавлен");
            }

            // 4. Настраиваем ссылки на базы данных (только в редакторе)
#if UNITY_EDITOR
            SetupDatabases();
#endif

            Debug.Log("Настройка префаба завершена!");
        }

#if UNITY_EDITOR
        private void SetupDatabases()
        {
            // Ищем или создаём Resources папку
            string resourcesPath = "Assets/Resources";
            if (!AssetDatabase.IsValidFolder(resourcesPath))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }

            string databasesPath = "Assets/Resources/Databases";
            if (!AssetDatabase.IsValidFolder(databasesPath))
            {
                AssetDatabase.CreateFolder("Assets", "Resources/Databases");
            }

            Debug.Log("Создайте архетипы в Assets/Resources/Databases/:");
            Debug.Log("- ArchetypeDatabase: Create → Bureau/Databases/Archetype Database");
            Debug.Log("- ClientArchetype: Create → Bureau/Characters/Client Archetype");
        }
#endif

        [ContextMenu("Test Hair Setup")]
        public void TestHairSetup()
        {
            if (clientPrefab == null)
            {
                Debug.LogError("Префаб не назначен!");
                return;
            }

            // Создаём тестовый клиент
            GameObject testClient = Instantiate(clientPrefab, Vector3.zero, Quaternion.identity);
            var visuals = testClient.GetComponent<Characters.CharacterVisuals>();

            if (visuals != null && testHairSprite != null)
            {
                // Создаём тестовую причёску
                var hairGO = new GameObject("TestHair");
                hairGO.transform.SetParent(testClient.transform, false);
                var hairRenderer = hairGO.AddComponent<SpriteRenderer>();
                hairRenderer.sprite = testHairSprite;
                hairRenderer.color = testHairColor;
                hairRenderer.sortingOrder = 10;

                Debug.Log($"✓ Тестовые волосы добавлены: спрайт={testHairSprite.name}");
            }

            // Удаляем через 3 секунды
            Destroy(testClient, 3f);
        }
    }
}
