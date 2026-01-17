// Assets/Scripts/Characters/Controllers/SmallTalkController.cs
using UnityEngine;
using System.Collections.Generic;
using Data;
using Managers;

[RequireComponent(typeof(StaffController), typeof(ThoughtBubbleController))]
public class SmallTalkController : MonoBehaviour
{
    [Header("Настройки")]
    public SmallTalkDatabase database;
    public float checkRadius = 3.0f;
    public float checkInterval = 1.0f;
    
    [Tooltip("Минимальное время между репликами")]
    public float globalCooldown = 10f;
    [Tooltip("Шанс сказать что-то при встрече (если уже здоровались)")]
    [Range(0f, 1f)] public float chatChance = 0.3f;

    private StaffController myStaff;
    private ThoughtBubbleController bubbles;
    private float nextCheckTime;
    private float nextTalkTime;

    private HashSet<int> seenColleagues = new HashSet<int>();

    void Awake()
    {
        myStaff = GetComponent<StaffController>();
        bubbles = GetComponent<ThoughtBubbleController>();
    }

    void Start()
    {
        if (database == null) database = Resources.Load<SmallTalkDatabase>("SmallTalkDatabase");
    }

    void Update()
    {
        if (Time.timeScale == 0f || !myStaff.IsOnDuty() || myStaff.energy <= 0.1f) return;

        if (Time.time >= nextCheckTime)
        {
            CheckSurroundings();
            nextCheckTime = Time.time + checkInterval + Random.Range(0f, 0.5f);
        }
    }

    public void OnShiftStarted()
    {
        seenColleagues.Clear();
    }

    private void CheckSurroundings()
    {
        if (Time.time < nextTalkTime) return;

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, checkRadius);
        
        foreach (var hit in hits)
        {
            if (hit.gameObject == this.gameObject) continue;

            StaffController otherStaff = hit.GetComponent<StaffController>();
            if (otherStaff == null) continue;

            // Директор
            if (otherStaff is DirectorAvatarController)
            {
                Say(database.GetRandomDirectorReaction(), Color.yellow, 2.0f);
                return;
            }

            // Коллеги
            int otherID = otherStaff.gameObject.GetInstanceID();

            if (!seenColleagues.Contains(otherID))
            {
                seenColleagues.Add(otherID);
                
                // --- ЛОГИКА ВРЕМЕНИ ---
                var currentPeriod = TimeManager.Instance != null ? TimeManager.Instance.GetCurrentPeriodType() : Data.Calendar.CalendarDayPeriodType.Day;
                string greeting = database.GetGreetingForTime(currentPeriod);
                // ----------------------

                Say(greeting, Color.white); 
                return; 
            }
            else
            {
                if (Random.value < chatChance)
                {
                    Say(database.GetRandomChatter(), new Color(0.9f, 0.9f, 0.9f));
                    return;
                }
            }
        }
    }

    public void ForceSayPhrase(string text)
    {
        Say(text, Color.green);
    }
    
    public string GetRandomTopic()
    {
        return database != null ? database.GetRandomChatter() : "бла-бла...";
    }

    private void Say(string text, Color color, float cooldownMult = 1.0f)
    {
        if (bubbles != null && database != null)
        {
            bubbles.ShowPriorityMessage(text, 2.5f, color);
            nextTalkTime = Time.time + (globalCooldown * cooldownMult);
        }
    }
}