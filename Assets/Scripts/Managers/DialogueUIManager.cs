using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using DialogueSystem.Data;
using Scriptables.Audio;

namespace Managers
{
    public class DialogueUIManager : MonoBehaviour
    {
        public static DialogueUIManager Instance { get; private set; }

        // --- ССЫЛКИ НА UI ---
        [Header("UI Ссылки")]
        private GameObject dialoguePanel;
        private CanvasGroup panelCanvasGroup; 
        
        private Image directorPortrait;
        private TextMeshProUGUI directorNameText;
        
        private Image clientPortrait;
        private TextMeshProUGUI clientNameText;

        private TextMeshProUGUI dialogueText;
        private Button nextButton;
        private Transform choiceContainer;
        
        private System.Action onDialogueComplete;

        // --- НАСТРОЙКИ ---
        [Header("Assets")]
        [SerializeField] private GameObject choiceButtonPrefab;

        [Header("Настройки Текста")]
        [Tooltip("Фиксированная скорость печати (сек на символ). 0.04 - комфортная скорость.")]
        [SerializeField] private float unifiedTypingSpeed = 0.04f;

        [Header("Звуки")]
        [SerializeField] private AudioClip defaultStartSound;
        [SerializeField] private AudioClip defaultChoiceSound;
        [SerializeField] private AudioClip defaultEventSound;
        [SerializeField] private AudioClip defaultPhraseSound;

        [Header("Настройки Директора (Фолбэк)")]
        [Tooltip("Голос директора для сцен, где нет аватара (например, Главное Меню)")]
        [SerializeField] private VoiceData directorVoiceProfile; 

        [Header("Анимация Окна")]
        [SerializeField] private float windowAnimDuration = 0.3f;
        [SerializeField] private Vector3 windowStartScale = new Vector3(0.9f, 0.9f, 1f); 

        [Header("Анимация Портретов")]
        [SerializeField] private float portraitAnimDuration = 0.2f;
        [SerializeField] private Vector3 activeScale = new Vector3(1.1f, 1.1f, 1f); 
        [SerializeField] private Vector3 inactiveScale = Vector3.one; 
        [SerializeField] private Color activeColor = Color.white;
        [SerializeField] private Color activeTextColor = Color.black; 
        [SerializeField] private Color inactiveColor = new Color(0.6f, 0.6f, 0.6f, 1f); 
        [SerializeField] private Color inactiveTextColor = new Color(0.5f, 0.5f, 0.5f, 1f); 

        [Header("База Персонажей (NPC)")]
        [SerializeField] private List<SpeakerProfile> npcProfiles;

        // --- ВНУТРЕННИЕ ПЕРЕМЕННЫЕ ---
        private DialogueNode currentNode;
        private ClientPathfinding currentClientContext;
        private bool isUIReady = false;
        
        private Coroutine typingCoroutine;
        private Coroutine panelAnimCoroutine;
        
        private Coroutine directorAnimCoroutine;
        private Coroutine clientAnimCoroutine;

        private bool isTyping = false;
        private string fullTextTarget = "";

        void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        public void RegisterSceneUI(DialogueUIConnector connector)
        {
            this.dialoguePanel = connector.dialoguePanel;
            this.panelCanvasGroup = dialoguePanel.GetComponent<CanvasGroup>();
            if (this.panelCanvasGroup == null) this.panelCanvasGroup = dialoguePanel.AddComponent<CanvasGroup>();

            this.directorPortrait = connector.directorPortrait;
            this.directorNameText = connector.directorNamePlate;

            this.clientPortrait = connector.clientPortrait;
            this.clientNameText = connector.clientNamePlate;

            this.dialogueText = connector.dialogueText;
            this.nextButton = connector.nextButton;
            this.choiceContainer = connector.choiceContainer;

            this.nextButton.onClick.RemoveAllListeners();
            this.nextButton.onClick.AddListener(OnNextClicked);

            dialoguePanel.SetActive(false);
            isUIReady = true;
        }

        public void StartDialogue(DialogueGraph graph, ClientPathfinding client, System.Action onComplete = null)
        {
            if (!isUIReady || graph == null) return;

            this.onDialogueComplete = onComplete;
            currentClientContext = client;
            
            MainUIManager.Instance?.PauseGame(false);
            
            dialoguePanel.SetActive(true);
            if (panelAnimCoroutine != null) StopCoroutine(panelAnimCoroutine);
            panelAnimCoroutine = StartCoroutine(AnimatePanel(true));
            
            SetupPortraitsAndNames(client);

            var startNode = graph.allNodes.Find(n => n is StartNode) as StartNode;
            if (startNode != null)
            {
                AudioClip clip = startNode.startSoundOverride != null ? startNode.startSoundOverride : defaultStartSound;
                PlaySystemSound(clip);
                if (startNode.nextNode != null) ProcessNode(startNode.nextNode);
            }
            else
            {
                EndDialogue();
            }
        }

        private void SetupPortraitsAndNames(ClientPathfinding client)
        {
            if (directorPortrait)
            {
                directorPortrait.gameObject.SetActive(true);
                if(directorNameText) directorNameText.text = "Директор"; 
                SetVisualState(directorPortrait, directorNameText, false);
            }

            if (clientPortrait)
            {
                clientPortrait.gameObject.SetActive(false);
                if(clientNameText) clientNameText.text = "";
                SetVisualState(clientPortrait, clientNameText, false);
                
                if (client != null)
                {
                    Sprite face = client.GetVisuals()?.GetPortraitSprite();
                    if (face != null)
                    {
                        clientPortrait.sprite = face;
                        clientPortrait.gameObject.SetActive(true);
                        if(clientNameText) clientNameText.text = client.name ?? "Посетитель";
                    }
                }
            }
        }

        private void ProcessNode(DialogueNode node)
        {
            currentNode = node;
            if (choiceContainer != null) foreach (Transform child in choiceContainer) Destroy(child.gameObject);
			
			if (node is EndNode endNode)
            {
                if (endNode.endSound != null)
                {
                    PlaySystemSound(endNode.endSound);
                }
                EndDialogue();
                return;
            }
			
			
            if (node == null || node is EndNode)
            {
                EndDialogue();
                return;
            }

            switch (node)
            {
                case PhraseNode phrase: ShowPhrase(phrase); break;
                case ChoiceNode choice: ShowChoice(choice); break;
                case EventNode evt: ExecuteEvent(evt); break;
                case RandomNode rnd: ProcessRandomNode(rnd); break;
            }
        }
        
        private void ProcessRandomNode(RandomNode node)
        {
            if (node.outcomes == null || node.outcomes.Count == 0)
            {
                Debug.LogWarning("RandomNode не имеет исходов!");
                EndDialogue();
                return;
            }

            float totalWeight = 0f;
            foreach (var outcome in node.outcomes) totalWeight += outcome.chance;

            float randomPoint = UnityEngine.Random.value * totalWeight;
            float currentWeight = 0f;

            DialogueNode targetNode = null;

            foreach (var outcome in node.outcomes)
            {
                currentWeight += outcome.chance;
                if (randomPoint <= currentWeight)
                {
                    targetNode = outcome.nextNode;
                    break;
                }
            }
            
            if (targetNode == null) targetNode = node.outcomes[node.outcomes.Count - 1].nextNode;
            ProcessNode(targetNode);
        }

        private void ShowPhrase(PhraseNode phrase)
        {
            nextButton.gameObject.SetActive(true);
            choiceContainer.gameObject.SetActive(false);

            AudioClip appearClip = phrase.appearSound != null ? phrase.appearSound : defaultPhraseSound;
            PlaySystemSound(appearClip);

            VoiceData voice = null;
            Sprite portraitToShow = null;
            string speakerName = phrase.speakerID;
            bool isDirector = false;

            if (phrase.speakerID == "Director")
            {
                speakerName = "Директор";
                isDirector = true;
                
                if (DirectorAvatarController.Instance != null) 
                    voice = DirectorAvatarController.Instance.voiceProfile;
                if (voice == null) 
                    voice = directorVoiceProfile;
            }
            else if (phrase.speakerID == "Client" && currentClientContext != null)
            {
                speakerName = currentClientContext.name ?? "Посетитель";
                isDirector = false;
                voice = currentClientContext.voiceProfile;
                portraitToShow = currentClientContext.GetVisuals()?.GetPortraitSprite();
            }
            else
            {
                var npc = npcProfiles.Find(p => p.speakerID == phrase.speakerID);
                if (npc != null)
                {
                    speakerName = npc.displayName;
                    portraitToShow = npc.portrait;
                    voice = npc.voice;
                }
                isDirector = false;
            }

            if (phrase.speakerPortrait != null) portraitToShow = phrase.speakerPortrait;

            if (!isDirector)
            {
                if (portraitToShow != null)
                {
                    if (clientPortrait)
                    {
                        clientPortrait.sprite = portraitToShow;
                        clientPortrait.gameObject.SetActive(true);
                    }
                }
                if (clientNameText) clientNameText.text = speakerName;
            }
            else
            {
                if (directorNameText) directorNameText.text = "Директор";
            }

            UpdateFocusAnimation(isDirector);

            if (typingCoroutine != null) StopCoroutine(typingCoroutine);
            typingCoroutine = StartCoroutine(TypewriterRoutine(phrase.text, voice));
        }

        private void UpdateFocusAnimation(bool? isDirectorActive)
        {
            bool dirActive = (isDirectorActive == true);
            bool clientActive = (isDirectorActive == false);

            if (directorAnimCoroutine != null) StopCoroutine(directorAnimCoroutine);
            directorAnimCoroutine = StartCoroutine(AnimateGroup(directorPortrait, directorNameText, dirActive));

            if (clientAnimCoroutine != null) StopCoroutine(clientAnimCoroutine);
            clientAnimCoroutine = StartCoroutine(AnimateGroup(clientPortrait, clientNameText, clientActive));
        }

        private IEnumerator AnimateGroup(Image portrait, TextMeshProUGUI nameText, bool isActive)
        {
            if (portrait == null) yield break;

            Vector3 startScale = portrait.rectTransform.localScale;
            Color startImgColor = portrait.color;
            Color startTextColor = nameText != null ? nameText.color : Color.white;

            Vector3 endScale = isActive ? activeScale : inactiveScale;
            Color endImgColor = isActive ? activeColor : inactiveColor;
            Color endTextColor = isActive ? activeTextColor : inactiveTextColor;

            float timer = 0f;
            while (timer < portraitAnimDuration)
            {
                timer += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(timer / portraitAnimDuration);
                
                portrait.rectTransform.localScale = Vector3.Lerp(startScale, endScale, t);
                portrait.color = Color.Lerp(startImgColor, endImgColor, t);
                if (nameText != null) nameText.color = Color.Lerp(startTextColor, endTextColor, t);

                yield return null;
            }

            portrait.rectTransform.localScale = endScale;
            portrait.color = endImgColor;
            if (nameText != null) nameText.color = endTextColor;
        }

        private void SetVisualState(Image portrait, TextMeshProUGUI nameText, bool isActive)
        {
            if (portrait)
            {
                portrait.rectTransform.localScale = isActive ? activeScale : inactiveScale;
                portrait.color = isActive ? activeColor : inactiveColor;
            }
            if (nameText)
            {
                nameText.color = isActive ? activeTextColor : inactiveTextColor;
            }
        }

        private IEnumerator TypewriterRoutine(string text, VoiceData voice)
        {
            isTyping = true;
            fullTextTarget = text;
            dialogueText.text = "";
            
            // --- ЕДИНАЯ СКОРОСТЬ ДЛЯ ВСЕХ ---
            float delay = unifiedTypingSpeed; 

            // Звук должен быть слышен всегда, берем позицию камеры
            Vector3 soundPos = Camera.main != null ? Camera.main.transform.position : transform.position;

            for (int i = 0; i < text.Length; i++)
            {
                dialogueText.text += text[i];
                
                // Проигрываем звук (каждые 2 символа для не слишком частого треска)
                if (i % 2 == 0 && !char.IsWhiteSpace(text[i]) && voice != null && AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlayVoiceClip(
                        voice.GetRandomClip(), 
                        soundPos, 
                        voice.basePitch, 
                        voice.pitchDelta, 
                        voice.volume
                    );
                }
                yield return new WaitForSecondsRealtime(delay);
            }
            isTyping = false;

            // --- АВТО-СКРЫТИЕ КНОПКИ В ВЫБОРЕ ---
            // Если текст дописался сам и это Выбор, убираем кнопку "Далее", чтобы можно было жать варианты
            if (currentNode is ChoiceNode)
            {
                nextButton.gameObject.SetActive(false);
            }
        }

        private void ShowChoice(ChoiceNode choice)
        {
            choiceContainer.gameObject.SetActive(true);
            PlaySystemSound(defaultChoiceSound);

            if (!string.IsNullOrEmpty(choice.queryText))
            {
                // Показываем кнопку "Далее" как прозрачный кликер для пропуска текста
                nextButton.gameObject.SetActive(true);
                
                // Анимация фокуса на Директоре
                if (directorNameText) directorNameText.text = "Директор";
                UpdateFocusAnimation(true);

                // Озвучка голосом Директора
                VoiceData voice = null;
                if (DirectorAvatarController.Instance != null) 
                    voice = DirectorAvatarController.Instance.voiceProfile;
                else 
                    voice = directorVoiceProfile;

                if (typingCoroutine != null) StopCoroutine(typingCoroutine);
                typingCoroutine = StartCoroutine(TypewriterRoutine(choice.queryText, voice));
            }
            else
            {
                // Если текста вопроса нет - сразу кнопки
                dialogueText.text = "";
                nextButton.gameObject.SetActive(false); 
            }

            foreach (var option in choice.options)
            {
                if (StoryStateManager.Instance != null && !string.IsNullOrEmpty(option.conditionKey))
                {
                    if (!StoryStateManager.Instance.CheckCondition(option.conditionKey, option.operation, option.conditionValue))
                        continue;
                }
                GameObject btnObj = Instantiate(choiceButtonPrefab, choiceContainer);
                btnObj.GetComponentInChildren<TextMeshProUGUI>().text = option.text;
                btnObj.GetComponent<Button>().onClick.AddListener(() => ProcessNode(option.nextNode));
            }
        }

        private void ExecuteEvent(EventNode evt)
        {
            // 1. Флаги сюжета (требуют StoryStateManager)
            if (evt.eventType == EventNode.EventType.SetFlag) 
            {
                if (StoryStateManager.Instance != null)
                    StoryStateManager.Instance.SetFlag(evt.flagKey, evt.intValue);
                else
                    Debug.LogWarning("Попытка установить флаг, но StoryStateManager не найден!");
            }
                
            // 2. Деньги (НЕ требуют StoryStateManager)
            else if (evt.eventType == EventNode.EventType.AddMoney) 
            {
                PlayerWallet.Instance?.AddMoney(evt.intValue, "Сюжет (Диалог)", IncomeType.Shadow);
            }
                
            // 3. Страйки (НЕ требуют StoryStateManager)
            else if (evt.eventType == EventNode.EventType.AddStrike) 
            {
                DirectorManager.Instance?.AddStrike();
            }
            
            AudioClip clip = evt.soundEffect != null ? evt.soundEffect : defaultEventSound;
            PlaySystemSound(clip);

            if (!string.IsNullOrEmpty(evt.notificationText))
            {
                nextButton.gameObject.SetActive(true);
                choiceContainer.gameObject.SetActive(false);
                if(directorNameText) directorNameText.text = "";
                if(clientNameText) clientNameText.text = "РЕЗУЛЬТАТ";
                
                dialogueText.text = evt.notificationText;
                UpdateFocusAnimation(null);
                
                currentNode = evt; 
            }
            else
            {
                if (evt.eventType == EventNode.EventType.EndDialogue) EndDialogue();
                else ProcessNode(evt.nextNode);
            }
        }

        private void OnNextClicked()
        {
            if (isTyping)
            {
                // --- СКИП ТЕКСТА ---
                if (typingCoroutine != null) StopCoroutine(typingCoroutine);
                dialogueText.text = fullTextTarget;
                isTyping = false;

                // Если это Выбор и мы скипнули текст - сразу убираем кнопку, открывая варианты
                if (currentNode is ChoiceNode)
                {
                    nextButton.gameObject.SetActive(false);
                }
                return;
            }

            if (currentNode is PhraseNode phrase) ProcessNode(phrase.nextNode);
            else if (currentNode is EventNode evt)
            {
                if (evt.eventType == EventNode.EventType.EndDialogue) EndDialogue();
                else ProcessNode(evt.nextNode);
            }
        }

        private void EndDialogue()
        {
            if (panelAnimCoroutine != null) StopCoroutine(panelAnimCoroutine);
            panelAnimCoroutine = StartCoroutine(AnimatePanel(false));

            if (currentClientContext != null)
            {
                currentClientContext.stateMachine.SetGoal(ClientSpawner.Instance.exitWaypoint);
                currentClientContext.stateMachine.SetState(ClientState.Leaving);
                StartOfDayPanel.Instance?.RemoveDocumentIcon(currentClientContext);
            }

            currentClientContext = null;
            currentNode = null;

            onDialogueComplete?.Invoke();
            onDialogueComplete = null;
        }
        
        private IEnumerator AnimatePanel(bool show)
        {
            float timer = 0f;
            float startAlpha = panelCanvasGroup.alpha;
            float targetAlpha = show ? 1f : 0f;
            
            Vector3 startScale = dialoguePanel.transform.localScale;
            Vector3 targetScale = show ? Vector3.one : windowStartScale;

            if (show && startAlpha == 0f) 
            {
                dialoguePanel.transform.localScale = windowStartScale;
                startScale = windowStartScale;
            }

            while (timer < windowAnimDuration)
            {
                timer += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(timer / windowAnimDuration);
                float smoothT = Mathf.SmoothStep(0f, 1f, t);

                panelCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, smoothT);
                dialoguePanel.transform.localScale = Vector3.Lerp(startScale, targetScale, smoothT);
                yield return null;
            }

            panelCanvasGroup.alpha = targetAlpha;
            dialoguePanel.transform.localScale = targetScale;

            if (!show)
            {
                dialoguePanel.SetActive(false);
                MainUIManager.Instance?.ResumeGame();
            }
        }

        private void PlaySystemSound(AudioClip clip)
        {
            Vector3 soundPos = Camera.main != null ? Camera.main.transform.position : transform.position;

            if (clip != null && AudioManager.Instance != null)
            {
                AudioSource src = GetComponent<AudioSource>();
                if (src) src.PlayOneShot(clip);
                else AudioSource.PlayClipAtPoint(clip, soundPos);
            }
        }
    }
}