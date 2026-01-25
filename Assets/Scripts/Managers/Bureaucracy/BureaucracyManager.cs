using System.Collections.Generic;
using UnityEngine;
using Enums;
using Characters;

namespace Managers.Bureaucracy
{
    public class BureaucracyManager : MonoBehaviour
    {
        public static BureaucracyManager Instance { get; private set; }

        [Header("База маршрутов")]
        public List<Data.Bureaucracy.BureaucracyRoute> allRoutes;

        [Header("Состояние документов")]
        private Dictionary<string, int> documentProgress = new Dictionary<string, int>();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
            }
        }

        public Data.Bureaucracy.BureaucracyRoute GetRoute(ProjectDocumentType docType)
        {
            if (allRoutes == null) return null;

            foreach (var route in allRoutes)
            {
                if (route != null && route.documentType == docType)
                {
                    return route;
                }
            }

            // Если маршрут не найден, возвращаем базовый
            return CreateDefaultRoute(docType);
        }

        private Data.Bureaucracy.BureaucracyRoute CreateDefaultRoute(ProjectDocumentType docType)
        {
            var defaultRoute = new Data.Bureaucracy.BureaucracyRoute
            {
                routeID = "DEFAULT_" + docType.ToString(),
                routeName = "Стандартный маршрут",
                documentType = docType,
                steps = new List<Data.Bureaucracy.BureaucracyRoute.RouteStep>
                {
                    new Data.Bureaucracy.BureaucracyRoute.RouteStep
                    {
                        stepOrder = 1,
                        stepName = "Регистрация",
                        targetPoint = ServicePointType.Registration,
                        requireSignature = false,
                        requirePayment = false,
                        allowedRoles = new List<StaffController.Role>
                        {
                            StaffController.Role.Registrar,
                            StaffController.Role.Clerk
                        }
                    },
                    new Data.Bureaucracy.BureaucracyRoute.RouteStep
                    {
                        stepOrder = 2,
                        stepName = "Подпись директора",
                        targetPoint = ServicePointType.DirectorDesk,
                        requireSignature = true,
                        requirePayment = false,
                        directorCanProcess = true,
                        allowedRoles = new List<StaffController.Role>
                        {
                            StaffController.Role.Director
                        }
                    },
                    new Data.Bureaucracy.BureaucracyRoute.RouteStep
                    {
                        stepOrder = 3,
                        stepName = "Оплата",
                        targetPoint = ServicePointType.Cashier,
                        requireSignature = false,
                        requirePayment = true,
                        allowedRoles = new List<StaffController.Role>
                        {
                            StaffController.Role.Cashier
                        }
                    },
                    new Data.Bureaucracy.BureaucracyRoute.RouteStep
                    {
                        stepOrder = 4,
                        stepName = "Архивирование",
                        targetPoint = ServicePointType.Archive,
                        requireSignature = false,
                        requirePayment = false,
                        allowedRoles = new List<StaffController.Role>
                        {
                            StaffController.Role.Archivist
                        }
                    }
                }
            };

            return defaultRoute;
        }

        public int GetDocumentProgress(string documentID)
        {
            return documentProgress.ContainsKey(documentID) ? documentProgress[documentID] : 0;
        }

        public void AdvanceDocumentProgress(string documentID)
        {
            if (!documentProgress.ContainsKey(documentID))
            {
                documentProgress[documentID] = 0;
            }
            documentProgress[documentID]++;
        }

        public void ResetDocumentProgress(string documentID)
        {
            documentProgress[documentID] = 0;
        }

        public bool IsDocumentComplete(string documentID)
        {
            var route = GetRoute(global::Enums.ProjectDocumentType.Policy); // Упрощенно
            if (route == null) return false;
            int progress = GetDocumentProgress(documentID);
            return progress >= route.GetTotalSteps();
        }
    }
}
