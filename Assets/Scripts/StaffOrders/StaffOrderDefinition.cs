using System;
using System.Collections.Generic;
using Managers;
using Scriptables.Progression;
using UnityEngine;

namespace StaffOrders
{
    /// <summary>
    /// Пункт меню приказов: какое действие приказать, кому (роли) и с какой должности директора он открывается.
    /// </summary>
    [Serializable]
    public class StaffOrderDefinition
    {
        /// <summary>
        /// Текст пункта меню, например «Сходить в туалет».
        /// </summary>
        [field: SerializeField]
        public string Label { get; private set; }

        [field: SerializeField]
        public StaffAction Action { get; private set; }

        /// <summary>
        /// Роли, которым доступен приказ. Пустой список — приказ общий для всех.
        /// </summary>
        [field: SerializeField]
        [field: Tooltip("Пусто — приказ доступен всем ролям.")]
        public List<StaffController.Role> Roles { get; private set; } = new List<StaffController.Role>();

        /// <summary>
        /// Должность директора, которая открывает приказ. Пусто — приказ доступен с начала игры.
        /// </summary>
        [field: SerializeField]
        [field: Tooltip("Должность директора, которая открывает приказ. Пусто — доступен сразу.")]
        public JobTitleData RequiredJob { get; private set; }

        /// <summary>
        /// Сколько стресса сотрудник получает сразу при получении приказа.
        /// </summary>
        [field: SerializeField]
        [field: Range(0f, 50f)]
        public float StressCost { get; private set; } = StaffOrder.DefaultStressCost;

        // Конструктор без параметров нужен Unity для десериализации.
        public StaffOrderDefinition()
        {
        }

        public StaffOrderDefinition(string label, StaffAction action, params StaffController.Role[] roles)
        {
            Label = label;
            Action = action;
            Roles = new List<StaffController.Role>(roles);
        }

        public bool IsAvailableFor(StaffController staff)
        {
            if (staff == null || Action == null) return false;
            if (Roles != null && Roles.Count > 0 && !Roles.Contains(staff.currentRole)) return false;
            if (RequiredJob == null) return true;

            return ProgressionManager.Instance != null && ProgressionManager.Instance.IsJobUnlocked(RequiredJob.jobID);
        }
    }
}
