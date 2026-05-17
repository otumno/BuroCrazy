// === FILE: Assets/Scripts/Cinematic/CinematicNode.cs ===
using System.Collections;
using UnityEngine;

namespace CinematicSystem
{
    /// <summary>
    /// Абстрактный базовый класс для всех узлов кинематического графа.
    /// Каждый узел инкапсулирует отдельное действие (движение, диалог, событие и т.д.).
    /// </summary>
    public abstract class CinematicNode : ScriptableObject
    {
        /// <summary>Уникальный идентификатор узла (GUID)</summary>
        [HideInInspector]
        public string id;
        
        /// <summary>Название узла для отображения в редакторе</summary>
        [HideInInspector]
        public string nodeName;
        
        /// <summary>Позиция узла в графическом редакторе</summary>
        [HideInInspector]
        public Rect editorPosition;

        /// <summary>
        /// Возвращает тип узла (строка для отображения и JSON).
        /// </summary>
        public abstract string GetNodeType();

        /// <summary>
        /// Выполнить узел. Возвращает корутину.
        /// Если узел асинхронный, внутри вызывается yield return.
        /// После завершения должен вызвать player.GoToNextNode() для перехода к следующему узлу.
        /// </summary>
        public abstract IEnumerator Execute(CinematicPlayer player);
    }

    /// <summary>
    /// Базовый класс для узлов с одним выходом (связью на следующий узел).
    /// Наследники должны вызывать player.GoToNextNode(nextNode) в конце Execute.
    /// </summary>
    public abstract class NextNode : CinematicNode
    {
        /// <summary>Ссылка на следующий узел для перехода после завершения</summary>
        public CinematicNode nextNode;
    }
}