using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Data.Creation;
using Characters;
using Managers;
using TMPro;
using Enums;

namespace UI.Creation
{
    public class DirectorCreationBookUI : MonoBehaviour
    {
        [Header("UI ссылки")]
        public Transform choicesContainer;
        public GameObject choiceButtonPrefab;
        public BookPageData currentPage;

        [Header("Навигация")]
        public Button previousPageButton;
        public Button nextPageButton;
        public Button finishButton;
        public Button startGameButton;

        [Header("Визуал")]
        public Image backgroundImage;
        public Image characterImage;
        public TextMeshProUGUI storyText;
        public TextMeshProUGUI resultText;

        [Header("Звук")]
        public AudioSource audioSource;

        [Header("Сохраненные данные")]
        public DirectorInitialState initialState;

        [Header("Код создания")]
        [Tooltip("Сгенерированный код (A1B2C1D3E3)")]
        public string creationCode = "";

        private Stack<BookPageData> pageHistory = new Stack<BookPageData>();
        private List<BookPageData> allPages;
        private string currentPageLetter = "";
        private bool showingResult = false;

        private void Start()
        {
            if (BookPageDatabase.Instance != null)
            {
                allPages = BookPageDatabase.Instance.allPages;
                if (allPages.Count > 0)
                {
                    currentPageLetter = GetPageLetter(0);
                    ShowPage(allPages[0]);
                }
            }
            else
            {
                Debug.LogError("[DirectorCreationBookUI] BookPageDatabase не найден!");
            }

            initialState = ScriptableObject.CreateInstance<DirectorInitialState>();
            ResetCode();
        }

        private string GetPageLetter(int index)
        {
            return ((char)('A' + index)).ToString();
        }

        private void ResetCode()
        {
            creationCode = "";
        }

        public void ShowPage(string pageID)
        {
            var page = allPages.Find(p => p.pageID == pageID);
            if (page != null)
            {
                int idx = allPages.IndexOf(page);
                currentPageLetter = GetPageLetter(idx);
                ShowPage(page);
            }
        }

        private void ShowPage(BookPageData page)
        {
            showingResult = false;

            if (currentPage != null)
            {
                pageHistory.Push(currentPage);
            }

            currentPage = page;

            if (backgroundImage != null && page.backgroundImage != null)
            {
                backgroundImage.sprite = page.backgroundImage;
            }

            if (characterImage != null)
            {
                characterImage.sprite = page.characterIllustration;
            }

            if (storyText != null)
            {
                storyText.text = page.storyText;
            }

            if (resultText != null)
            {
                resultText.text = "";
                resultText.gameObject.SetActive(false);
            }

            ClearChoices();

            if (page.choices != null)
            {
                foreach (var choice in page.choices)
                {
                    CreateChoiceButton(choice);
                }
            }

            PlayPageMusic(page);

            UpdateNavigationButtons();
        }

        private void CreateChoiceButton(BookPageData.BookChoice choice)
        {
            var buttonObj = Instantiate(choiceButtonPrefab, choicesContainer);
            var button = buttonObj.GetComponent<Button>();
            var text = buttonObj.GetComponentInChildren<TextMeshProUGUI>();

            if (text != null)
            {
                text.text = choice.choiceText;
            }

            button.onClick.AddListener(() => OnChoiceSelected(choice));
        }

        private void ClearChoices()
        {
            foreach (Transform child in choicesContainer)
            {
                Destroy(child.gameObject);
            }
        }

        private void OnChoiceSelected(BookPageData.BookChoice choice)
        {
            if (showingResult) return;
            showingResult = true;

            ApplyChoiceEffects(choice.effects);
            AppendToCode(choice);

            if (resultText != null)
            {
                resultText.text = GetChoiceResultText(choice);
                resultText.gameObject.SetActive(true);
            }

            if (characterImage != null && choice.resultImage != null)
            {
                characterImage.sprite = choice.resultImage;
            }

            if (currentPage.isFinalPage)
            {
                if (finishButton != null)
                {
                    finishButton.gameObject.SetActive(true);
                }

                if (startGameButton != null)
                {
                    startGameButton.gameObject.SetActive(true);
                }

                if (currentPage.finalMusic != null)
                {
                    PlayMusic(currentPage.finalMusic);
                }
            }
            else
            {
                if (nextPageButton != null)
                {
                    nextPageButton.gameObject.SetActive(true);
                }
            }

            UpdateNavigationButtons();
        }

        private void AppendToCode(BookPageData.BookChoice choice)
        {
            int choiceIndex = currentPage.choices.IndexOf(choice) + 1;
            creationCode += currentPageLetter + choiceIndex;
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

                    case BookPageData.EffectType.SetGender:
                        if (System.Enum.TryParse<Gender>(effect.targetID, out var gender))
                        {
                            initialState.startingGender = gender;
                        }
                        break;

                    case BookPageData.EffectType.SetStrikes:
                        initialState.startingStrikes = Mathf.Clamp(effect.value, 0, 3);
                        break;

                    case BookPageData.EffectType.UnlockRegion:
                        if (!initialState.unlockedRegions.Contains(effect.targetID))
                        {
                            initialState.unlockedRegions.Add(effect.targetID);
                        }
                        break;

                    case BookPageData.EffectType.SetSpriteCollection:
                        initialState.spriteCollectionID = effect.targetID;
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

        private string GetChoiceResultText(BookPageData.BookChoice choice)
        {
            string result = "";

            if (!string.IsNullOrEmpty(choice.resultText))
            {
                result += choice.resultText + "\n\n";
            }

            if (choice.effects != null && choice.effects.Count > 0)
            {
                result += "<color=yellow>Итог:</color>\n";
                foreach (var effect in choice.effects)
                {
                    switch (effect.type)
                    {
                        case BookPageData.EffectType.AddMoney:
                            result += $"+{effect.value} денег ";
                            break;
                        case BookPageData.EffectType.SetMoney:
                            result += $"{effect.value} денег ";
                            break;
                        case BookPageData.EffectType.AddInfluence:
                            result += $"+{effect.value} влияния ";
                            break;
                        case BookPageData.EffectType.SetInfluence:
                            result += $"{effect.value} влияния ";
                            break;
                        case BookPageData.EffectType.AddStaff:
                            result += $"+{effect.value} сотрудников ";
                            break;
                        case BookPageData.EffectType.SetGender:
                            result += $"Пол: {effect.targetID} ";
                            break;
                        case BookPageData.EffectType.SetStrikes:
                            result += $"Ошибки: {effect.value} ";
                            break;
                        case BookPageData.EffectType.UnlockRegion:
                            result += $"Район: {effect.targetID} ";
                            break;
                    }
                }
            }
            return result;
        }

        private void PlayPageMusic(BookPageData page)
        {
            if (page.pageMusic != null && audioSource != null)
            {
                audioSource.clip = page.pageMusic;
                audioSource.loop = true;
                audioSource.Play();
            }
        }

        private void PlayMusic(AudioClip clip)
        {
            if (clip != null && audioSource != null)
            {
                audioSource.clip = clip;
                audioSource.loop = true;
                audioSource.Play();
            }
        }

        private void StopMusic()
        {
            if (audioSource != null)
            {
                audioSource.Stop();
            }
        }

        public void OnNextPageClicked()
        {
            if (!showingResult) return;

            if (!string.IsNullOrEmpty(currentPage.choices[currentPage.choices.Count - 1].nextPageID))
            {
                ShowPage(currentPage.choices[currentPage.choices.Count - 1].nextPageID);
            }
            else
            {
                int currentIndex = allPages.IndexOf(currentPage);
                if (currentIndex < allPages.Count - 1)
                {
                    currentPageLetter = GetPageLetter(currentIndex + 1);
                    ShowPage(allPages[currentIndex + 1]);
                }
            }
        }

        private void FinishBook()
        {
            Debug.Log("[DirectorCreationBookUI] Книга завершена.");
            Debug.Log($"  Код создания: {creationCode}");
            Debug.Log($"  Деньги: {initialState.startingMoney}");
            Debug.Log($"  Влияние: {initialState.startingInfluence}");
            Debug.Log($"  Работники: {initialState.startingStaff.Count}");

            OnBookFinished?.Invoke(initialState, creationCode);
        }

        public void OnStartGameClicked()
        {
            StopMusic();
            FinishBook();
        }

        public void GoBack()
        {
            if (pageHistory.Count > 0)
            {
                var previousPage = pageHistory.Pop();
                currentPage = null;
                int idx = allPages.IndexOf(previousPage);
                currentPageLetter = GetPageLetter(idx);
                ShowPage(previousPage);
            }
        }

        private void UpdateNavigationButtons()
        {
            if (previousPageButton != null)
            {
                previousPageButton.interactable = pageHistory.Count > 0 && !showingResult;
            }

            if (nextPageButton != null)
            {
                nextPageButton.gameObject.SetActive(showingResult && !currentPage.isFinalPage);
            }

            if (finishButton != null)
            {
                finishButton.gameObject.SetActive(false);
            }

            if (startGameButton != null)
            {
                startGameButton.gameObject.SetActive(false);
            }
        }

        public void SetActive(bool active)
        {
            gameObject.SetActive(active);
            if (!active)
            {
                StopMusic();
            }
        }

        public event System.Action<DirectorInitialState, string> OnBookFinished;
    }
}
