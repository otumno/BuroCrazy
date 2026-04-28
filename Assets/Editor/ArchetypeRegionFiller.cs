using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using Characters;
using Data;
using Enums;
using Scriptables.Progression;

public class ArchetypeRegionFiller : EditorWindow
{
    [MenuItem("Tools/Bureau/Fill Archetypes and Regions")]
    public static void FillAll()
    {
        FillArchetypes();
        FillRegions();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("<color=green>[ArchetypeRegionFiller] Парные архетипы и регионы успешно обновлены!</color>");
    }

    private static void FillArchetypes()
    {
        string archetypeFolder = "Assets/Data/Archetypes";
        if (!AssetDatabase.IsValidFolder(archetypeFolder))
            AssetDatabase.CreateFolder("Assets/Data", "Archetypes");

        foreach (var entry in ArchetypeData.Entries)
        {
            // Создаём два ассета: женский и мужской
            CreateOrUpdateArchetype(entry, Gender.Female, archetypeFolder);
            CreateOrUpdateArchetype(entry, Gender.Male, archetypeFolder);
        }
    }

    private static void CreateOrUpdateArchetype(ArchetypeData.ArchetypeEntry entry, Gender gender, string folder)
    {
        string genderSuffix = gender == Gender.Female ? "Female" : "Male";
        string assetPath = $"{folder}/Archetype_{entry.groupID}_{genderSuffix}.asset";
        ClientArchetype archetype = AssetDatabase.LoadAssetAtPath<ClientArchetype>(assetPath);

        bool isNew = false;
        if (archetype == null)
        {
            archetype = ScriptableObject.CreateInstance<ClientArchetype>();
            isNew = true;
            Debug.Log($"[ArchetypeRegionFiller] Создан новый архетип: {entry.groupID}_{genderSuffix}");
        }
        else
        {
            Debug.Log($"[ArchetypeRegionFiller] Обновлён архетип: {entry.groupID}_{genderSuffix}");
        }

        // --- Общие параметры ---
        archetype.groupID = entry.groupID;
        archetype.archetypeID = $"{entry.archetypeID}_{genderSuffix.ToLower()}";
        archetype.displayName = $"{entry.displayName} ({genderSuffix})";
        archetype.gender = (gender == Gender.Female) ? 0 : 1;

        archetype.patience = entry.patience;
        archetype.speedMultiplier = entry.urgency;
        archetype.grumblingThreshold = 0.5f;
        archetype.grumblingFrequency = 0.5f;

        archetype.allowedGoals = entry.allowedGoals.ToList();
        // directorApprovalChance и canVisitToilet - это параметры ArchetypeEntry, не ClientArchetype

        // Гендерно-специфичные мысли (useGenderSpecificLines = false, т.к. ассет уже для конкретного пола)
        archetype.useGenderSpecificLines = false;

        // Заполняем мысли в зависимости от пола
        if (gender == Gender.Female)
        {
            archetype.thoughtPool = entry.thoughtsFemale ?? entry.thoughtsUniversal;
            archetype.grumblingLines = entry.grumblingFemale ?? entry.grumblingUniversal;
            archetype.happyResponses = entry.happyFemale ?? entry.happyUniversal;
            archetype.angryResponses = entry.sadFemale ?? entry.sadUniversal;
        }
        else
        {
            archetype.thoughtPool = entry.thoughtsMale ?? entry.thoughtsUniversal;
            archetype.grumblingLines = entry.grumblingMale ?? entry.grumblingUniversal;
            archetype.happyResponses = entry.happyMale ?? entry.happyUniversal;
            archetype.angryResponses = entry.sadMale ?? entry.sadUniversal;
        }

        // Визуальные поля оставляем пустыми (будут настроены вручную)

        if (isNew)
        {
            AssetDatabase.CreateAsset(archetype, assetPath);
        }

        EditorUtility.SetDirty(archetype);
    }

    private static void FillRegions()
    {
        string regionFolder = "Assets/Data/Progression";
        string[] regionGuids = AssetDatabase.FindAssets("t:RegionData", new[] { regionFolder });

        foreach (string guid in regionGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            RegionData region = AssetDatabase.LoadAssetAtPath<RegionData>(path);
            if (region == null) continue;

            region.groupWeights.Clear();

            if (ArchetypeData.RegionWeights.TryGetValue(region.regionID, out var weights))
            {
                foreach (var kvp in weights)
                {
                    region.groupWeights.Add(new ArchetypeGroupWeight
                    {
                        groupID = kvp.Key,
                        weight = kvp.Value
                    });
                }
                Debug.Log($"[ArchetypeRegionFiller] Регион {region.regionID} обновлён ({weights.Count} групп)");
            }
            else
            {
                Debug.LogWarning($"[ArchetypeRegionFiller] Для региона {region.regionID} не найдены веса!");
            }

            // Установка unlocksContactIDs
            Dictionary<string, string[]> unlocksMap = new Dictionary<string, string[]>()
            {
                {"Commercial", new string[]{"CleaningService"}},
                {"Park", new string[]{"ClownService"}}
            };

            if (unlocksMap.TryGetValue(region.regionID, out var contactIDs))
            {
                region.unlocksContactIDs = new List<string>(contactIDs);
            }
            else
            {
                region.unlocksContactIDs.Clear();
            }

            EditorUtility.SetDirty(region);
        }
    }
}

// ==================================================================
// ТАБЛИЦА ДАННЫХ (ВСЁ В ОДНОМ МЕСТЕ)
// ==================================================================
public static class ArchetypeData
{
    public class ArchetypeEntry
    {
        public string groupID;
        public string archetypeID;
        public string displayName;

        public float patience;
        public float errorProneness;
        public float wealth;
        public float politeness;
        public float urgency;
        public float directorApprovalChance;
        public bool canVisitToilet;

        public List<ClientGoal> allowedGoals;

        // Универсальные мысли (используются, если гендерные пусты)
        public List<string> thoughtsUniversal;
        public List<string> grumblingUniversal;
        public List<string> happyUniversal;
        public List<string> sadUniversal;

        // Гендерные мысли
        public List<string> thoughtsFemale;
        public List<string> thoughtsMale;
        public List<string> grumblingFemale;
        public List<string> grumblingMale;
        public List<string> happyFemale;
        public List<string> happyMale;
        public List<string> sadFemale;
        public List<string> sadMale;
    }

    // Веса по регионам (привязываем к groupID, пол не важен)
    public static Dictionary<string, Dictionary<string, float>> RegionWeights = new Dictionary<string, Dictionary<string, float>>()
    {
        {"Slums", new Dictionary<string, float> { {"Worker",60}, {"Foreman",5}, {"Elderly",10}, {"Student",10}, {"Shady",30}, {"Marginal",40} }},
        {"Factory", new Dictionary<string, float> { {"Worker",70}, {"Foreman",25}, {"Shady",10} }},
        {"Residential", new Dictionary<string, float> { {"Elderly",60}, {"Student",30}, {"Worker",10}, {"Veteran",10}, {"Bohemian",5} }},
        {"OldTown", new Dictionary<string, float> { {"Elderly",20}, {"Professional",50}, {"Bohemian",30}, {"Aristocrat",10}, {"Veteran",20}, {"Student",5} }},
        {"Business", new Dictionary<string, float> { {"Businessman",60}, {"Entrepreneur",30}, {"Professional",10}, {"Official",10} }},
        {"Center", new Dictionary<string, float> { {"Businessman",30}, {"Official",60}, {"Professional",20}, {"Tourist",10}, {"VIP",10}, {"Aristocrat",5} }},
        {"Commercial", new Dictionary<string, float> { {"Businessman",15}, {"Entrepreneur",40}, {"Shopkeeper",50}, {"Student",5} }},
        {"Redlight", new Dictionary<string, float> { {"Shady",70}, {"Bohemian",20}, {"Entrepreneur",10}, {"Marginal",20} }},
        {"Park", new Dictionary<string, float> { {"Tourist",80}, {"Businessman",10}, {"Bohemian",10}, {"Marginal",5} }},
        {"Elite", new Dictionary<string, float> { {"VIP",70}, {"Aristocrat",30}, {"Official",20} }}
    };

    // ==================== АРХЕТИПЫ ====================
    public static ArchetypeEntry[] Entries = new ArchetypeEntry[]
    {
        // 1. Worker
        new ArchetypeEntry {
            groupID = "Worker", archetypeID = "worker", displayName = "Worker",
            patience = 1.0f, errorProneness = 0.2f, wealth = 0.8f, politeness = 0.6f, urgency = 1.0f,
            directorApprovalChance = 0.05f, canVisitToilet = true,
            allowedGoals = new List<ClientGoal>{ ClientGoal.GetCertificate2, ClientGoal.PayTax },
            thoughtsFemale = new List<string>{ "Надо работать.", "План дали.", "Где бригада?", "Опять отчёт.", "Скорей бы домой." },
            thoughtsMale = new List<string>{ "Надо работать.", "План дали.", "Где бригада?", "Опять отчёт.", "Скорей бы домой." },
            grumblingFemale = new List<string>{ "Долго.", "Я устала.", "Начальство ждёт.", "Быстрее.", "Сколько можно?" },
            grumblingMale = new List<string>{ "Долго.", "Я устал.", "Начальство ждёт.", "Быстрее.", "Сколько можно?" },
            happyFemale = new List<string>{ "Спасибо.", "Наконец.", "Пойду.", "Хорошо.", "До свидания." },
            happyMale = new List<string>{ "Спасибо.", "Наконец.", "Пойду.", "Хорошо.", "До свидания." },
            sadFemale = new List<string>{ "Провал.", "Не повезло.", "Ухожу.", "Грустно.", "Прощайте." },
            sadMale = new List<string>{ "Провал.", "Не повезло.", "Ухожу.", "Грустно.", "Прощайте." }
        },
        // 2. Foreman
        new ArchetypeEntry {
            groupID = "Foreman", archetypeID = "foreman", displayName = "Foreman",
            patience = 0.9f, errorProneness = 0.1f, wealth = 1.1f, politeness = 0.7f, urgency = 1.2f,
            directorApprovalChance = 0.10f, canVisitToilet = true,
            allowedGoals = new List<ClientGoal>{ ClientGoal.Form1A, ClientGoal.Certificate1A },
            thoughtsFemale = new List<string>{ "Надо проверить девочек.", "План висит.", "Где эти лентяйки?", "Опять отчёты...", "Хоть бы премию." },
            thoughtsMale = new List<string>{ "Надо проследить за парнями.", "План горит.", "Где эти бездельники?", "Опять бумажки...", "Хоть бы премию дали." },
            grumblingFemale = new List<string>{ "Сколько можно ждать?", "Я не нанималась!", "Бригадирша всё видит.", "Потом отчитаюсь.", "Опять задерживают." },
            grumblingMale = new List<string>{ "Опять задерживают.", "Сколько можно ждать?", "Я не нанимался!", "Бригадир всё видит.", "Потом отчитаюсь." },
            happyFemale = new List<string>{ "Ну наконец-то!", "Спасибо, девочки.", "Пойду обрадую.", "Вот это по-нашему!", "Быстро сработали." },
            happyMale = new List<string>{ "Ну наконец-то.", "Спасибо, мужики.", "Пойду обрадую.", "Вот это по-нашему!", "Быстро сработали." },
            sadFemale = new List<string>{ "Эх, опять провал.", "Не мой день.", "Расстрою девчонок.", "Ну и ладно.", "Пойду я..." },
            sadMale = new List<string>{ "Эх, опять провал.", "Не мой день.", "Расстрою парней.", "Ну и ладно.", "Пойду я..." }
        },
        // 3. Elderly
        new ArchetypeEntry {
            groupID = "Elderly", archetypeID = "elderly", displayName = "Elderly",
            patience = 1.5f, errorProneness = 0.4f, wealth = 0.7f, politeness = 0.8f, urgency = 0.7f,
            directorApprovalChance = 0.03f, canVisitToilet = true,
            allowedGoals = new List<ClientGoal>{ ClientGoal.Certificate1B, ClientGoal.AskAndLeave },
            thoughtsFemale = new List<string>{ "Ой, молодой человек...", "Помогите бабушке.", "Давно я тут не была.", "Здравствуйте, милый!", "Дай бог здоровья." },
            thoughtsMale = new List<string>{ "Эх, молодёжь...", "Помогите дедушке.", "Давно я тут не был.", "Здравствуйте, уважаемый!", "Дай бог здоровья." },
            grumblingFemale = new List<string>{ "Это несносно!", "Вот бедствие!", "Никакого уважения к старшим!", "Ой, дела-а-а...", "Тяжело стоять-то." },
            grumblingMale = new List<string>{ "Это безобразие!", "В моё время такого не было.", "Никакого уважения.", "Эх, молодёжь.", "Долго ждать." },
            happyFemale = new List<string>{ "Спасибо, милый!", "Бог тебя наградит!", "Вот спасибочко!", "Дай бог тебе здоровья!", "Умница ты, сынок!" },
            happyMale = new List<string>{ "Спасибо, сынок!", "Благодарю.", "Вот это по-нашему.", "Дай бог здоровья.", "Всего хорошего." },
            sadFemale = new List<string>{ "Ну и ну, молодёжь...", "Эх, лучше бы дома осталась.", "Никакого воспитания.", "Печально всё это.", "Грустно." },
            sadMale = new List<string>{ "Эх, жизнь...", "Лучше бы не приходил.", "Никакого уважения.", "Печально.", "Прощайте." }
        },
        // 4. Student
        new ArchetypeEntry {
            groupID = "Student", archetypeID = "student", displayName = "Student",
            patience = 0.7f, errorProneness = 0.3f, wealth = 0.6f, politeness = 0.5f, urgency = 1.3f,
            directorApprovalChance = 0.05f, canVisitToilet = true,
            allowedGoals = new List<ClientGoal>{ ClientGoal.Form2A, ClientGoal.AskAndLeave },
            thoughtsFemale = new List<string>{ "Скорей бы сессия.", "Мальчики, пропустите.", "Опаздываю.", "Где тут Wi-Fi?", "Кринж..." },
            thoughtsMale = new List<string>{ "Скорей бы сессия.", "Девушки, я спешу.", "Опаздываю.", "Где тут Wi-Fi?", "Кринж..." },
            grumblingFemale = new List<string>{ "Сколько можно?", "Я опаздываю!", "Блин, девочки.", "Это надолго?", "Жесть." },
            grumblingMale = new List<string>{ "Сколько можно?", "Я опаздываю!", "Блин, пацаны.", "Это надолго?", "Жесть." },
            happyFemale = new List<string>{ "Наконец-то!", "Спасибочки!", "Всё гуд!", "Я свободна!", "Побежала!" },
            happyMale = new List<string>{ "Наконец-то!", "Пасиб!", "Всё гуд!", "Я свободен!", "Побежал!" },
            sadFemale = new List<string>{ "Ну блин...", "Печалька.", "Как всегда.", "Не повезло.", "Пойду отсюда." },
            sadMale = new List<string>{ "Ну блин...", "Печалька.", "Как всегда.", "Не повезло.", "Пойду отсюда." }
        },
        // 5. Businessman
        new ArchetypeEntry {
            groupID = "Businessman", archetypeID = "businessman", displayName = "Businessman",
            patience = 0.8f, errorProneness = 0.1f, wealth = 1.5f, politeness = 0.9f, urgency = 1.1f,
            directorApprovalChance = 0.15f, canVisitToilet = false,
            allowedGoals = new List<ClientGoal>{ ClientGoal.Form1B, ClientGoal.Certificate1A },
            thoughtsFemale = new List<string>{ "Время — деньги.", "Успеть бы на встречу.", "Клиентка ждёт.", "Быстрее бы.", "Где мой кофе?" },
            thoughtsMale = new List<string>{ "Время — деньги.", "Успеть бы на встречу.", "Клиент ждёт.", "Быстрее бы.", "Где мой кофе?" },
            grumblingFemale = new List<string>{ "Это возмутительно.", "Я теряю деньги.", "Ваша бюрократия...", "Сколько можно ждать?", "Я буду жаловаться." },
            grumblingMale = new List<string>{ "Это возмутительно.", "Я теряю деньги.", "Ваша бюрократия...", "Сколько можно ждать?", "Я буду жаловаться." },
            happyFemale = new List<string>{ "Отлично.", "Благодарю.", "Оперативно.", "Приятно иметь дело.", "В следующий раз только к вам." },
            happyMale = new List<string>{ "Отлично.", "Благодарю.", "Оперативно.", "Приятно иметь дело.", "В следующий раз только к вам." },
            sadFemale = new List<string>{ "Разочарована.", "Теряю время.", "Никогда больше.", "Ужасный сервис.", "Пойду к конкурентам." },
            sadMale = new List<string>{ "Разочарован.", "Теряю время.", "Никогда больше.", "Ужасный сервис.", "Пойду к конкурентам." }
        },
        // 6. Entrepreneur
        new ArchetypeEntry {
            groupID = "Entrepreneur", archetypeID = "entrepreneur", displayName = "Entrepreneur",
            patience = 0.9f, errorProneness = 0.2f, wealth = 1.2f, politeness = 0.8f, urgency = 1.0f,
            directorApprovalChance = 0.10f, canVisitToilet = true,
            allowedGoals = new List<ClientGoal>{ ClientGoal.Form2B, ClientGoal.PayTax },
            thoughtsFemale = new List<string>{ "Новый проект ждёт.", "Надо успеть.", "Бизнес не ждёт.", "Где моя команда?", "Всё сама." },
            thoughtsMale = new List<string>{ "Новый проект ждёт.", "Надо успеть.", "Бизнес не ждёт.", "Где моя команда?", "Всё сам." },
            grumblingFemale = new List<string>{ "Опять задержки.", "Я так не работаю.", "Время — ресурс.", "Бюрократы.", "Скорее бы." },
            grumblingMale = new List<string>{ "Опять задержки.", "Я так не работаю.", "Время — ресурс.", "Бюрократы.", "Скорее бы." },
            happyFemale = new List<string>{ "Спасибо.", "Быстро.", "Продолжу работу.", "Отличный сервис.", "Вернусь." },
            happyMale = new List<string>{ "Спасибо.", "Быстро.", "Продолжим работу.", "Отличный сервис.", "Вернусь." },
            sadFemale = new List<string>{ "Ну и ладно.", "Провал.", "Сама виновата.", "В другой раз.", "Пойду." },
            sadMale = new List<string>{ "Ну и ладно.", "Провал.", "Сам виноват.", "В другой раз.", "Пойду." }
        },
        // 7. Shopkeeper
        new ArchetypeEntry {
            groupID = "Shopkeeper", archetypeID = "shopkeeper", displayName = "Shopkeeper",
            patience = 1.0f, errorProneness = 0.3f, wealth = 1.0f, politeness = 0.7f, urgency = 0.9f,
            directorApprovalChance = 0.05f, canVisitToilet = true,
            allowedGoals = new List<ClientGoal>{ ClientGoal.Form1C, ClientGoal.Certificate2A },
            thoughtsFemale = new List<string>{ "Лавка без присмотра.", "Надо быстрее.", "Товар ждёт.", "Опять эти бумаги.", "Скорей бы." },
            thoughtsMale = new List<string>{ "Лавка без присмотра.", "Надо быстрее.", "Товар ждёт.", "Опять эти бумаги.", "Скорей бы." },
            grumblingFemale = new List<string>{ "Сколько можно?", "Я покупателей теряю.", "Очереди везде.", "Быстрее давайте.", "Ну сколько ждать?" },
            grumblingMale = new List<string>{ "Сколько можно?", "Я покупателей теряю.", "Очереди везде.", "Быстрее давайте.", "Ну сколько ждать?" },
            happyFemale = new List<string>{ "Спасибо.", "Хорошего дня.", "Быстро вы.", "Пойду торговать.", "До свидания." },
            happyMale = new List<string>{ "Спасибо.", "Хорошего дня.", "Быстро вы.", "Пойду торговать.", "До свидания." },
            sadFemale = new List<string>{ "Эх, дела.", "Покупатели уйдут.", "Ну и ладно.", "Пойду.", "В следующий раз." },
            sadMale = new List<string>{ "Эх, дела.", "Покупатели уйдут.", "Ну и ладно.", "Пойду.", "В следующий раз." }
        },
        // 8. Shady
        new ArchetypeEntry {
            groupID = "Shady", archetypeID = "shady", displayName = "Shady",
            patience = 0.6f, errorProneness = 0.8f, wealth = 1.3f, politeness = 0.3f, urgency = 1.2f,
            directorApprovalChance = 0.20f, canVisitToilet = true,
            allowedGoals = new List<ClientGoal>{ ClientGoal.Form1B, ClientGoal.PayTax },
            thoughtsFemale = new List<string>{ "Только бы не спалили.", "Главное — уйти.", "Быстрее, быстрее.", "Не смотри на меня.", "Деньги есть." },
            thoughtsMale = new List<string>{ "Только бы не спалили.", "Главное — свалить.", "Быстрее, быстрее.", "Не смотри на меня.", "Деньги есть." },
            grumblingFemale = new List<string>{ "Чё так долго?", "Я спешу.", "Давай быстрей.", "Не тяни.", "Мне некогда." },
            grumblingMale = new List<string>{ "Чё так долго?", "Я спешу.", "Давай быстрей.", "Не тяни.", "Мне некогда." },
            happyFemale = new List<string>{ "Ну наконец.", "Валим отсюда.", "Повезло.", "Спасибо, шеф.", "Не поминай лихом." },
            happyMale = new List<string>{ "Ну наконец.", "Валим отсюда.", "Повезло.", "Спасибо, шеф.", "Не поминай лихом." },
            sadFemale = new List<string>{ "Попалась.", "Обманули.", "Кидалово.", "Ну и чёрт с вами.", "Я ухожу." },
            sadMale = new List<string>{ "Попался.", "Обманули.", "Кидалово.", "Ну и чёрт с вами.", "Я ухожу." }
        },
        // 9. Professional
        new ArchetypeEntry {
            groupID = "Professional", archetypeID = "professional", displayName = "Professional",
            patience = 1.2f, errorProneness = 0.0f, wealth = 1.2f, politeness = 1.0f, urgency = 0.9f,
            directorApprovalChance = 0.15f, canVisitToilet = false,
            allowedGoals = new List<ClientGoal>{ ClientGoal.Form1A, ClientGoal.Certificate1A },
            thoughtsFemale = new List<string>{ "Всё по протоколу.", "Надеюсь, без ошибок.", "Проверю каждую запятую.", "Время терпит.", "Спокойствие." },
            thoughtsMale = new List<string>{ "Всё по протоколу.", "Надеюсь, без ошибок.", "Проверю каждую запятую.", "Время терпит.", "Спокойствие." },
            grumblingFemale = new List<string>{ "Это не по стандарту.", "Я настаиваю на качестве.", "Переделайте.", "Так не пойдёт.", "Где печать?" },
            grumblingMale = new List<string>{ "Это не по стандарту.", "Я настаиваю на качестве.", "Переделайте.", "Так не пойдёт.", "Где печать?" },
            happyFemale = new List<string>{ "Идеально.", "Благодарю.", "Всё верно.", "Приятно работать.", "До встречи." },
            happyMale = new List<string>{ "Идеально.", "Благодарю.", "Всё верно.", "Приятно работать.", "До встречи." },
            sadFemale = new List<string>{ "Разочарована.", "Ожидала большего.", "Некомпетентность.", "В следующий раз к другим.", "Прощайте." },
            sadMale = new List<string>{ "Разочарован.", "Ожидал большего.", "Некомпетентность.", "В следующий раз к другим.", "Прощайте." }
        },
        // 10. Bohemian
        new ArchetypeEntry {
            groupID = "Bohemian", archetypeID = "bohemian", displayName = "Bohemian",
            patience = 1.1f, errorProneness = 0.5f, wealth = 0.9f, politeness = 0.6f, urgency = 0.8f,
            directorApprovalChance = 0.05f, canVisitToilet = true,
            allowedGoals = new List<ClientGoal>{ ClientGoal.AskAndLeave, ClientGoal.Certificate2A },
            thoughtsFemale = new List<string>{ "Вдохновение ждёт.", "Как здесь скучно.", "Надо бы музу.", "Очереди убивают.", "Может, стихи?" },
            thoughtsMale = new List<string>{ "Вдохновение ждёт.", "Как здесь скучно.", "Надо бы музу.", "Очереди убивают.", "Может, стихи?" },
            grumblingFemale = new List<string>{ "Сколько можно?", "Я теряю рифму.", "Бюрократы душат.", "Это невыносимо.", "Где свобода?" },
            grumblingMale = new List<string>{ "Сколько можно?", "Я теряю рифму.", "Бюрократы душат.", "Это невыносимо.", "Где свобода?" },
            happyFemale = new List<string>{ "Благодарю.", "Вы прекрасны.", "Всё чудесно.", "До встречи.", "Удачи." },
            happyMale = new List<string>{ "Благодарю.", "Вы прекрасны.", "Всё чудесно.", "До встречи.", "Удачи." },
            sadFemale = new List<string>{ "Печаль.", "Мир жесток.", "Искусство мертво.", "Пойду страдать.", "Прощайте." },
            sadMale = new List<string>{ "Печаль.", "Мир жесток.", "Искусство мертво.", "Пойду страдать.", "Прощайте." }
        },
        // 11. Tourist
        new ArchetypeEntry {
            groupID = "Tourist", archetypeID = "tourist", displayName = "Tourist",
            patience = 1.0f, errorProneness = 0.2f, wealth = 1.8f, politeness = 0.7f, urgency = 1.0f,
            directorApprovalChance = 0.10f, canVisitToilet = true,
            allowedGoals = new List<ClientGoal>{ ClientGoal.Certificate1C },
            thoughtsFemale = new List<string>{ "Красивый город.", "Надо сувенир.", "Где тут музей?", "Ничего не понимаю.", "Помогите!" },
            thoughtsMale = new List<string>{ "Красивый город.", "Надо сувенир.", "Где тут музей?", "Ничего не понимаю.", "Помогите!" },
            grumblingFemale = new List<string>{ "Долго.", "Я не местная.", "Почему так сложно?", "У нас проще.", "Сколько ждать?" },
            grumblingMale = new List<string>{ "Долго.", "Я не местный.", "Почему так сложно?", "У нас проще.", "Сколько ждать?" },
            happyFemale = new List<string>{ "Thank you!", "Спасибо!", "Отличная страна.", "Всё поняла.", "До свидания!" },
            happyMale = new List<string>{ "Thank you!", "Спасибо!", "Отличная страна.", "Всё понял.", "До свидания!" },
            sadFemale = new List<string>{ "Oh no.", "Печально.", "Не повезло.", "Больше не приеду.", "Прощайте." },
            sadMale = new List<string>{ "Oh no.", "Печально.", "Не повезло.", "Больше не приеду.", "Прощайте." }
        },
        // 12. Official
        new ArchetypeEntry {
            groupID = "Official", archetypeID = "official", displayName = "Official",
            patience = 1.3f, errorProneness = 0.0f, wealth = 1.0f, politeness = 1.0f, urgency = 0.8f,
            directorApprovalChance = 0.25f, canVisitToilet = false,
            allowedGoals = new List<ClientGoal>{ ClientGoal.Form1C, ClientGoal.DirectorApproval },
            thoughtsFemale = new List<string>{ "Всё по регламенту.", "Порядок превыше всего.", "Надо проверить.", "Без бумажки — букашка.", "Спокойно." },
            thoughtsMale = new List<string>{ "Всё по регламенту.", "Порядок превыше всего.", "Надо проверить.", "Без бумажки — букашка.", "Спокойно." },
            grumblingFemale = new List<string>{ "Нарушение процедуры.", "Я буду жаловаться.", "Это не по уставу.", "Переделать.", "Где гербовая печать?" },
            grumblingMale = new List<string>{ "Нарушение процедуры.", "Я буду жаловаться.", "Это не по уставу.", "Переделать.", "Где гербовая печать?" },
            happyFemale = new List<string>{ "Порядок.", "Всё верно.", "Благодарю.", "Сотрудничество окончено.", "До свидания." },
            happyMale = new List<string>{ "Порядок.", "Всё верно.", "Благодарю.", "Сотрудничество окончено.", "До свидания." },
            sadFemale = new List<string>{ "Безобразие.", "Я это так не оставлю.", "Доложу начальству.", "Некомпетентность.", "Прощайте." },
            sadMale = new List<string>{ "Безобразие.", "Я это так не оставлю.", "Доложу начальству.", "Некомпетентность.", "Прощайте." }
        },
        // 13. VIP
        new ArchetypeEntry {
            groupID = "VIP", archetypeID = "vip", displayName = "VIP",
            patience = 0.9f, errorProneness = 0.0f, wealth = 3.0f, politeness = 1.0f, urgency = 1.1f,
            directorApprovalChance = 0.0f, canVisitToilet = false,
            allowedGoals = new List<ClientGoal>{ ClientGoal.DirectorAudience, ClientGoal.Certificate1A },
            thoughtsFemale = new List<string>{ "Надеюсь, особое отношение.", "Я не привыкла ждать.", "Где мой менеджер?", "Всё должно быть идеально.", "Быстрее." },
            thoughtsMale = new List<string>{ "Надеюсь, особое отношение.", "Я не привык ждать.", "Где мой менеджер?", "Всё должно быть идеально.", "Быстрее." },
            grumblingFemale = new List<string>{ "Это возмутительно.", "Вы знаете, кто я?", "Я этого так не оставлю.", "Позовите директора.", "Моё время дорого." },
            grumblingMale = new List<string>{ "Это возмутительно.", "Вы знаете, кто я?", "Я этого так не оставлю.", "Позовите директора.", "Моё время дорого." },
            happyFemale = new List<string>{ "Прекрасно.", "Благодарю.", "Вы знаете своё дело.", "Я довольна.", "Обращусь ещё." },
            happyMale = new List<string>{ "Прекрасно.", "Благодарю.", "Вы знаете своё дело.", "Я доволен.", "Обращусь ещё." },
            sadFemale = new List<string>{ "Разочарована.", "Никогда больше.", "Ужасное место.", "Вы потеряли клиентку.", "Прощайте." },
            sadMale = new List<string>{ "Разочарован.", "Никогда больше.", "Ужасное место.", "Вы потеряли клиента.", "Прощайте." }
        },
        // 14. Aristocrat
        new ArchetypeEntry {
            groupID = "Aristocrat", archetypeID = "aristocrat", displayName = "Aristocrat",
            patience = 1.4f, errorProneness = 0.1f, wealth = 2.5f, politeness = 1.0f, urgency = 0.7f,
            directorApprovalChance = 0.20f, canVisitToilet = false,
            allowedGoals = new List<ClientGoal>{ ClientGoal.DirectorApproval, ClientGoal.Form1A },
            thoughtsFemale = new List<string>{ "Традиции требуют.", "В наше время было иначе.", "Уважение к старшим.", "Всему своё время.", "Спешка не к лицу." },
            thoughtsMale = new List<string>{ "Традиции требуют.", "В наше время было иначе.", "Уважение к старшим.", "Всему своё время.", "Спешка не к лицу." },
            grumblingFemale = new List<string>{ "Молодёжь пошла.", "Никакого воспитания.", "В мои годы такого не было.", "Это неприлично.", "Я требую уважения." },
            grumblingMale = new List<string>{ "Молодёжь пошла.", "Никакого воспитания.", "В мои годы такого не было.", "Это неприлично.", "Я требую уважения." },
            happyFemale = new List<string>{ "Благодарю.", "Вы достойный человек.", "Приятно иметь дело.", "Всего наилучшего.", "Честь имею." },
            happyMale = new List<string>{ "Благодарю.", "Вы достойный человек.", "Приятно иметь дело.", "Всего наилучшего.", "Честь имею." },
            sadFemale = new List<string>{ "Печально.", "Времена меняются.", "Ухожу.", "Не ожидала.", "Прощайте." },
            sadMale = new List<string>{ "Печально.", "Времена меняются.", "Ухожу.", "Не ожидал.", "Прощайте." }
        },
        // 15. Veteran
        new ArchetypeEntry {
            groupID = "Veteran", archetypeID = "veteran", displayName = "Veteran",
            patience = 1.6f, errorProneness = 0.3f, wealth = 0.5f, politeness = 0.9f, urgency = 0.6f,
            directorApprovalChance = 0.05f, canVisitToilet = true,
            allowedGoals = new List<ClientGoal>{ ClientGoal.Certificate1B },
            thoughtsFemale = new List<string>{ "Служба не забыта.", "Льготы положены.", "Потихоньку.", "Куда спешить?", "Всё пройдём." },
            thoughtsMale = new List<string>{ "Служба не забыта.", "Льготы положены.", "Потихоньку.", "Куда спешить?", "Всё пройдём." },
            grumblingFemale = new List<string>{ "В наше время быстро делали.", "Молодёжь.", "Никакого уважения.", "Я заслужила.", "Долго." },
            grumblingMale = new List<string>{ "В наше время быстро делали.", "Молодёжь.", "Никакого уважения.", "Я заслужил.", "Долго." },
            happyFemale = new List<string>{ "Спасибо, дочка.", "Будьте здоровы.", "Всё путём.", "До свидания.", "Служба закончена." },
            happyMale = new List<string>{ "Спасибо, сынок.", "Будьте здоровы.", "Всё путём.", "До свидания.", "Служба закончена." },
            sadFemale = new List<string>{ "Эх, жизнь.", "Не ценят.", "Пойду.", "Грустно.", "Прощайте." },
            sadMale = new List<string>{ "Эх, жизнь.", "Не ценят.", "Пойду.", "Грустно.", "Прощайте." }
        },
        // 16. Marginal
        new ArchetypeEntry {
            groupID = "Marginal", archetypeID = "marginal", displayName = "Marginal",
            patience = 0.5f, errorProneness = 0.9f, wealth = 0.3f, politeness = 0.2f, urgency = 1.4f,
            directorApprovalChance = 0.02f, canVisitToilet = true,
            allowedGoals = new List<ClientGoal>{ ClientGoal.AskAndLeave, ClientGoal.PayTax },
            thoughtsFemale = new List<string>{ "Где тут поесть?", "Тепло бы.", "Мне бы справку.", "Ничего не надо.", "Только спросить." },
            thoughtsMale = new List<string>{ "Где тут поесть?", "Тепло бы.", "Мне бы справку.", "Ничего не надо.", "Только спросить." },
            grumblingFemale = new List<string>{ "Чё так долго?", "Издеваетесь?", "Я есть хочу.", "Холодно.", "Пустите погреться." },
            grumblingMale = new List<string>{ "Чё так долго?", "Издеваетесь?", "Я есть хочу.", "Холодно.", "Пустите погреться." },
            happyFemale = new List<string>{ "Спасибо.", "Пойду.", "Ладно.", "Бывайте.", "До встречи." },
            happyMale = new List<string>{ "Спасибо.", "Пойду.", "Ладно.", "Бывайте.", "До встречи." },
            sadFemale = new List<string>{ "Ну и ладно.", "Как всегда.", "Не везёт.", "Пойду отсюда.", "Прощайте." },
            sadMale = new List<string>{ "Ну и ладно.", "Как всегда.", "Не везёт.", "Пойду отсюда.", "Прощайте." }
        }
    };
}