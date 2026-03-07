using UnityEngine;
using Data.Documents;
using Gameplay.Documents;
using Managers;
using Managers.Teletype;
using System.Collections.Generic;
using UI;
using Enums;

namespace Gameplay
{
    public class NoticeBoard : MonoBehaviour
    {
        public static NoticeBoard Instance { get; private set; }

        [Header("Настройки")]
        public Transform interactionPoint;
        public ParticleSystem pinEffect;

        [Header("UI Связь")]
        public ActivePoliciesPanelUI uiPanel;

        [Header("Визуал Доски")]
        public Transform pinnedPapersContainer;
        public GameObject pinnedPaperPrefab;
        public Transform pinPoint;

        private List<GameObject> visualDocs = new List<GameObject>();

        private void Awake()
        {
            if (Instance != null) Destroy(gameObject);
            else Instance = this;

            if (interactionPoint == null) interactionPoint = transform;
        }

        private void Start()
        {
            if (ScenePointsRegistry.Instance != null)
                ScenePointsRegistry.Instance.noticeBoard = this;
        }

        public void PostDocument(ProjectDocumentDefinition doc)
        {
            if (doc == null) return;

            Debug.Log($"[NoticeBoard] Вывешен новый документ: {doc.documentName}");

            ActivateDocumentEffect(doc);

            if (pinnedPapersContainer != null && pinnedPaperPrefab != null)
            {
                GameObject paper = Instantiate(pinnedPaperPrefab, pinnedPapersContainer);
                paper.transform.localRotation = Quaternion.Euler(0, 0, Random.Range(-12f, 12f));
            }

            TeletypeManager.Instance?.LogImportant($"Новый приказ вступил в силу: {doc.documentName}");
        }

        public void PinPolicy(ProjectDocumentObject doc)
        {
            if (doc == null || doc.documentData == null) return;

            string policyID = doc.documentData.targetUpgradeID;
            
            PolicyManager.Instance?.ActivatePolicy(policyID);

            if (pinEffect != null) pinEffect.Play();
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySound(Scriptables.Audio.SoundID.UI_Stamp_Approve, transform.position);

            if (pinnedPaperPrefab != null)
            {
                Transform container = pinnedPapersContainer != null ? pinnedPapersContainer : pinPoint;
                if (container != null)
                {
                    GameObject paper = Instantiate(pinnedPaperPrefab, container);
                    paper.transform.localPosition = new Vector3(Random.Range(-0.2f, 0.2f), Random.Range(-0.2f, 0.2f), 0);
                    paper.transform.localRotation = Quaternion.Euler(0, 0, Random.Range(-10f, 10f));
                    visualDocs.Add(paper);
                }
            }

            Destroy(doc.gameObject);
            
            var policy = PolicyManager.Instance?.GetPolicyById(policyID);
            string name = policy != null ? policy.displayName : "Указ";
            TeletypeManager.Instance?.Log($"ВСТУПИЛ В СИЛУ: {name}");
        }

        void OnMouseDown()
        {
            if (UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;

            if (DirectorAvatarController.Instance != null)
            {
                DirectorAvatarController.Instance.GoToNoticeBoard(this);
            }
        }

        public void OpenUI()
        {
            uiPanel?.Show();
        }

        public void RemoveVisualDoc()
        {
            if (visualDocs.Count > 0)
            {
                var last = visualDocs[visualDocs.Count - 1];
                visualDocs.RemoveAt(visualDocs.Count - 1);
                Destroy(last);
            }
        }

        private void ActivateDocumentEffect(ProjectDocumentDefinition doc)
        {
            if (doc.docType == ProjectDocumentType.Policy)
            {
                string policyID = doc.targetUpgradeID;
                if (PolicyManager.Instance != null && PolicyManager.Instance.GetPolicyById(policyID) != null)
                {
                    PolicyManager.Instance.ActivatePolicy(policyID);
                    return;
                }
            }

            if (doc.docType == ProjectDocumentType.RegionUnlock && doc.targetRegion != null)
            {
                if (ProgressionManager.Instance != null)
                {
                    ProgressionManager.Instance.UnlockRegion(doc.targetRegion.regionID);
                    return;
                }
            }

            if (doc.docType == ProjectDocumentType.JobPromotion && doc.targetJob != null)
            {
                Debug.Log($"[NoticeBoard] Job promotion document detected: {doc.targetJob.jobID}. Use ProgressionManager to handle promotion.");
                return;
            }

            if (doc.docType == ProjectDocumentType.FacilityUpgrade && !string.IsNullOrEmpty(doc.targetUpgradeID))
            {
                Debug.Log($"[NoticeBoard] Upgrade document detected: {doc.targetUpgradeID}. Use UpgradeManager to handle.");
                return;
            }

            Debug.LogWarning($"[NoticeBoard] Документ не распознан: {doc.documentName}");
        }
    }
}
