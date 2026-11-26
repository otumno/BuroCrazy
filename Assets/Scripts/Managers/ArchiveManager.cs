// Файл: ArchiveManager.cs

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Managers
{
    public class ArchiveManager : MonoBehaviour
    {
        public static ArchiveManager Instance { get; private set; }

        [Header("Основная точка сдачи документов")]
        [Tooltip("Ссылка на стопку документов у стола архивариуса")]
        public DocumentStack mainDocumentStack;
        [Tooltip("Максимальное количество документов в основной стопке до того, как они начнут появляться в других местах")]
        public int maxCapacityBeforeOverflow = 20;
        [Header("Точки для переполнения")]
        [Tooltip("Список трансформов, где будут появляться документы, если основная стопка переполнена")]
        public List<Transform> overflowPoints;
        [Header("Архивные шкафы")]
        [Tooltip("Список всех шкафов, куда архивариус будет относить документы")]
        public List<ArchiveCabinet> cabinets;
        private List<Transform> occupiedOverflowPoints = new List<Transform>();

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); }
            else { Instance = this; }
        }

        void Start()
        {
            if (cabinets == null || cabinets.Count == 0)
            {
                cabinets = FindObjectsByType<ArchiveCabinet>(FindObjectsSortMode.None).ToList();
            }
        }

        public Transform RequestDropOffPoint()
        {
            if (AvailableDropOffSlots(mainDocumentStack) > 0)
            {
                return mainDocumentStack.transform;
            }
            else
            {
                Transform freePoint = overflowPoints?.FirstOrDefault(p => p != null &&
                    (mainDocumentStack == null || p != mainDocumentStack.transform) && !occupiedOverflowPoints.Contains(p)
                    && (p.GetComponent<DocumentStack>() == null || !p.GetComponent<DocumentStack>().IsFull));
                if (freePoint != null)
                {
                    occupiedOverflowPoints.Add(freePoint);
                    return freePoint;
                }
            }
        
            Debug.LogWarning("Нет свободных мест для сдачи документов в архиве!");
            return null;
        }

        public static bool IsWithinReach(Transform carrier, Vector3 position)
        {
            if (carrier == null) return false;
            // The drop-off marker can be offset from a waypoint, and the mover stops just short of its centre.
            var mover = carrier.GetComponent<AgentMover>();
            float reach = 1f + (mover != null ? mover.stoppingDistance : 0f);
            return Vector2.Distance(carrier.position, position) <= reach;
        }

        public int AvailableDropOffSlots(DocumentStack stack)
        {
            if (stack == null) return 0;
            int limit = stack == mainDocumentStack ? Mathf.Min(stack.maxStackSize, maxCapacityBeforeOverflow) : stack.maxStackSize;
            return Mathf.Max(0, limit - stack.CurrentSize);
        }

        public DocumentStack GetStackToProcess()
        {
            if (mainDocumentStack != null && mainDocumentStack.OrdinaryDocumentCount > 0) return mainDocumentStack;
            return overflowPoints?.Where(p => p != null).Select(p => p.GetComponent<DocumentStack>())
                .FirstOrDefault(s => s != null && s.OrdinaryDocumentCount > 0);
        }

        public bool OwnsStack(DocumentStack stack) => stack != null && (stack == mainDocumentStack ||
            (overflowPoints != null && overflowPoints.Contains(stack.transform)));

        public DocumentStack GetDropOffStack(Transform point)
        {
            if (point == null) return null;
            var stack = point.GetComponent<DocumentStack>();
            if (stack == null)
            {
                stack = point.gameObject.AddComponent<DocumentStack>();
                stack.stackRoot = point;
                stack.documentPrefab = mainDocumentStack != null ? mainDocumentStack.documentPrefab : null;
            }
            return stack;
        }
    
        public ArchiveCabinet GetRandomCabinet()
        {
            if (cabinets == null || cabinets.Count == 0) return null;
            return cabinets[Random.Range(0, cabinets.Count)];
        }

        public void FreeOverflowPoint(Transform point)
        {
            if (occupiedOverflowPoints.Contains(point))
            {
                occupiedOverflowPoints.Remove(point);
            }
        }

        // --- НОВЫЕ МЕТОДЫ ДЛЯ СИСТЕМЫ СОХРАНЕНИЙ И НОВОЙ ИГРЫ ---

        public int GetCurrentDocumentCount()
        {
            if (mainDocumentStack == null) return 0;
            return mainDocumentStack.GetDocumentsForSave().Count;
        }

        public void SetDocumentCount(int count)
        {
            if (mainDocumentStack == null) return;
            mainDocumentStack.SetCount(count);
        }

        public void ResetState()
        {
            if (mainDocumentStack != null)
            {
                mainDocumentStack.TakeEntireStack();
            }
            occupiedOverflowPoints.Clear();
            if (overflowPoints != null)
                foreach (var point in overflowPoints)
                    if (point != null) point.GetComponent<DocumentStack>()?.TakeEntireStack();
        }
    }
}
