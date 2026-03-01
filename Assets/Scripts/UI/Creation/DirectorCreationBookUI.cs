using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Data.Creation;
using Characters;
using Managers;
using TMPro;
using Enums;
using UnityEngine.SceneManagement;

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

        [Header("Перелистывание (эффект)")]
        [Tooltip("Оверлей для засвета при перелистывании")]
        public Image transitionOverlay;
        [Tooltip("Картинка для анимации корешка")]
        public Image flipAnimationImage;
        [Tooltip("3 кадра анимации перелистывания")]
        public List<Sprite> flipFrames;
        [Tooltip("Время перелистывания")]
        public float pageTurnDuration = 0.4f;
        [Tooltip("Цвет вспышки")]
        public Color flashColor = Color.white;

        [Header("Звук")]
        public AudioSource audioSource;
        [Tooltip("Звуки перелистывания страниц")]
        public List<AudioClip> pageTurnSounds;

        [Header("Сохраненные данные")]
        public DirectorInitialState initialState;

        [Header("Код создания")]
        [Tooltip("Сгенерированный код (A1B2C1D3E3)")]
        public string creationCode = "";

        [Header("Настройки")]
        [Tooltip("Имя сцены для запуска игры")]
        public string gameSceneName = "GameScene";

        private Stack<BookPageData> pageHistory = new Stack<BookPageData>();
        private List<BookPageData> allPages;
        private string currentPageLetter = "";
        private bool showingResult = false;
        private bool isTransitioning = false;

        private void Start()
        {
            Debug.Log("[DirectorCreationBookUI] Start вызван!");
            
            if (nextPageButton != null)
            {
                nextPageButton.onClick.AddListener(OnNextPageClicked);
            }

            if (startGameButton != null)
            {
                startGameButton.onClick.AddListener(OnStartGameClicked);
            }

            if (transitionOverlay != null)
            {
                transitionOverlay.gameObject.SetActive(false);
                transitionOverlay.color = new Color(flashColor.r, flashColor.g, flashColor.b, 0f);
            }
            if (flipAnimationImage != null) flipAnimationImage.gameObject.SetActive(false);
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
            Debug.Log($"[DirectorCreationBookUI] ShowPage: {page.pageID}");

            if (choicesContainer != null)
            {
                choicesContainer.gameObject.SetActive(true);
                Debug.Log($"[DirectorCreationBookUI] ChoicesContainer включен: {choicesContainer.gameObject.activeInHierarchy}");
            }

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

            Debug.Log($"[DirectorCreationBookUI] ShowPage: choices count = {page.choices?.Count ?? 0}");
            Debug.Log($"[DirectorCreationBookUI] choicesContainer active = {choicesContainer.gameObject.activeInHierarchy}");

            if (page.choices != null)
            {
                foreach (var choice in page.choices)
                {
                    CreateChoiceButton(choice);
                }
            }

            Debug.Log($"[DirectorCreationBookUI] Created buttons: {choicesContainer.childCount}");

            PlayPageMusic(page);

            UpdateNavigationButtons();
        }

        private void CreateChoiceButton(BookPageData.BookChoice choice)
        {
            var buttonObj = Instantiate(choiceButtonPrefab, choicesContainer);
            var button = buttonObj.GetComponent<Button>();
            var text = buttonObj.GetComponentInChildren<TextMeshProUGUI>();
            var image = buttonObj.GetComponent<Image>();
            var rect = buttonObj.GetComponent<RectTransform>();
            
            rect.sizeDelta = new Vector2(900, 120);
            
            var canvasGroup = buttonObj.GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = buttonObj.AddComponent<CanvasGroup>();
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
            canvasGroup.alpha = 1;

            var canvas = buttonObj.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                canvas.overrideSorting = true;
                canvas.sortingOrder = 15;
            }

            foreach (var graphic in buttonObj.GetComponentsInChildren<UnityEngine.UI.Graphic>())
            {
                graphic.raycastTarget = true;
            }

            buttonObj.layer = LayerMask.NameToLayer("UI");

            if (text != null)
            {
                text.text = choice.choiceText;
                text.fontSize = 26;
            }

            button.interactable = true;
            
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => {
                Debug.Log($"[DirectorCreationBookUI] ====> НАЖАТА кнопка: {choice.choiceText}");
                OnChoiceSelected(choice);
            });
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

            if (choicesContainer != null)
                choicesContainer.gameObject.SetActive(false);

            ApplyChoiceEffects(choice.effects);
            AppendToCode(choice);

            if (resultText != null)
            {
                resultText.text = GetChoiceResultText(choice);
                resultText.gameObject.SetActive(true);
            }

            if (characterImage != null && choice.resultImage != null)
            {
                Debug.Log($"[DirectorCreationBookUI] Меняем иллюстрацию на: {choice.resultImage.name}");
                characterImage.sprite = choice.resultImage;
            }
            else
            {
                Debug.Log($"[DirectorCreationBookUI] resultImage = {(choice.resultImage != null ? choice.resultImage.name : "NULL")}");
            }

            if (currentPage.isFinalPage)
            {
                Debug.Log($"[DirectorCreationBookUI] Финальная страница! isFinalPage = {currentPage.isFinalPage}");

                if (currentPage.finalMusic != null)
                {
                    PlayMusic(currentPage.finalMusic);
                }

                if (backgroundImage != null && currentPage.finalBackgroundImage != null)
                {
                    Debug.Log($"[DirectorCreationBookUI] Меняем фон на finalBackgroundImage: {currentPage.finalBackgroundImage.name}");
                    backgroundImage.sprite = currentPage.finalBackgroundImage;
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

        private void PlayRandomPageTurnSound()
        {
            if (audioSource == null)
            {
                Debug.LogWarning("[DirectorCreationBookUI] audioSource = NULL!");
                return;
            }
            if (pageTurnSounds == null || pageTurnSounds.Count == 0)
            {
                Debug.LogWarning("[DirectorCreationBookUI] pageTurnSounds пустой или null!");
                return;
            }
            
            var validSounds = pageTurnSounds.FindAll(s => s != null);
            if (validSounds.Count > 0)
            {
                Debug.Log($"[DirectorCreationBookUI] Воспроизводим звук перелистывания: {validSounds[0].name}");
                audioSource.PlayOneShot(validSounds[Random.Range(0, validSounds.Count)]);
            }
        }

        public void OnNextPageClicked()
        {
            if (!showingResult || isTransitioning) return;

            string nextPageId = null;
            if (!string.IsNullOrEmpty(currentPage.choices[currentPage.choices.Count - 1].nextPageID))
            {
                nextPageId = currentPage.choices[currentPage.choices.Count - 1].nextPageID;
            }
            else
            {
                int currentIndex = allPages.IndexOf(currentPage);
                if (currentIndex < allPages.Count - 1)
                {
                    nextPageId = allPages[currentIndex + 1].pageID;
                }
            }

            if (!string.IsNullOrEmpty(nextPageId))
            {
                StartCoroutine(PageTransitionRoutine(nextPageId));
            }
        }

        private IEnumerator PageTransitionRoutine(string nextPageId)
        {
            isTransitioning = true;
            SetButtonsInteractable(false);

            StartCoroutine(RunFlipAnimation(1));
            PlayRandomPageTurnSound();

            float halfDuration = pageTurnDuration / 2f;
            float timer = 0f;

            if (transitionOverlay != null)
            {
                transitionOverlay.gameObject.SetActive(true);
                Color c = flashColor;
                c.a = 0f;
                transitionOverlay.color = c;
                
                while (timer < halfDuration)
                {
                    timer += Time.unscaledDeltaTime;
                    float progress = Mathf.Clamp01(timer / halfDuration);
                    c.a = progress;
                    transitionOverlay.color = c;
                    yield return null;
                }
                c.a = 1f;
                transitionOverlay.color = c;
            }
            else
            {
                yield return new WaitForSecondsRealtime(halfDuration);
            }

            currentPageLetter = GetPageLetter(allPages.FindIndex(p => p.pageID == nextPageId));
            ShowPage(nextPageId);

            timer = 0f;
            if (transitionOverlay != null)
            {
                Color c = flashColor;
                while (timer < halfDuration)
                {
                    timer += Time.unscaledDeltaTime;
                    float progress = 1f - Mathf.Clamp01(timer / halfDuration);
                    c.a = progress;
                    transitionOverlay.color = c;
                    yield return null;
                }
                c.a = 0f;
                transitionOverlay.color = c;
                transitionOverlay.gameObject.SetActive(false);
            }
            else
            {
                yield return new WaitForSecondsRealtime(halfDuration);
            }

            isTransitioning = false;
            SetButtonsInteractable(true);
        }

        private IEnumerator RunFlipAnimation(int direction)
        {
            if (flipAnimationImage == null || flipFrames == null || flipFrames.Count < 3) yield break;

            flipAnimationImage.gameObject.SetActive(true);
            float timePerFrame = pageTurnDuration / 3f;
            int[] frameIndices = (direction > 0) ? new int[] { 0, 1, 2 } : new int[] { 2, 1, 0 };

            for (int i = 0; i < frameIndices.Length; i++)
            {
                flipAnimationImage.sprite = flipFrames[frameIndices[i]];
                yield return new WaitForSecondsRealtime(timePerFrame);
            }

            flipAnimationImage.gameObject.SetActive(false);
        }

        private void SetButtonsInteractable(bool interactable)
        {
            if (previousPageButton != null) previousPageButton.interactable = interactable;
            if (nextPageButton != null) nextPageButton.interactable = interactable;
            if (finishButton != null) finishButton.interactable = interactable;
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
            Debug.Log("[DirectorCreationBookUI] OnStartGameClicked вызван!");
            StopMusic();
            FinishBook();
            
            Debug.Log($"[DirectorCreationBookUI] Переход управляется MainMenuActions");
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

            if (startGameButton != null)
            {
                startGameButton.gameObject.SetActive(showingResult && currentPage.isFinalPage);
            }
        }

        public void OpenBook()
        {
            var animator = GetComponent<UIWindowAnimator>();
            if (animator != null)
            {
                animator.Open();
            }
            else
            {
                var canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = 1f;
                    canvasGroup.interactable = true;
                    canvasGroup.blocksRaycasts = true;
                }
            }
            Debug.Log("[DirectorCreationBookUI] OpenBook() - запускаем инициализацию");
            InitializeBook();
        }

        public void CloseBook()
        {
            StopMusic();
            var animator = GetComponent<UIWindowAnimator>();
            if (animator != null)
            {
                animator.Close();
            }
            else
            {
                var canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = 0f;
                    canvasGroup.interactable = false;
                    canvasGroup.blocksRaycasts = false;
                }
            }
        }

        private void InitializeBook()
        {
            var database = BookPageDatabase.Instance;
            if (database == null)
            {
                database = Resources.Load<BookPageDatabase>("DirectorBook/DirectorBookPageDatabase");
            }
            
            if (database != null)
            {
                allPages = database.allPages;
                Debug.Log($"[DirectorCreationBookUI] Найдено страниц: {allPages.Count}");
                
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

        public event System.Action<DirectorInitialState, string> OnBookFinished;
    }
}
