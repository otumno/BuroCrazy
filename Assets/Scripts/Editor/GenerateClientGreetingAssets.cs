using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using Data;
using Enums;

public class GenerateClientGreetingAssets : EditorWindow
{
    [MenuItem("Bureau/Generate Client Greeting Assets")]
    public static void ShowWindow()
    {
        GetWindow<GenerateClientGreetingAssets>("Generate Greeting Assets");
    }

    void OnGUI()
    {
        GUILayout.Label("Создание ассетов системы общения клиентов", EditorStyles.boldLabel);
        GUILayout.Space(10);

        if (GUILayout.Button("СОЗДАТЬ ВСЕ АССЕТЫ", GUILayout.Height(40)))
        {
            GenerateAllAssets();
        }

        GUILayout.Space(10);
        GUILayout.Label("Или создать по отдельности:", EditorStyles.label);

        if (GUILayout.Button("ClientGreetingDatabase"))
        {
            CreateGreetingDatabase();
        }

        if (GUILayout.Button("Scene: Conflict In Queue"))
        {
            CreateScene_ConflictInQueue();
        }

        if (GUILayout.Button("Scene: Weather Comment"))
        {
            CreateScene_WeatherComment();
        }

        if (GUILayout.Button("Scene: Director Appears"))
        {
            CreateScene_DirectorAppears();
        }

        if (GUILayout.Button("Scene: Group Grumble"))
        {
            CreateScene_GroupGrumble();
        }

        if (GUILayout.Button("Scene: Happy Exit"))
        {
            CreateScene_HappyExit();
        }
    }

    [MenuItem("Bureau/Generate Client Greeting Assets")]
    static void GenerateAllAssets()
    {
        CreateGreetingDatabase();
        CreateScene_ConflictInQueue();
        CreateScene_WeatherComment();
        CreateScene_DirectorAppears();
        CreateScene_GroupGrumble();
        CreateScene_HappyExit();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("<color=green>Все ассеты системы общения клиентов созданы!</color>");
    }

    static void CreateGreetingDatabase()
    {
        string path = "Assets/Resources/ClientGreetingDatabase.asset";

        var database = AssetDatabase.LoadAssetAtPath<ClientGreetingDatabase>(path);
        if (database == null)
        {
            database = ScriptableObject.CreateInstance<ClientGreetingDatabase>();
            AssetDatabase.CreateAsset(database, path);
            Debug.Log($"Создан: {path}");
        }

        // Приветствия к работникам
        database.greetingsToRegistrar = new List<string> {
            "Здравствуйте!", "Добрый день!", "Можно к регистратуре?", "Мне бы записаться...",
            "Здравствуйте, мне нужно к регистратуре.", "Подскажите, что мне нужно?",
            "День добрый! Я первый раз тут.", "Здравствуйте, хотел бы узнать...",
            "Можно войти?", "Добрый день! Подскажите порядок действий."
        };

        database.greetingsToClerk = new List<string> {
            "Здравствуйте!", "Добрый день!", "Мне бы помочь с документами...",
            "Здравствуйте, можно к вам?", "Мне бы консультацию...", "День добрый! У меня вопрос...",
            "Здравствуйте, я по поводу...", "Подскажите, куда мне обратиться?",
            "Добрый день! Помогите разобраться...", "Здравствуйте, у меня дело..."
        };

        database.greetingsToCashier = new List<string> {
            "День добрый!", "Деньги принёс!", "Плачу!", "Здравствуйте, вот оплата.",
            "Добрый день! Квитанция на оплату.", "Здравствуйте, сколько с меня?",
            "День добрый! Принимаете картой?", "Вот, держите!", "Оплата!",
            "Добрый день! Наличными можно?"
        };

        database.greetingsToDirector = new List<string> {
            "Здравствуйте!", "Добрый день!", "Ооо, здравствуйте!", "День добрый!",
            "Здравствуйте, не знал что вы тут...", "Добрый день! Простите, не заметил...",
            "Здравствуйте!", "Добрый день! Рад видеть!"
        };

        // Прощания
        database.farewellsHappy = new List<string> {
            "Спасибо большое!", "Спасибо, выручили!", "До свидания!", "Благодарю!",
            "Спасибо вам!", "Вы мне очень помогли!", "Всего доброго!", "Спасибо за помощь!",
            "Очень признателен!", "До свидания, всего хорошего!", "Спасибо, добрый человек!",
            "Вы спасли мой день!", "Премного благодарен!", "Ценю вашу помощь!",
            "Спасибочки!", "Вот спасибочко!", "Дай бог здоровья!", "Славно сработали!",
            "Всё отлично!", "Вы лучший!"
        };

        database.farewellsNeutral = new List<string> {
            "Ну ладно, до свидания...", "До свидания...", "Пока...", "Ну что ж, пойду...",
            "Ладно, справился сам...", "Эх, разобрался как-то...", "Ну и ладно...",
            "Сам справился...", "До встречи...", "Ну что, нормально...",
            "Ладно, пойду отсюда...", "Справился как-то...", "Ну и день прошёл...",
            "Вот так вот...", "Ничего особенного..."
        };

        database.farewellsSad = new List<string> {
            "Ну и ну...", "Как всегда...", "Никогда тут ничего просто...",
            "Эх, лучше бы не приходил...", "Что ж, справился, но какой ценой...",
            "В следующий раз лучше не приду...", "Ну и дела...", "Это было ужасно...",
            "Так себе сервис...", "Ну и зачем это было...", "В другой раз постараюсь не приходить...",
            "Господи, зачем это всё...", "Ужас просто...", "Вот так вот...", "Ну и денёк...",
            "Ну и ну...", "Печально...", "Неприятно...", "Грустно...", "Ну ладно..."
        };

        database.farewellsEnraged = new List<string> {
            "Это безобразие!", "Я вам это припомню!", "Худшее место!", "Позор!",
            "Безобразие полное!", "Никогда больше!", "Я в шоке!", "Это КОШМАР!",
            "ПРОСТО УЖАС!", "Никакого уважения!", "Вот ведь чёрт!", "Ну и гадство!",
            "Стыд и срам!", "Обращусь куда следует!", "Надо ж было так облажаться!",
            "Вот зачем вы работаете вообще?", "Я в бешенстве!", "ИЗДЕВАТЕЛЬСТВО!",
            "ПРОКЛЯТЬЕ!", "СГОРИТЕ ВСЕ!"
        };

        // Small talk
        database.smallTalkQueue = new List<string> {
            "Долго ждать?", "А вы по талону?", "Нервный нынче народ...",
            "Как думаете, долго ещё?", "Очередь не уменьшается...", "Сколько уже стоим...",
            "Надеюсь, не до вечера...", "Вы в какой очереди?", "Талоны ещё остались?",
            "Кто последний?", "А без очереди нельзя?", "Всего один сотрудник работает?",
            "Почему так медленно?", "Ну и система...", "Электронная очередь, а толку...",
            "Это надолго?", "Кто-нибудь знает, куда идти?", "А мне сначала куда?",
            "Первый раз тут, подскажите...", "Народу сегодня много..."
        };

        database.smallTalkWeather = new List<string> {
            "Дождь-то как польёт...", "Свежо сегодня...", "Жара просто...",
            "Холодно на улице...", "Снег идёт...", "Солнце вышло...", "Ветрено...",
            "Слякоть...", "Народу сегодня...", "Праздники скоро...", "Грибной сезон...",
            "Урожай нынче...", "Слышали новости?", "Вот и лето прошло...", "Осень на дворе...",
            "Зима близко...", "Весна пришла...", "Хороший денёк...", "Непогода...",
            "Духота какая..."
        };

        database.smallTalkStaff = new List<string> {
            "А чего это директор не выходит?", "Слышали, нового клерка взяли?",
            "А старый куда делся?", "Почему один работает, а другие стоят?",
            "Это кто такой?", "А где обычно тут сидят?", "Кого ждать-то?",
            "А это точно нужная очередь?", "Кто принимает вон там?", "Мне к кому идти?"
        };

        EditorUtility.SetDirty(database);
        Debug.Log($"Обновлён: {path}");
    }

    static void CreateScene_ConflictInQueue()
    {
        CreateSceneAsset("Assets/Resources/ClientScenes/Scene_ConflictInQueue.asset",
            "conflict_queue", "Конфликт в очереди", ClientSceneType.ClientConflict,
            3, 30f, 0.1f, 120f,
            new List<string> { "Guard", "Director" },
            new List<SceneLineData> {
                new SceneLineData { text = "Вы не в очередь встали!" },
                new SceneLineData { text = "Я тут стоял! Вы ошиблись." },
                new SceneLineData { text = "Какое нахальство! Я была первая!" },
                new SceneLineData { text = "Блин, хватит уже..." }
            },
            true, 1.3f);
    }

    static void CreateScene_WeatherComment()
    {
        CreateSceneAsset("Assets/Resources/ClientScenes/Scene_WeatherComment.asset",
            "weather_comment", "Комментарий о погоде", ClientSceneType.WeatherComment,
            1, 10f, 0.03f, 300f,
            new List<string>(),
            new List<SceneLineData> {
                new SceneLineData { speakerArchetypeID = "Elderly", text = "Дождь-то как польёт..." }
            },
            false, 1f);
    }

    static void CreateScene_DirectorAppears()
    {
        CreateSceneAsset("Assets/Resources/ClientScenes/Scene_DirectorAppears.asset",
            "director_appears", "Появление директора", ClientSceneType.DirectorAppears,
            2, 5f, 0.5f, 180f,
            new List<string>(),
            new List<SceneLineData> {
                new SceneLineData { text = "Ооо, директор вышел!" },
                new SceneLineData { text = "Тише, тише..." }
            },
            false, 1f, true, Emotion.Neutral);
    }

    static void CreateScene_GroupGrumble()
    {
        CreateSceneAsset("Assets/Resources/ClientScenes/Scene_GroupGrumble.asset",
            "group_grumble", "Коллективное ворчание", ClientSceneType.GroupGrumble,
            4, 45f, 0.08f, 180f,
            new List<string>(),
            new List<SceneLineData> {
                new SceneLineData { speakerArchetypeID = "Elderly", requiredGender = 0, text = "Ой, тяжело стоять, ноги болят..." },
                new SceneLineData { speakerArchetypeID = "Elderly", requiredGender = 1, text = "Никакого стульчика для стариков!" },
                new SceneLineData { text = "Вот в наше время очередь была почитаемой..." }
            },
            true, 1.5f, true, Emotion.Irritated);
    }

    static void CreateScene_HappyExit()
    {
        CreateSceneAsset("Assets/Resources/ClientScenes/Scene_HappyExit.asset",
            "happy_exit", "Счастливый уход", ClientSceneType.HappyExit,
            2, 20f, 0.02f, 600f,
            new List<string>(),
            new List<SceneLineData> {
                new SceneLineData { text = "Спасибо, выручили!" },
                new SceneLineData { text = "Вот за это плачу!" }
            },
            false, 1f, true, Emotion.Happy);
    }

    static void CreateSceneAsset(string path, string sceneID, string sceneName, ClientSceneType type,
        int minClients, float minTime, float chance, float cooldown,
        List<string> excludeArchetypes, List<SceneLineData> lines,
        bool causeGrumbling, float grumbleMult, bool affectEmotions = false, Emotion postEmotion = Emotion.Neutral)
    {
        // Создаём папку если нужно
        string folder = System.IO.Path.GetDirectoryName(path);
        if (!System.IO.Directory.Exists(folder))
        {
            System.IO.Directory.CreateDirectory(folder);
        }

        var scene = AssetDatabase.LoadAssetAtPath<ClientSceneData>(path);
        if (scene == null)
        {
            scene = ScriptableObject.CreateInstance<ClientSceneData>();
            AssetDatabase.CreateAsset(scene, path);
            Debug.Log($"Создан: {path}");
        }

        scene.sceneID = sceneID;
        scene.sceneName = sceneName;
        scene.sceneType = type;
        scene.minClientsInZone = minClients;
        scene.minTimeInZone = minTime;
        scene.triggerChance = chance;
        scene.checkInterval = 5f;
        scene.sceneCooldown = cooldown;
        scene.excludeArchetypes = excludeArchetypes;
        scene.causeGrumbling = causeGrumbling;
        scene.grumblingMultiplier = grumbleMult;
        scene.affectEmotions = affectEmotions;
        scene.postSceneEmotion = postEmotion;
        scene.debugMode = false;

        // Конвертируем SceneLineData в SceneLine
        scene.lines = new List<SceneLine>();
        foreach (var lineData in lines)
        {
            var line = new SceneLine {
                speakerArchetypeID = lineData.speakerArchetypeID ?? "",
                requiredGender = lineData.requiredGender,
                text = lineData.text
            };
            scene.lines.Add(line);
        }

        EditorUtility.SetDirty(scene);
    }

    class SceneLineData
    {
        public string speakerArchetypeID;
        public int requiredGender = -1;
        public string text;
    }
}
