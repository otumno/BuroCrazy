using System.Collections.Generic;
using UnityEngine;
using Enums;
using Characters;

namespace Data.Bureaucracy
{
    [CreateAssetMenu(fileName = "BureaucracyRoute", menuName = "Bureau/Bureaucracy/Route")]
    public class BureaucracyRoute : ScriptableObject
    {
        [Header("Идентификация")]
        public string routeID;
        public string routeName;

        [Header("Тип документа")]
        public ProjectDocumentType documentType;

        [Header("Этапы маршрута")]
        public List<RouteStep> steps;

        [System.Serializable]
        public class RouteStep
        {
            public int stepOrder;
            public string stepName;

            [Tooltip("Целевая точка")]
            public ServicePointType targetPoint;

            [Tooltip("Требуется ли подпись на этом этапе")]
            public bool requireSignature = false;

            [Tooltip("Требуется ли оплата на этом этапе")]
            public bool requirePayment = false;

            [Tooltip("Сумма оплаты (0 = согласно документу)")]
            public int paymentAmount = 0;

            [Tooltip("Время на этот этап (секунды)")]
            public float processingTime = 5f;

            [Tooltip("Роли, которые могут обрабатывать на этом этапе")]
            public List<StaffController.Role> allowedRoles;

            [Tooltip("Может ли директор обрабатывать")]
            public bool directorCanProcess = false;
        }

        public RouteStep GetStep(int stepNumber)
        {
            return steps.Find(s => s.stepOrder == stepNumber);
        }

        public int GetTotalSteps()
        {
            return steps.Count;
        }

        public List<RouteStep> GetAllSteps()
        {
            steps.Sort((a, b) => a.stepOrder.CompareTo(b.stepOrder));
            return steps;
        }
    }
}
