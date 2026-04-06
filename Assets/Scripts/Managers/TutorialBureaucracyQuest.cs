using UnityEngine;
using System.Collections;
using Utilities;
using Managers.Teletype;

namespace Managers
{
    public class TutorialBureaucracyQuest : MonoBehaviour
    {
        public static TutorialBureaucracyQuest Instance { get; private set; }

        [Header("Стартовая точка")]
        public InteractionPoint directorDeskPoint;
        public GameObject pathToDesk; // Декаль на полу

        [Header("Точки остановок для катсцены")]
        public Transform registrarStandPoint;
        public Transform officeStandPoint;
        public Transform cashierStandPoint;
        public Transform archiveStandPoint;

        [Header("Ссылки")]
        public DocumentStack directorInboxStack;
        public DialogueSystem.Data.DialogueGraph secretarySuccessCall;
        public GameObject innerDocPrefab; // Префаб документа для визуализации в туториале

        public Data.Documents.ProjectDocumentDefinition tutorialDoc { get; private set; }

        private void Awake()
        {
            if (Instance == null) Instance = this; else Destroy(gameObject);
            if (directorDeskPoint != null) directorDeskPoint.gameObject.SetActive(false);
        }

        public void StartQuest(Data.Documents.ProjectDocumentDefinition doc)
        {
            tutorialDoc = doc;
            TeletypeManager.Instance?.LogImportant("ТУТОРИАЛ: Возьмите приказ со стола и оформите его.");
            if (pathToDesk) pathToDesk.SetActive(true);
            if (directorDeskPoint)
            {
                directorDeskPoint.gameObject.SetActive(true);
                directorDeskPoint.type = InteractionPoint.InteractionType.TutorialTakeDoc;
            }
        }

        public void CompleteQuest()
        {
            if (pathToDesk) pathToDesk.SetActive(false);
            if (directorDeskPoint) directorDeskPoint.type = InteractionPoint.InteractionType.None;

            if (tutorialDoc != null)
            {
                tutorialDoc.archived = true;
                ProgressionManager.Instance.ProcessCompletedDocument(tutorialDoc);
            }

            if (secretarySuccessCall != null && PhoneManager.Instance != null)
            {
                PhoneManager.Instance.RegisterIncomingCall(secretarySuccessCall);
                DialogueUIManager.Instance.StartCoroutine(WaitForDialogueEnd());
            }
        }

        private IEnumerator WaitForDialogueEnd()
        {
            yield return new WaitUntil(() => !PhoneManager.Instance.HasActiveCalls);
            WaveManager.Instance.enableAutoSpawn = true;
            WaveManager.Instance.ForceCheckMorningEvents();
            TeletypeManager.Instance?.LogImportant("Бюро официально открыто! Ожидайте клиентов.");
        }
    }
}