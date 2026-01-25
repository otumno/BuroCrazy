using System.Collections.Generic;
using UnityEngine;
using Data.Creation;
using Characters;

namespace UI.Creation
{
    public class DirectorCreationBookUI : MonoBehaviour
    {
        [Header("UI ссылки")]
        public Transform pagesContainer;
        public GameObject pagePrefab;
        public GameObject choiceButtonPrefab;
        public BookPageData currentPage;

        [Header("Навигация")]
        public UnityEngine.UI.Button nextPageButton;
        public UnityEngine.UI.Button previousPageButton;
        public UnityEngine.UI.Button finishButton;

        [Header("Визуал")]
        public UnityEngine.UI.Image backgroundImage;
        public UnityEngine.UI.Image characterImage;
        public TMPro.TextMeshProUGUI storyText;

        [Header("Сохраненные данные")]
        public DirectorInitialState initialState;

        private Stack<BookPageData> pageHistory = new Stack<BookPageData>();
        private List<BookPageData> allPages;

        private void Start()
        {
            if (BookPageDatabase.Instance != null)
            {
                allPages = BookPageDatabase.Instance.allPages;
                ShowPage(allPages[0]); // Показываем первую страницу
            }
            else
            {
                Debug.LogError("[DirectorCreationBookUI] BookPageDatabase не найден!");
            }

            initialState = new DirectorInitialState();
        }

        public void ShowPage(string pageID)
        {
            var page = allPages.Find(p => p.pageID == pageID);
            if (page != null)
            {
                ShowPage(page);
            }
        }

        private void ShowPage(BookPageData page)
        {
            if (currentPage != null)
            {
                pageHistory.Push(currentPage);
            }

            currentPage = page;

            // Обновляем визуал
            if (backgroundImage != null && page.backgroundImage != null)
            {
                backgroundImage.sprite = page.backgroundImage;
            }

            if (characterImage != null && page.characterIllustration != null)
            {
                characterImage.sprite = page.characterIllustration;
            }

            if (storyText != null)
            {
                storyText.text = page.storyText;
            }

            // Очищаем старые кнопки выбора
            ClearChoices();

            // Создаем кнопки выбора
            if (page.choices != null)
            {
                foreach (var choice in page.choices)
                {
                    CreateChoiceButton(choice);
                }
            }

            // Обновляем кнопки навигации
            UpdateNavigationButtons();
        }

        private void CreateChoiceButton(BookPageData.BookChoice choice)
        {
            var buttonObj = Instantiate(choiceButtonPrefab, pagesContainer);
            var button = buttonObj.GetComponent<UnityEngine.UI.Button>();
            var text = buttonObj.GetComponentInChildren<TMPro.TextMeshProUGUI>();

            if (text != null)
            {
                text.text = choice.choiceText;
            }

            button.onClick.AddListener(() => OnChoiceSelected(choice));
        }

        private void ClearChoices()
        {
            foreach (Transform child in pagesContainer)
            {
                if (child.gameObject != characterImage?.gameObject &&
                    child.gameObject != storyText?.gameObject)
                {
                    Destroy(child.gameObject);
                }
            }
        }

        private void OnChoiceSelected(BookPageData.BookChoice choice)
        {
            // Применяем эффекты
            ApplyChoiceEffects(choice.effects);

            // Переходим на следующую страницу
            if (!string.IsNullOrEmpty(choice.nextPageID))
            {
                ShowPage(choice.nextPageID);
            }
            else if (choice.nextPageID == null && currentPage != null)
            {
                // Конец книги - переход к созданию персонажа
                FinishBook();
            }
        }

        private void ApplyChoiceEffects(List<BookPageData.ChoiceEffect> effects)
        {
            if (effects == null) return;

            foreach (var effect in effects)
            {
                switch (effect.type)
                {
                    case BookPageData.EffectType.AddMoney:
                        initialState.startingMoney += effect.value;
                        break;

                    case BookPageData.EffectType.SetMoney:
                        initialState.startingMoney = effect.value;
                        break;

                    case BookPageData.EffectType.AddInfluence:
                        initialState.startingInfluence += effect.value;
                        break;

                    case BookPageData.EffectType.SetInfluence:
                        initialState.startingInfluence = effect.value;
                        break;

                    case BookPageData.EffectType.AddStaff:
                        AddStaffMember(effect.targetID, effect.value);
                        break;

                    case BookPageData.EffectType.UnlockUpgrade:
                        if (!initialState.unlockedUpgradeNames.Contains(effect.targetID))
                        {
                            initialState.unlockedUpgradeNames.Add(effect.targetID);
                        }
                        break;

                    case BookPageData.EffectType.SetPolicy:
                        if (!initialState.activePolicyIDs.Contains(effect.targetID))
                        {
                            initialState.activePolicyIDs.Add(effect.targetID);
                        }
                        break;
                }
            }
        }

        private void AddStaffMember(string roleID, int count)
        {
            if (System.Enum.TryParse(roleID, out StaffController.Role role))
            {
                for (int i = 0; i < count; i++)
                {
                    initialState.startingStaff.Add(new DirectorInitialState.StaffSpawnData
                    {
                        role = role,
                        skillLevel = 1,
                        punctuality = Random.Range(0.3f, 0.7f),
                        stressResistance = Random.Range(0.4f, 0.8f),
                        workSpeed = Random.Range(0.8f, 1.2f)
                    });
                }
            }
        }

        private void FinishBook()
        {
            Debug.Log("[DirectorCreationBookUI] Книга завершена. Начальное состояние директора:");
            Debug.Log($"  Деньги: {initialState.startingMoney}");
            Debug.Log($"  Влияние: {initialState.startingInfluence}");
            Debug.Log($"  Работники: {initialState.startingStaff.Count}");
            Debug.Log($"  Апгрейды: {initialState.unlockedUpgradeNames.Count}");
            Debug.Log($"  Политики: {initialState.activePolicyIDs.Count}");

            // Вызываем событие завершения
            OnBookFinished?.Invoke(initialState);
        }

        public void GoBack()
        {
            if (pageHistory.Count > 0)
            {
                var previousPage = pageHistory.Pop();
                currentPage = null; // Чтобы не сохранять текущую в историю
                ShowPage(previousPage);
            }
        }

        private void UpdateNavigationButtons()
        {
            if (previousPageButton != null)
            {
                previousPageButton.interactable = pageHistory.Count > 0;
            }
        }

        public event System.Action<DirectorInitialState> OnBookFinished;
    }
}
