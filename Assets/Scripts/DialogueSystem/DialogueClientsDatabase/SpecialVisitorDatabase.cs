// Файл: Assets/Scripts/Scriptables/SpecialVisitorDatabase.cs
using UnityEngine;
using System.Collections.Generic;
using DialogueSystem.Data;

[CreateAssetMenu(fileName = "SpecialVisitors", menuName = "Bureau/Special Visitor Database")]
public class SpecialVisitorDatabase : ScriptableObject
{
    [System.Serializable]
    public class ScheduledVisitor
    {
        public string name;
        public int dayToSpawn;
        public DialogueGraph dialogue;
        [Range(0f, 1f)] public float spawnChance = 1f;

        [Header("Настройки Появления")]
        [Tooltip("Если True, появится МГНОВЕННО при загрузке дня (до нажатия 'Начать').")]
        public bool spawnAtStartOfDay; // <--- НОВАЯ ГАЛОЧКА

        [Header("Настройки Звонка")]
        [Tooltip("Если True, персонаж не пойдет к столу, а появится скрыто")]
        public bool isRemoteInteraction; 
        
        [Tooltip("Иконка для стола (Телефон, Конверт).")]
        public Sprite deskIconOverride; 
    }

    public List<ScheduledVisitor> visitors;
}