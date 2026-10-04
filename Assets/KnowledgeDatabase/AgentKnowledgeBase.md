# BuroCrazy — база знаний для AI-агента

> Назначение: дать агенту, который начинает работу с проектом, всё нужное, чтобы не изучать репозиторий с нуля.
> Актуально на **2026-10-04** (ветка `Dacha`, коммит `9bb8d81`). Перед тем как опираться на конкретный класс или путь, проверь, что он всё ещё существует.
> Статус фич для людей лежит в соседнем файле `TeamStatus.md`.

---

## 1. Что за игра (коротко)

**Bureau Crazy** — сатирический 2D-симулятор управления госучреждением (тайкун + tower defense) в вымышленной стране «НМР Бренвельзия», стилистика альтернативных 1960-х. Референсы: Papers, Please, Two Point Hospital, Overcooked.

- Игрок — **Директор**, физический юнит в офисе: ходит кликом, может сесть за любой стол, подписывает документы за своим столом, разговаривает с VIP-посетителями.
- **Клиенты** — «волны врагов»: у каждого есть архетип, цель визита и терпение.
- **Сотрудники** — автономные агенты на utility AI, с потребностями (туалет, энергия, мораль, стресс) и навыками.
- **Мета-слой**: влияние (Influence), захват районов города, карьерная лестница от Директора до Министра.
- **Длительность партии**: 30 игровых дней (`AIBalanceConfig.finalDay`). Финал: визит Инспектора, затем концовка по доминирующей черте директора.
- **Нарратив**: сюжетные арки — оммажи на классику (Оруэлл, Булгаков, Достоевский…), разыгрываются через диалоги с посетителями.

Платформа: PC и Steam (Steamworks.NET подключён, `steam_appid.txt` лежит в корне), с прицелом на Steam Deck.

---

## 2. Источник дизайна — Yonote

Гейм-дизайн документация лежит в Yonote: `https://mindeaterteaceremony.yonote.ru`. Yonote — форк Outline.

**Доступ через API** (в браузере сессии нет, логиниться за пользователя нельзя):
- Токен лежит в env-переменной `YONOTE_API_TOKEN`, она задаётся в `.claude/settings.local.json`. Значение токена никогда не выводи.
- Запросы: `POST https://mindeaterteaceremony.yonote.ru/api/<method>` с заголовками `Authorization: Bearer $YONOTE_API_TOKEN` и `Content-Type: application/json`.
- `collections.list` — 2 коллекции:
  - **«Buro Crazy»** (`d1c58605-…`): Концепт, Механики, Сущности, Структура, Нарратив, Контент, Тех Доки.
  - **«Тех. Задачки»** (`04b0c8e1-…`): «ТЗ» (последнее ТЗ на найм, смены, приказы по ПКМ и список сотрудников в HUD) и «Список задач» (архитектурные советы).
- `documents.list` с `{"collectionId": "...", "limit": 100}` возвращает документы вместе с полем `text` (markdown).
- **Подвох.** Документы с `type: "database"` («Нови Списак Делъ», «Список дел», «Список сделанных дел», «Арки») приходят с пустым текстом. Их строки достаются отдельно: `documents.list` с `{"type": ["row"], "limit": 100}`. Затем фильтруешь по `parentDocumentId`; значения колонок лежат в `values` по id свойств из `properties` базы.
- В базе «Арки» строки содержат JSON сюжетных арок (формат описан в «БИБЛИЯ СЮЖЕТНЫХ АРОК»). Их импортирует `Assets/Editor/ArcJsonImporter*.cs`.

**Важно.** Многие страницы Yonote (особенно разделы «Техническая реализация») писались в конце 2025 года и **расходятся с кодом**. Примеры:
- `ClientSpawner` как «метроном» — сейчас время ведёт `TimeManager`.
- `MapManager`, `CompetitorAI`, `BreakageManager` в коде не существуют.

Источник истины по реализации — код. Yonote — источник истины по замыслу.

Локальные копии дизайн-доков (устаревшие): `.Docs.New/01..08-*.md` и `.Docs/` (Obsidian). Корневые `PROJECT_STATUS.md`, `SUMMARY.md`, `IMPLEMENTATION_GUIDE.md`, `MAP_CAREER_SETUP_GUIDE.md`, `SummaryDocs.md` описывают состояние января 2026 года и **устарели**. Актуальные гайды: `CINEMATIC_SYSTEM_GUIDE.md` и `Assets/Scripts/Cinematic/README_CinematicSystem.txt`.

---

## 3. Технический стек

| Что | Значение |
|---|---|
| Unity | **6000.0.36f1**, URP 2D |
| Ввод | **Legacy `Input`** (`Input.GetMouseButtonDown` и т.п.). Пакет Input System установлен, но не используется |
| UI | uGUI + TextMeshPro |
| Анимации UI | **DOTween** (`Assets/Plugins/Demigiant`), хелперы в `Assets/Scripts/DoTweenExt/` |
| DI | Zenject (`Assets/Plugins/Zenject`), но **фактически декоративный**: `ProjectContextInstaller` биндит синглтоны через `FromMethod(() => X.Instance)`, а `[Inject]` есть только в примере |
| Сохранения | `JsonUtility` (Newtonsoft установлен, но для сейвов не используется) |
| Локализация | Unity Localization 1.5.9, локали ru/en, в таблице `Localization_Table_Main` **один ключ** (`hiring.temporary`). Остальной текст захардкожен на русском |
| Асинхронность | Корутины (≈250 `StartCoroutine`), без async/UniTask |
| Steam | `Assets/Scripts/Steamworks.NET/SteamManager.cs`, `Managers/BuroSteamManager.cs` |
| Unity MCP | В манифесте есть `com.gladekit.mcp-bridge` — мост MCP в редактор |
| CI | Нет. `.github/workflows/main.yml` только шлёт уведомление в Telegram |

Тесты (EditMode): `Assets/Scripts/Editor/Tests/` — `ArchetypeDatabaseTests`, `ClientArchetypeTests`, `DocumentDataTests`, `StaffScheduleTests`.

---

## 4. Структура репозитория

- **Весь игровой код** — `Assets/Scripts/` (~470 файлов, ~66k строк). **Нет asmdef и нет разделения Runtime/Editor**, всё компилируется в `Assembly-CSharp`.
- **Editor-код** раскидан по нескольким папкам: `Assets/Editor/` (≈45 тулов), `Assets/Scripts/Editor/`, `Assets/Scripts/Cinematic/Editor/`, `Assets/Scripts/DialogueSystem/Editor/`, `Assets/Scripts/DI/Editor/`.
- **Неймспейсы непоследовательны**: ≈270 файлов в глобальном (`StaffController`, `ClientStateMachine`, `SaveData`, все `StaffAction`…). Основные неймспейсы — `Managers`, `UI`, `Utilities`, `CinematicSystem(.Nodes)`, `DialogueSystem.Data`, `StorySystem`, `Gameplay`, `Enums`, `Scriptables.*`, `BuroDebug`.
- **Сцены** (в Build Settings ровно 2):
  - `Assets/Scenes/MainMenuScene.unity`
  - `Assets/Scenes/GameScene.unity`
- **Данные (ScriptableObject):**

| Где | Что лежит |
|---|---|
| `Assets/Data/` | Archetypes (36), Calendar (`MainGameCalendar.asset`), CinematicGraphs, Databases, DirectorBook, Policies, Progression (регионы), StaffActions, TemporaryNPCs |
| `Assets/Resources/` | То, что грузится через `Resources.Load`: `Databases/AIBalanceConfig`, `ActionDatabase`, `ArchetypeDatabase`, `Progression/Jobs` (12 должностей), `Policies` (20), `Dialogues`, `EndingSystem`, `CinematicGraphs`, `DirectorBook`, `ProjectContext.prefab`; **`Arcs/` пустая** |
| `Assets/Thoughts/` | **Название вводит в заблуждение** — это общий склад SO: `MainThoughtCollection`, `Achivments`, `Orders`, `Mandates`, `Ranks`, `Roledata`, `Skills`, `StaffActions`, `Upgrades`. Папка **не** под Resources |
| `Assets/Audio/` | `MainAudioLibrary.asset`, `Mixer/MainMixer.mixer`, `Voices/` |

- **Мусор** (не трогать без запроса):
  - `Assets/_Recovery/` — 47 recovery-сцен.
  - `Assets/New Folder/`.
  - Дубли ассетов: `Region_01_Slums` / `Region_Slums`, два `Job_Director`, `*_OLD.asset`, `ThoghtPanel.prefab`.

---

## 5. Архитектура и жизненный цикл

### 5.1 Два времени жизни менеджеров

**Персистентные** — префаб `Assets/Prefabs/[SYSTEMS].prefab` в `MainMenuScene`, переживает загрузки сцен:
- `SystemsBootstrapper` (`Managers/SystemBootstapper.cs` — опечатка в имени файла)
- `SaveLoadManager`, `ProgressionManager`, `MainUIManager`, `MusicPlayer`, `AudioManager`, `DialogueUIManager`, `UIGlobalSettingsManager`, `SteamManager`, `BuroSteamManager`
- Через override в сцене добавлены ещё: `TransitionManager`, `AchievementManager`, `TemporaryEffectManager`.

**Сценовые** (только в `GameScene`, умирают при выходе в меню):
- `TimeManager`, `CalendarManager`, `HiringManager`, `PlayerWallet`, `DirectorManager`, `WaveManager`
- `CinematicTriggerManager`, `EndingManager`, `OrderManager`, `UpgradeManager`, `PolicyManager`, `PhoneManager` и т.д.

### 5.2 Вход в игровую сессию — `GameSession`

`Assets/Scripts/Managers/GameSession.cs` (static) — **единая точка инициализации** при каждой загрузке `GameScene` (появилась в последнем коммите). Порядок вызовов:

1. `BindPersistentManagers` — переподписывает `ProgressionManager` и `MusicPlayer` на новый `TimeManager`.
2. `ResetGameState` — календарь, кошелёк, архив, директор, найм, прогрессия, приказы, story-флаги, триггеры синематиков.
3. `ApplySave` — загрузка слота. Для новой игры дополнительно применяется стейт из книги создания директора и вызывается `ArcManager.Initialize`.
4. `DirectorManager.PrepareDay`.

Запускается из `MainUIManager.UnveilSequence`:
пауза → `GameSession.Begin()` → сплэш дня → `CinematicTriggerManager.TriggerDayStart(day)` → (катсцена) или стол директора (`StartOfDayPanel`) + выбор приказа (`OrderSelectionUI`).

**Правило.** Новую сбрасываемую или загружаемую систему подключай именно в `GameSession` (Reset/Apply). Раскидывать инициализацию по `Start()` разных менеджеров и UI не нужно — от этого и уходили.

**Ловушка.** `TimeManager.OnDestroy` очищает свои события. Значит, персистентный менеджер, подписанный на `TimeManager.OnPeriodChanged` / `OnDayChanged`, **обязан переподписываться каждую сессию** через `GameSession.BindPersistentManagers`. Сейчас там только `ProgressionManager` и `MusicPlayer`. `TemporaryEffectManager` (персистентный, подписывается в `Awake`/`OnEnable`), вероятно, теряет подписку при второй загрузке `GameScene` за запуск.

### 5.3 Связи между системами

- **Синглтоны везде**: ~70 классов с `static Instance`, ~1900 обращений `.Instance`. Менеджеры вызывают друг друга напрямую (`X.Instance?.Method()`), event bus нет.
- **События**: обычные C# `event` / `System.Action`. Главные:
  - `TimeManager.OnPeriodChanged(PeriodSettings)`, `OnDayChanged(int)`
  - `CalendarManager.OnNewDayStarted`
  - `ProgressionManager.OnInfluenceChanged`
  - `CinematicTriggerManager.OnTriggerCompleted(id)`
  - `DialogueUIManager.OnDialogueFinished`
  - `UpgradeManager.OnUpgradePurchased`
  - `TraitManager.OnTraitsChanged`
- **Пауза**: стек `MainUIManager.PushPause()` / `PopPause()` (счётчик `_pauseCount`). Пользовательская пауза — пробел.
- Известная проблема из ТЗ: «UI управляется из менеджеров». Направление рефакторинга — UI сам смотрит в менеджеры, а менеджеры содержат только логику («ёлочка»: Менеджер → Система → Компонент → Данные).

---

## 6. Карта систем

### Время и календарь

| Класс | Что делает |
|---|---|
| `Managers/TimeManager.cs` | Главные часы: идёт по периодам `CalendarDay.periodSettings`; переход на индекс 0 = новый день → `CalendarManager.AdvanceDay()` |
| `Managers/CalendarManager.cs` | `CurrentDay` — именно он сохраняется |
| `Data/Calendar/CalendarDayPeriodType.cs` | `[Flags]`: Morning, EarlyDay, Noon, Day, LateDay, Evening, StartNight, EndNight; есть хелперы `FullDay`, `FullNight`, `IsNight`. **Это же маска смен сотрудников** |
| `Assets/Data/Calendar/MainGameCalendar.asset` | Конфиг дня: EndNight 10 с → 6 дневных периодов по 60 с → StartNight 50 с. Игровой день ≈ 7 минут |

Связанное: `LightingManager`, `TimeOfDaySpriteController`, UI-часы (`GameClockUI`, `IconClockUI`, `DayCounterUI`, `TimelineController`, `DaySplashScreenController`).

### Персонал

- **`Characters/Controllers/StaffController.cs`** (1600+ строк, глобальный неймспейс) — база всех сотрудников:
  - `enum Role`: Intern, Registrar, Cashier, Archivist, Guard, Janitor, Clerk, OfficeManager, Accountant, ServiceWorker, Director.
  - Потребности, черты (`TraitType`), `EmploymentType` (Permanent/Temporary), `AIUpdateLoop`.
- **Наследники**: `ClerkController`, `InternController`, `GuardMovement`, `ServiceWorkerController`, `OfficeManagerController`, `DirectorAvatarController` (аватар игрока).
- **Utility AI**:
  - `Data/StaffAction.cs` — абстрактный SO: `AreConditionsMet`, `CalculateUtility`, `GetExecutorType`.
  - Каждому действию соответствует `ActionExecutor` (MonoBehaviour), который добавляется через `AddComponent` при выполнении.
  - ≈60 пар действие/исполнитель лежат в `Assets/Scripts/Data/Actions/`.
  - Ассеты действий — в `Assets/Thoughts/StaffActions/**` и `Resources/Actions`.
  - Баланс — `Data/AIBalanceConfig.cs` (SO в `Resources/Databases`).
  - Отладка — окно `Bureau / Utility AI Debug` (`Assets/Editor/UtilityAIDebugWindow.cs`).
- **`Managers/HiringManager.cs`**:
  - Генерирует кандидатов только на разрешённые роли. Доступ = `startingHiringAccess` + `JobTitleData.hiringAccessRules` / `unlockedRoles` открытых должностей.
  - Найм бывает постоянный или временный (временный: оплата вперёд, уходит в конце дня).
  - `RestoreStaff` / `DestroyAllStaff` для сейвов, `RebuildControllerComponent` при смене роли.
- **Прочие менеджеры персонала**:
  - `PayStaffManager` — зарплаты, увольнение временных.
  - `AssignmentManager` — привязка к `ServicePoint`.
  - `ExperienceManager`.
  - `StaffScheduleExtensions` — смены, опоздания, больничные.
- **UI персонала**: `HiringSystemUI` (доска резюме + `SwitchToggle` «временный»), `StaffSchedulePanelUI`, `ActionConfigPopupUI` (тактика), `PromotionPanelUI`.

### Клиенты и спавн

- **`Characters/Controllers/ClientPathfinding.cs`** — главный компонент клиента.
- **`ClientStateMachine.cs`** — **рабочая** корутинная стейт-машина. Behaviour Tree (`Assets/Scripts/AI/BehaviorTree/*`) существует, но **принудительно выключен** в `ClientBehaviorManager.Awake`.
- **Архетипы**: `Characters/ClientArchetype.cs`, `Data/ArchetypeDatabase.cs`, 36 ассетов (мужские и женские варианты 15+ групп из «Таблицы архетипов» в Yonote).
- **Спавн**:
  - `Managers/WaveManager.cs` — дневной план спавна из потоков регионов (`ProgressionManager`), очередь спавна, визитёры к директору и визитёры арок.
  - `ClientSpawner` — ссылки на зоны сцены.
  - `ClientSpawnerWithArchetypes` в сценах **не используется**.
- **Очереди**: `ClientQueueManager`, `Objects/TicketTerminal.cs` (автомат номерков), `Gameplay/LimitedCapacityZone.cs`, `WaitingZone`.
- **Навигация** — свой граф вейпоинтов, без NavMesh: `Gameplay/Waypoint.cs`, `Utilities/PathfindingUtility.cs`, `Characters/Movement/AgentMover.cs`.
- **Мини-игра «клинч»** (конфликт клиента и сотрудника): `Assets/Scripts/Clinch/`.
- **Временные NPC по телефону** (клоун, клининг): `AI/TemporaryClownAI.cs`, `AI/TemporaryCleanerAI.cs`, `Managers/TemporaryEffectManager.cs`.

### Мысли над головами

- `ThoughtBubbleController.cs` (глобальный неймспейс) раз в 10–20 с берёт фразу из `ThoughtCollection` (`Assets/Thoughts/MainThoughtCollection.asset`) по ключу активности и уровню стресса или терпения. Также воспроизводит «бубнёж» через `VoiceData`.
- Префаб: `Assets/Prefabs/ThoughtBubble.prefab` — один вложенный префаб на всех 10 персонажей (старый `ThoghtPanel.prefab` не использовать).

### Экономика, директор, приказы, указы, апгрейды

| Класс | Что делает |
|---|---|
| `Managers/PlayerWallet.cs` | `AddMoney(amount, desc, IncomeType)`. Official умножается на `officialIncomeRate` (10% + бонус бухгалтера), Shadow идёт 100% |
| `FinancialLedgerManager` | Лог транзакций + коррупция |
| `Managers/DirectorManager.cs` | Репутация (HP; максимум растёт с числом регионов), страйки, коррупция, `PrepareDay`, `EvaluateEndOfDayStrikes` |
| `Managers/OrderManager.cs` + `Data/DirectorOrder.cs` | Приказы дня (выбор 1 из нескольких), мандаты |
| `Managers/PolicyManager.cs` + `PolicyData` | «Указы»/политики: `Resources/Policies` (20), `GlobalWorkSpeedMod` |
| `Managers/UpgradeManager.cs` + `UpgradeData` | Апгрейды (2 ассета в `Thoughts/Upgrades`), хуки `RegisterUpgradeHook` |
| `DirectorDocumentReviewPanel.cs` + `Utilities/BureaucraticTextGenerator.cs` | Подписание документов за столом директора |
| Документы | `DocumentManager`, `DocumentQualityManager`, `ArchiveManager`, `ArchiveRequestManager`, `Gameplay/DocumentStack.cs`, `DocumentMover.cs`, `Gameplay/Documents/ProjectDocumentObject.cs` (красные папки) |
| Беспорядок и износ | `MessManager`, `DirtGridManager`, `Gameplay/MessPoint.cs`, `Puddle`, `TrashCan`, `DurabilityManager`, `Gameplay/OfficeObjectDurability.cs` |
| Охрана | `GuardManager` («доска заявок»), `Gameplay/SecurityBarrier.cs` |

**Приказы дня.** Из эффектов приказа в коде используются только `allowedDirectorErrorRate` и `oneTimeMoneyBonus`. Множители (`staffMoveSpeedMultiplier`, `clientSpawnRateMultiplier`, `clientPatienceMultiplier`, `disableGuards`, `removeStrike`, `permanentDailyIncome` …) **нигде не читаются**.

### Прогрессия (мета-слой)

- **`Managers/ProgressionManager.cs`** (персистентный):
  - Влияние: `AddInfluence`, `TrySpendInfluence`, `OnInfluenceChanged`.
  - Открытые регионы: `RegionRuntimeState` (`daysOwned` → рост потока клиентов до `maxDailyFlow`), веса групп архетипов.
  - Дерево должностей: повышение идёт через проектный документ (`StartJobPromotion` → `ProcessCompletedDocument` → `FinalizeJobPromotion`); должность может включать указ или апгрейд.
  - Сохраняет должности, влияние и регионы.
- **Данные**: `Scriptables/Progression/JobTitleData.cs`, `RegionData.cs` (стоимость, `archetypeGroups`, `unlocksContactIDs`), `HiringAccessRule.cs`.
- **UI**: `Assets/Scripts/UI/Map/*` (`MapPanelUI`, `JobNodeUI`, `RegionSlotUI`…), `UI/InfluenceUI.cs`.
- **Editor-тулы**: `SetupCareerTree`, `DiagnoseCareerTree`, `CreateJobTitles`.
- **Конкурентов** (ведомств-соперников на карте) **в коде нет**.

### Диалоги, сюжет, синематики

- **Диалоги**:
  - `DialogueSystem/Data/*` — `DialogueGraph` SO, ноды Start / Phrase / Choice / Condition / Event / Random / End.
  - Редактор на GraphView + JSON-импорт и экспорт.
  - Рантайм — `Managers/DialogueUIManager.cs` (персистентный).
  - Флаги — `Managers/StoryStateManager.cs` (string → int, сохраняются).
- **Сюжетные арки (StoryMaker)**:
  - `StorySystem/ArcManager.cs` (выбирает 4–7 арок на прохождение, раскладывает этапы по дням, отдаёт визитёров в `WaveManager`), `ArcDefinition`, `ArcInstance`.
  - Импорт — `Assets/Editor/ArcJsonImporter*.cs`, `ImportStoryArcsHeadless`, `ValidateStoryArcsHeadless`.
  - ⚠️ **`ArcManager` нет ни в одной сцене и ни в одном префабе, а ассетов `ArcDefinition` нет вообще** (`Resources/Arcs/` пустая). Хотя `story_arcs_import_report.txt` сообщает об импорте 10 арок. Итог: арки в игре не запускаются.
- **Синематики** (`Assets/Scripts/Cinematic/`, неймспейс `CinematicSystem`):
  - `CinematicGraph` + 20 типов нод, `CinematicPlayer`.
  - **`CinematicTriggerManager`** — единый пайплайн всех катсцен: триггеры OnDayStart / OnPeriodStart; сохраняется `hasCompleted`, а не `hasTriggered`.
  - Сейчас настроено 2 триггера: `Tutorial_Day1` (день 1) и `InspectorVisit` (день 30).
  - Гайд: `CINEMATIC_SYSTEM_GUIDE.md`.
- **Туториал** теперь синематик (`Assets/Data/CinematicGraphs/Tutorial_Day1.asset`; старый `FirstDayTutorial.cs` удалён). Дополнительно:
  - `Managers/TutorialBureaucracyQuest.cs`.
  - Маскот-папка `Utilities/TutorialMascot.cs` на `[SYSTEMS_Tutorial_Canvas].prefab`; показанные подсказки хранятся в PlayerPrefs.
- **Концовки**:
  - `Managers/EndingManager.cs`: досрочное увольнение по страйкам (`dismissalStrikeLimit` = 5); на `finalDay` ждёт синематик Инспектора → доминирующая черта (`TraitManager`: Law / Empathy / Mask / Ambition) → `EndingBookUI` или `DismissalScreenUI`.
  - Данные: `Resources/EndingSystem/EndingDatabase.asset`.
- **Создание директора** (книга в стиле Sir Brante): `UI/Creation/DirectorCreationBookUI.cs`, страницы A–E (`Data/DirectorBook`) → код вида `A1B2C1D3E3` → `DirectorInitialState`.
- **Прочее**:
  - Телефон: `Managers/PhoneManager.cs` (входящие звонки-диалоги, контакты открываются регионами).
  - Телетайп (лента событий): `Managers/Teletype/TeletypeManager.Log`.
  - Ачивки: `AchievementManager` (файл `achievements.dat`, отдельно от слотов; комиксы через `ComicViewerUI`).
  - Академия: `Managers/Academy/AcademyScenarioManager.cs` — заготовка, в сцене не используется.

### Аудио

- **`Managers/AudioManager.cs`** (персистентный):
  - Пул `Audio/AudioInstance.cs` (DOTween-фейды).
  - API: `PlaySound(SoundID[, pos/transform])`, `PlayVoiceClip`, `SetVolume`.
  - Звуки задаются enum `SoundID` + `SoundLibrary` (`Assets/Audio/MainAudioLibrary.asset`).
  - Миксер: `Assets/Audio/Mixer/MainMixer.mixer`, группы и слайдеры есть.
- **Новый звук**: заводи `SoundID` + `SoundData` в библиотеке и вызывай `AudioManager.Instance.PlaySound(...)`. `AudioSource` на объекты не вешай.
- **Музыка** — `Managers/MusicPlayer.cs` (стейт-машина: меню, день, ночь, стол директора, архив, диалог, арка, концовки, клоун).
- **Звук толпы** — `CrowdSoundManager` читает `ClientSpawnerWithArchetypes.Instance`, которого нет в сцене, поэтому всегда видит 0 клиентов.
- Ключи громкости в PlayerPrefs — `GameInitialization/AudioVolumeSettings.cs`.

### UI-инфраструктура

| Класс | Назначение |
|---|---|
| `Managers/UIWindowAnimator.cs` | Показ и скрытие панелей. Панели прячутся через alpha, а не `SetActive` |
| `UI/UIButtonJuice.cs` | «Сочные» кнопки |
| `UI/SwitchToggle.cs` | iOS-тогл, префаб `Prefabs/UI/Switch Toggle.prefab` |
| `UI/LocalizedText.cs` | Обёртка TMP + `LocalizedString` |
| `UI/TooltipManager.cs` | Тултипы |
| `Managers/NotificationManager.cs` | Нотификации |
| `UI/Settings/SettingsView.cs` | Настройки (звук, экран) |
| `GameInitialization/DisplaySettingsBootstrap.cs` | Применяет настройки экрана до загрузки сцены |

Стол директора — `StartOfDayPanel.cs`, `UI/DeskInteractiveItem.cs`, `UI/DirectorControlPanelUI.cs`.

---

## 7. Сохранения

- `Managers/SaveLoadManager.cs`: 3 слота, файлы `Application.persistentDataPath/save_slot_{i}.json`, сериализация `JsonUtility`.
- `SaveGame` **стартует с существующего файла слота**, чтобы не потерять поля без владельца (`gameCompleted`, код создания).
- Формат: `Data/SaveData.cs` (+ `StaffSaveData`, `RegionSaveData`, `DocumentStackSaveData`, `DurabilitySaveData`). Словари хранятся как параллельные списки ключей и значений.

**Сохраняется:**
- День, деньги, архив, приказы.
- Весь штат (включая временных, с типом найма) и директор с полом.
- Стопки документов, story-флаги, прочность мебели, контакты телефона, состояния триггеров синематиков.
- Должности, влияние, регионы (с `daysOwned`), страйки, прогресс арок, очки черт.

**Не сохраняется (известные дыры):** купленные апгрейды (`UpgradeManager`), активные указы (`PolicyManager.activePolicyIDs`). Поле `watchedDialogues` объявлено как `HashSet` — `JsonUtility` его не сериализует, и оно нигде не используется.

**Как добавить новое поле в сейв:**
1. Добавь поле в `SaveData` с дефолтом, совместимым со старыми сейвами (старые файлы должны грузиться).
2. Заполни его в `SaveLoadManager.SaveGame`.
3. Применяй в `LoadGame`.
4. Сбрасывай в `GameSession.ResetGameState`.

Вне слотов:
- `achievements.dat` — ачивки.
- PlayerPrefs — `CompletedArcs`, `LastUsedSlot`, громкость, экран, туториал маскота.

---

## 8. Правила работы с кодом

1. **Перед любой правкой `.cs` загрузи скилл `code-writing-guide`.** Глобальные правила пользователя лежат в `~/.claude/rules/`: SOLID и KISS, early return, `_camelCase` для приватных полей, `s_` для статических, `[field: SerializeField]` для свойств, `System.Action` для событий, обработчики вида `Subject_Event`, комментарии «почему не иначе».
2. **Проект исторически не соответствует этим правилам**: глобальный неймспейс, публичные поля, нет asmdef и разделения Runtime/Editor, нет `.editorconfig`. Новый код пиши по правилам, но **массово не переименовывай и не переноси существующее без запроса**. Сохраняй обратную совместимость (сейвы, сериализованные поля в префабах и сценах — переименование поля ломает ссылки; при необходимости используй `[FormerlySerializedAs]`).
3. **DOTween** (из памяти проекта): убивать твины через `SafeKill()`, а не `?.Kill()`; привязывать твины через `SetLink(gameObject, LinkBehaviour.KillOnDestroy)`, а не убивать их в `OnDestroy`; на одну анимацию — одна `Sequence`.
4. **Не использовать git worktree.**
5. **Язык**: комментарии и логи в коде на русском. Логи с тегом `[ИмяКласса]`.
6. **Ввод**: legacy `Input`. Не смешивай с Input System, если не просили миграцию.
7. **Синглтоны**: паттерн `public static X Instance` + guard в `Awake`. Новые менеджеры: если система нужна только в игре — клади в `GameScene`, иначе в `[SYSTEMS]`. Сброс состояния и загрузку подключай в `GameSession`.
8. **Не вызывать `FindObjectsByType` в `Update`**: используй кэш (`HiringManager.AllStaff` и т.п.).
9. **Отладочный код** оборачивай в `#if UNITY_EDITOR || DEVELOPMENT_BUILD`.
10. **Editor-код** — только в папках `Editor/`.
11. **Коммиты** делать только по просьбе пользователя. Ветка разработки — `Dacha`, основная — `master`.

---

## 9. Известные ловушки и баги (проверено по коду 2026-10-04)

| Где | Проблема |
|---|---|
| `StaffController.AIUpdateLoop` | Цикл ждёт `WaitForSeconds(1f)`, но умножает скорости потребностей на `Time.deltaTime`. Потребности меняются в ~50–60 раз медленнее, чем задумано в `AIBalanceConfig` |
| `PlayerWallet.Start()` | `AddMoney(10000, "DEBUG…")` — временный хак. После него `GameSession` и загрузка перезаписывают баланс, но в лог бухгалтерии попадает запись |
| `PlayerInputController.Update` | Чит **M** (+10000 теневых денег) **не обёрнут в define** — работает в релизном билде |
| `AchievementManager` | `Resources.Load("Thoughts/Achivments")`, но `Assets/Thoughts` не под Resources. В редакторе работает через AssetDatabase, **в билде ачивки не загрузятся** |
| `GameSession` | Грузит `Resources.Load("DirectorSprites/{id}")` — такой папки нет |
| `StaffController` | Грузит `Resources.Load("Prefabs/Whater1_0")`, `"Prefabs/Trash_Object1"`, `"Sounds/scream_7"` — таких путей нет |
| `ArcManager` | Не инстанцируется нигде, ассетов арок нет → арки не работают |
| `CrowdSoundManager` | Опирается на `ClientSpawnerWithArchetypes`, которого нет в сцене |
| `TemporaryEffectManager` | Персистентный, но не переподписывается на новый `TimeManager` |
| `DirectorOrder` | Почти все эффекты не применяются (см. раздел 6) |
| `UpgradeHooksInstaller` | Не стоит в сцене. Хук дивана только пишет в лог |
| `MusicPlayer.SetMuffled` | Пустой (TODO: LowPass в миксере) |
| `AudioManager.OnIsPausedChange` | Никто не подписан |
| `ProgressionManager:565` | `// TODO: TriggerWinSequence();` — победа через должность Министра не реализована |
| `StaffController` | `CalculateArrivalTime` / `CalculateLateness` / `CalculateEarlyLeave` — заглушки |

**Пустые файлы-«надгробия»** (только комментарий): `InstructionManager`, `JobInstructionDatabase`, `Data/Policies/JobInstruction`, `UI/Policies/InstructionItemUI`, `InstructionPanelUI`, `UI/World/WorldNoticeBoard`.

**Классы, которых нет ни в одной сцене или префабе**: `ArcManager`, `ClientSpawnerWithArchetypes`, `BureaucracyManager`, `AcademyUI`, `MainMenuController`, `CinematicTrigger` (компонент), `ClientSceneTrigger`, `NotificationUI`, `FloatingText`, `TeletypeStripUI`, `UpgradeHooksInstaller`, `PirateCheckStub`, `NightPatrolRoute` и др. Прежде чем чинить «баг» в таком классе, проверь, используется ли он вообще.

---

## 10. Отладка и инструменты

**Хоткеи в Play Mode:**

| Клавиша | Действие |
|---|---|
| Пробел | Пауза |
| F1 | `ClientDebugMenu` — спавн клиентов и архетипов, арки, концовки |
| M | +10000 денег (чит) |
| B | Принудительно открыть бухгалтерию (только Editor и Dev-билд) |
| F12 | Тумблер «все ачивки» (`AchievementListUI`) + тест тултипа (`TooltipManager`) |

**Editor-окна и тулы** (`Assets/Editor/`):

| Тул | Назначение |
|---|---|
| `UtilityAIDebugWindow` | Отладка utility AI в реальном времени |
| `GameBalanceControlPanel` | Баланс игры |
| `DataResetTool` | Чистый запуск |
| `SceneAutoLoader` / `QuickGameLauncher` | Play с любой сцены |
| `SmartSceneBuilder`, `WorkstationSetupTool`, `StaffScheduleEditor` | Сборка сцены, рабочие места, расписание |
| `EndingSystemBuilder` | Сборка системы концовок |
| `ArcJsonImporterWindow` | Импорт арок |
| Графовые редакторы | Диалоги и синематики (через меню и двойной клик по ассету) |

**Проверка изменений:**
- CI нет. Компиляцию и Play Mode проверяет сам пользователь в Unity, либо агент через Unity MCP bridge, если он подключён.
- EditMode-тесты — Test Runner (`Assets/Scripts/Editor/Tests`).

---

## 11. Глоссарий (RU → код)

| Термин | В коде |
|---|---|
| Влияние | `ProgressionManager` influence |
| Район / регион | `RegionData` |
| Должность | `JobTitleData` |
| Ранг сотрудника | `RankData` |
| Указ / политика | `PolicyData`, `PolicyManager` |
| Приказ дня | `DirectorOrder`, `OrderManager` |
| Мандат | `DirectorOrder` в `currentMandates` |
| Страйк | `DirectorManager.currentStrikes` |
| Репутация / HP директора | `DirectorManager.currentReputation` |
| Красная папка / проектный документ | `ProjectDocumentObject` |
| Тактика сотрудника | `StaffAction` в `ActionConfigPopupUI` |
| Смена | `WorkShiftMask` (`CalendarDayPeriodType`) |
| Арка / StoryMaker | `ArcManager`, `ArcDefinition` |
| Синематик | `CinematicGraph` |
| Клинч | `Assets/Scripts/Clinch` |
| Телетайп | `TeletypeManager` |
| Маскот / Скрепыш | `TutorialMascot` |
| Книга директора | `DirectorCreationBookUI` |
| Валюта мира | арджент = 10 ортов (в UI пока «$» / «орты») |
