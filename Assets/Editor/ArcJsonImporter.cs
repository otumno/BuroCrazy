// Assets/Editor/ArcJsonImporter.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Data;
using DialogueSystem.Data;
using DialogueSystem.EditorTools;
using StorySystem;

namespace StorySystem.EditorTools
{
    /// <summary>
    /// Импортирует JSON со списком сюжетных арок (ArcDefinition + DialogueGraph) в ассеты Unity.
    /// Ожидаемый формат (см. sample в Assets/Editor/Sample/story_arcs.json):
    /// [
    ///   {
    ///     "arcID": "Orwell_1984_Incident",
    ///     "displayName": "...",
    ///     "description": "...",
    ///     "durationDays": 3,
    ///     "priority": 4,
    ///     "isOneTimeOnly": true,
    ///     "defaultGoal": "DirectorAudience",
    ///     "defaultArchetype": null,
    ///     "stages": [
    ///       {
    ///         "dayOffset": 0,
    ///         "requiredFlag": null,
    ///         "onStartFlag": "Arc_Orwell_Started",
    ///         "onCompleteFlag": "Arc_Orwell_Stage0_Completed",
    ///         "forcedGoal": "DirectorAudience",
    ///         "forcedArchetype": "Official",
    ///         "isRemoteInteraction": false,
    ///         "characterName": "Винсан Шмидар",
    ///         "dialogueStructure": {
    ///           "startNodeID": "start",
    ///           "nodes": [ { "nodeID": "...", "type": "Start|Phrase|Choice|Event|Condition|End", ... } ]
    ///         }
    ///       }
    ///     ]
    ///   }
    /// ]
    /// </summary>
    public static class ArcJsonImporter
    {
        // ---------- DTO ----------

        [Serializable]
        public class StageDto
        {
            public int dayOffset;
            public string requiredFlag;
            public string onStartFlag;
            public string onCompleteFlag;
            public string forcedGoal;
            public string forcedArchetype;
            public bool isRemoteInteraction;
            public string characterName;
            /// <summary>Пол персонажа: 'Male'/'Female'/'Any'. Если пусто или отсутствует — 'Any'.</summary>
            public string characterGender;
            /// <summary>Путь к Sprite-фону для диалога. Если пусто — ставится DirectorOfficeBack.png.</summary>
            public string backgroundResource;
            public DialogueStructureDto dialogueStructure;
        }

        [Serializable]
        public class DialogueStructureDto
        {
            public string startNodeID;
            public List<DialogueNodeDto> nodes;
        }

        [Serializable]
        public class DialogueNodeDto
        {
            public string nodeID;
            public string type;
            public string dialogueType;
            public string defaultBackground; // ignored, оставлено для совместимости
            public string nextNodeID;
            public string speakerID;
            public string text;
            public string[] variantTexts;
            public string queryText;
            public List<DialogueOptionDto> options;
            public string eventType;
            public string flagKey;
            public int intValue;
            public string notificationText;
            public string conditionKey;
            public string operation;
            public int value;
            public string trueNodeID;
            public string falseNodeID;
            public string outcome;
            public string outputEmotion;
            public float stressModifier;
            // ignored fields:
            public string speakerPortrait;
        }

        [Serializable]
        public class DialogueOptionDto
        {
            public string text;
            public string nextNodeID;
            public string conditionKey;
            public string operation;
            public int conditionValue;
            public float chance;
        }

        [Serializable]
        public class ArcDto
        {
            public string arcID;
            public string displayName;
            public string description;
            public int durationDays;
            public int priority;
            public bool isOneTimeOnly;
            public string defaultGoal;
            public string defaultArchetype;
            public List<StageDto> stages;
        }

        public class ImportReport
        {
            public int arcsImported;
            public int arcsSkipped;
            public int stagesCreated;
            public int dialoguesCreated;
            public List<string> warnings = new List<string>();
            public List<string> errors = new List<string>();

            public override string ToString()
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"Арки импортированы: {arcsImported} (пропущено: {arcsSkipped})");
                sb.AppendLine($"Этапы созданы: {stagesCreated}");
                sb.AppendLine($"Диалоги созданы: {dialoguesCreated}");
                if (warnings.Count > 0)
                {
                    sb.AppendLine($"Предупреждения ({warnings.Count}):");
                    foreach (var w in warnings) sb.AppendLine("  • " + w);
                }
                if (errors.Count > 0)
                {
                    sb.AppendLine($"Ошибки ({errors.Count}):");
                    foreach (var e in errors) sb.AppendLine("  ✗ " + e);
                }
                return sb.ToString();
            }
        }

        // ---------- Public API ----------

        /// <summary>
        /// Импортировать JSON-строку. Каждая арка порождает:
        ///   Assets/<arcFolder>/<arcID>/Dialogues/<arcID>_Stage<i>.asset  (DialogueGraph)
        ///   Assets/<arcFolder>/<arcID>/<arcID>.asset                       (ArcDefinition)
        /// Если isOneTimeOnly=true и файл уже есть, он пересоздаётся с нуля.
        /// </summary>
        public static ImportReport ImportJson(string json, string arcFolder, bool overwriteExisting = true)
        {
            var report = new ImportReport();
            if (string.IsNullOrEmpty(json))
            {
                report.errors.Add("JSON пуст.");
                return report;
            }
            if (string.IsNullOrEmpty(arcFolder))
                arcFolder = "Assets/Resources/Arcs";

            ArcDto[] arcs;
            try
            {
                arcs = JsonArrayFromJson<ArcDto>(json);
            }
            catch (Exception ex)
            {
                report.errors.Add("Ошибка парсинга JSON: " + ex.Message);
                return report;
            }

            if (arcs == null || arcs.Length == 0)
            {
                report.errors.Add("JSON не содержит арок.");
                return report;
            }

            var db = LoadArchetypeDatabase();

            foreach (var arcDto in arcs)
            {
                if (arcDto == null || string.IsNullOrEmpty(arcDto.arcID))
                {
                    report.warnings.Add("Пропущена арка без arcID.");
                    report.arcsSkipped++;
                    continue;
                }

                try
                {
                    ImportSingleArc(arcDto, arcFolder, db, report, overwriteExisting);
                    report.arcsImported++;
                }
                catch (Exception ex)
                {
                    report.errors.Add($"[{arcDto.arcID}] {ex.Message}");
                    report.arcsSkipped++;
                    Debug.LogException(ex);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[ArcJsonImporter] " + report);
            return report;
        }

        public static ImportReport ImportFromFile(string jsonPath, string arcFolder, bool overwriteExisting = true)
        {
            if (!File.Exists(jsonPath))
                throw new FileNotFoundException("JSON не найден.", jsonPath);
            return ImportJson(File.ReadAllText(jsonPath), arcFolder, overwriteExisting);
        }

        /// <summary>
        /// Парсит JSON, проверяет структуру арок и формирует отчёт о потенциальных проблемах
        /// (неизвестные архетипы, эмоции, цели, пустые диалоги и т.п.) БЕЗ создания ассетов.
        /// </summary>
        public static ImportReport ValidateJson(string json)
        {
            var report = new ImportReport();
            if (string.IsNullOrEmpty(json))
            {
                report.errors.Add("JSON пуст.");
                return report;
            }

            ArcDto[] arcs;
            try
            {
                arcs = JsonArrayFromJson<ArcDto>(json);
            }
            catch (Exception ex)
            {
                report.errors.Add("Ошибка парсинга JSON: " + ex.Message);
                return report;
            }

            if (arcs == null || arcs.Length == 0)
            {
                report.errors.Add("JSON не содержит арок.");
                return report;
            }

            var db = LoadArchetypeDatabase();

            foreach (var arcDto in arcs)
            {
                if (arcDto == null || string.IsNullOrEmpty(arcDto.arcID))
                {
                    report.warnings.Add("Пропущена арка без arcID.");
                    report.arcsSkipped++;
                    continue;
                }

                // 1. Базовые проверки
                if (string.IsNullOrEmpty(arcDto.displayName))
                    report.warnings.Add($"[{arcDto.arcID}] displayName пустое.");
                if (arcDto.durationDays <= 0)
                    report.errors.Add($"[{arcDto.arcID}] durationDays должен быть > 0 (сейчас {arcDto.durationDays}).");
                if (arcDto.priority < 1 || arcDto.priority > 10)
                    report.warnings.Add($"[{arcDto.arcID}] priority {arcDto.priority} вне диапазона 1..10 → будет зажато.");

                // 2. Цель по умолчанию
                if (!string.IsNullOrEmpty(arcDto.defaultGoal) && !Enum.TryParse<ClientGoal>(arcDto.defaultGoal, true, out _))
                    report.warnings.Add($"[{arcDto.arcID}] defaultGoal '{arcDto.defaultGoal}' не распознан → используется DirectorAudience.");

                // 3. Default archetype
                if (!string.IsNullOrEmpty(arcDto.defaultArchetype))
                    ResolveArchetype(db, arcDto.defaultArchetype, arcDto.arcID, "defaultArchetype", report);

                // 4. Этапы
                if (arcDto.stages == null || arcDto.stages.Count == 0)
                {
                    report.warnings.Add($"[{arcDto.arcID}] нет этапов.");
                    continue;
                }

                HashSet<string> stageFlagPrefixes = new HashSet<string>();
                foreach (var stage in arcDto.stages)
                {
                    if (stage == null) continue;

                    if (!string.IsNullOrEmpty(stage.forcedGoal) && !Enum.TryParse<ClientGoal>(stage.forcedGoal, true, out _))
                        report.warnings.Add($"[{arcDto.arcID}] stage dayOffset={stage.dayOffset}: forcedGoal '{stage.forcedGoal}' не распознан.");

                    if (!string.IsNullOrEmpty(stage.forcedArchetype))
                        ResolveArchetype(db, stage.forcedArchetype, $"{arcDto.arcID}/stage_{stage.dayOffset}", "forcedArchetype", report);

                    if (stage.dialogueStructure == null || stage.dialogueStructure.nodes == null || stage.dialogueStructure.nodes.Count == 0)
                    {
                        report.warnings.Add($"[{arcDto.arcID}] stage dayOffset={stage.dayOffset}: dialogueStructure пуст.");
                        continue;
                    }

                    // Проверяем, что startNodeID указывает на существующую ноду и что нод Start ровно одна.
                    var nodeIds = new HashSet<string>();
                    int startCount = 0;
                    bool startNodeIdFound = false;
                    foreach (var n in stage.dialogueStructure.nodes)
                    {
                        if (n == null) continue;
                        if (string.IsNullOrEmpty(n.nodeID))
                        {
                            report.warnings.Add($"[{arcDto.arcID}] stage dayOffset={stage.dayOffset}: нода без nodeID.");
                            continue;
                        }
                        if (!nodeIds.Add(n.nodeID))
                            report.warnings.Add($"[{arcDto.arcID}] stage dayOffset={stage.dayOffset}: дублирующийся nodeID '{n.nodeID}'.");

                        if (string.Equals(n.type, "Start", StringComparison.OrdinalIgnoreCase)) startCount++;

                        if (!string.IsNullOrEmpty(stage.dialogueStructure.startNodeID) &&
                            n.nodeID == stage.dialogueStructure.startNodeID)
                            startNodeIdFound = true;

                        // Emotion
                        if (!string.IsNullOrEmpty(n.outputEmotion) && !Enum.TryParse<Emotion>(n.outputEmotion, true, out _))
                            report.warnings.Add($"[{arcDto.arcID}] stage dayOffset={stage.dayOffset} '{n.nodeID}': emotion '{n.outputEmotion}' не найден.");

                        // Outcome
                        if (!string.IsNullOrEmpty(n.outcome) && !Enum.TryParse<EndNode.DialogueOutcome>(n.outcome, true, out _))
                            report.warnings.Add($"[{arcDto.arcID}] stage dayOffset={stage.dayOffset} '{n.nodeID}': outcome '{n.outcome}' не распознан.");

                        // EventType
                        if (!string.IsNullOrEmpty(n.eventType) && !Enum.TryParse<EventNode.EventType>(n.eventType, true, out _))
                            report.warnings.Add($"[{arcDto.arcID}] stage dayOffset={stage.dayOffset} '{n.nodeID}': eventType '{n.eventType}' не распознан.");
                    }

                    if (!string.IsNullOrEmpty(stage.dialogueStructure.startNodeID) && !startNodeIdFound)
                        report.errors.Add($"[{arcDto.arcID}] stage dayOffset={stage.dayOffset}: startNodeID '{stage.dialogueStructure.startNodeID}' не найден среди нод.");

                    if (startCount > 1)
                        report.warnings.Add($"[{arcDto.arcID}] stage dayOffset={stage.dayOffset}: найдено {startCount} Start-нод (обычно ожидается 1).");
                    else if (startCount == 0)
                        report.warnings.Add($"[{arcDto.arcID}] stage dayOffset={stage.dayOffset}: нет Start-ноды.");
                }

                report.arcsImported++;
            }

            return report;
        }

        // ---------- internals ----------

        private static void ImportSingleArc(ArcDto arcDto, string arcFolder, ArchetypeDatabase db, ImportReport report, bool overwriteExisting)
        {
            // 1. Создаём папку арки и подпапку диалогов
            string arcAssetFolder = $"{arcFolder.TrimEnd('/')}/{arcDto.arcID}";
            string dialoguesFolder = $"{arcAssetFolder}/Dialogues";
            EnsureFolder(arcAssetFolder);
            EnsureFolder(dialoguesFolder);

            // 2. Создаём ArcDefinition asset (или пересоздаём)
            string arcAssetPath = $"{arcAssetFolder}/{arcDto.arcID}.asset";
            ArcDefinition arcAsset = AssetDatabase.LoadAssetAtPath<ArcDefinition>(arcAssetPath);
            if (arcAsset != null && overwriteExisting)
            {
                // Проверим, нет ли в проекте мест, ссылающихся на этот ArcDefinition
                // или на любой из его DialogueGraph subassets. Поиск через все
                // зависимости в `Assets/` занимает время, но мы делаем это один
                // раз на старте, поэтому допустимо.
                string oldGuid = AssetDatabase.AssetPathToGUID(arcAssetPath);
                if (!string.IsNullOrEmpty(oldGuid))
                {
                    var referencing = FindAssetsReferencingGuid(oldGuid);
                    if (referencing.Count > 0)
                    {
                        report.warnings.Add(
                            $"[{arcDto.arcID}] существующий ArcDefinition используется в {referencing.Count} " +
                            $"местах проекта; ссылки на старый GUID станут невалидными после перезаписи: " +
                            string.Join(", ", referencing.Take(5)));
                    }
                }

                // DeleteAsset также удалит все subassets (DialogueGraph и DialogueNode'ы).
                AssetDatabase.DeleteAsset(arcAssetPath);
                arcAsset = null;
            }
            if (arcAsset == null)
            {
                arcAsset = ScriptableObject.CreateInstance<ArcDefinition>();
                AssetDatabase.CreateAsset(arcAsset, arcAssetPath);
            }

            // 3. Заполняем статические поля
            arcAsset.arcID = arcDto.arcID;
            arcAsset.displayName = string.IsNullOrEmpty(arcDto.displayName) ? arcDto.arcID : arcDto.displayName;
            arcAsset.description = arcDto.description ?? "";
            arcAsset.durationDays = Mathf.Max(1, arcDto.durationDays > 0 ? arcDto.durationDays : 1);
            arcAsset.priority = Mathf.Clamp(arcDto.priority, 1, 10);
            arcAsset.isOneTimeOnly = arcDto.isOneTimeOnly;

            // Default goal
            if (Enum.TryParse<ClientGoal>(arcDto.defaultGoal, true, out var defGoal))
                arcAsset.defaultGoal = defGoal;
            else
                report.warnings.Add($"[{arcDto.arcID}] defaultGoal '{arcDto.defaultGoal}' не распознан → используется DirectorAudience.");

            // Default archetype (по строке ID)
            arcAsset.defaultArchetype = ResolveArchetype(db, arcDto.defaultArchetype, arcDto.arcID, "defaultArchetype", report);

            // !!! ВАЖНО: помечаем ArcDefinition как dirty и сохраняем ДО того, как
            // DialogueVerboseImporter начнёт добавлять subassets. Иначе Unity при
            // добавлении subasset-а (через AssetDatabase.AddObjectToAsset) запускает
            // неявный SaveAssets, который сериализует ArcDefinition до того, как
            // наши изменения arcID/displayName/description дойдут до диска — и
            // в итоге в .asset-файле окажутся пустые строки.
            EditorUtility.SetDirty(arcAsset);
            AssetDatabase.SaveAssetIfDirty(arcAsset);

            // 4. Создаём этапы
            arcAsset.stages = new List<StageDefinition>();
            if (arcDto.stages != null)
            {
                for (int i = 0; i < arcDto.stages.Count; i++)
                {
                    var stageDto = arcDto.stages[i];
                    if (stageDto == null) continue;

                    var stage = new StageDefinition
                    {
                        dayOffset = stageDto.dayOffset,
                        requiredFlag = stageDto.requiredFlag ?? "",
                        onStartFlag = stageDto.onStartFlag ?? "",
                        onCompleteFlag = stageDto.onCompleteFlag ?? "",
                        isRemoteInteraction = stageDto.isRemoteInteraction,
                        characterName = stageDto.characterName ?? "",
                        characterGender = string.IsNullOrEmpty(stageDto.characterGender) ? "Any" : stageDto.characterGender,
                        backgroundResource = stageDto.backgroundResource ?? "",
                        useForcedGoal = !string.IsNullOrEmpty(stageDto.forcedGoal)
                    };

                    if (stage.useForcedGoal && Enum.TryParse<ClientGoal>(stageDto.forcedGoal, true, out var stageGoal))
                        stage.forcedGoal = stageGoal;

                    stage.forcedArchetype = ResolveArchetype(db, stageDto.forcedArchetype,
                        $"{arcDto.arcID}/stage_{i}", "forcedArchetype", report);

                    // 5. Диалог
                    if (stageDto.dialogueStructure == null || stageDto.dialogueStructure.nodes == null || stageDto.dialogueStructure.nodes.Count == 0)
                    {
                        report.warnings.Add($"[{arcDto.arcID}] stage {i}: dialogueStructure пуст, этап создан БЕЗ диалога.");
                    }
                    else
                    {
                        string dialoguePath = $"{dialoguesFolder}/{arcDto.arcID}_Stage{i}.asset";
                        // [ИСПРАВЛЕНО] Фон диалога: если в JSON не указан — будет использован
                        // DirectorOfficeBack.png как постоянный дефолт для всех арок.
                        string bgResource = ResolveBackgroundResource(stageDto.backgroundResource, $"{arcDto.arcID}/stage_{i}", report);
                        try
                        {
                            stage.dialogue = BuildDialogue(stageDto.dialogueStructure, dialoguePath, arcDto.arcID, i, bgResource, report);
                            if (stage.dialogue != null) report.dialoguesCreated++;
                        }
                        catch (Exception ex)
                        {
                            report.errors.Add($"[{arcDto.arcID}] stage {i}: ошибка сборки диалога: {ex.Message}");
                            Debug.LogException(ex);
                        }
                    }

                    arcAsset.stages.Add(stage);
                    report.stagesCreated++;
                }
            }

            EditorUtility.SetDirty(arcAsset);
        }

        /// <summary>
        /// Собирает DialogueGraph из DTO (использует уже существующий DialogueVerboseImporter
        /// для материализации узлов). Корневая нода выбирается по startNodeID.
        /// </summary>
        private static DialogueGraph BuildDialogue(DialogueStructureDto dto, string assetPath, string arcID, int stageIndex, string backgroundResource, ImportReport report)
        {
            // Преобразуем DTO в формат, который понимает DialogueVerboseImporter.
            // Его NodeDto использует другие имена полей, поэтому сериализуем промежуточный JSON.
            string intermediate = SerializeForVerboseImporter(dto, arcID, stageIndex, backgroundResource, report);
            return DialogueVerboseImporter.Import(intermediate, assetPath);
        }

        private static string SerializeForVerboseImporter(DialogueStructureDto dto, string arcID, int stageIndex, string backgroundResource, ImportReport report)
        {
            // DTO verbose-импортёра — он ожидает поля ровно как DialogueVerboseImporter.NodeDto.
            // Чтобы не дублировать DTO, собираем JsonUtility-сериализуемую структуру через рефлексию.
            var verboseGraph = NewGraphDto(arcID, dto.startNodeID, backgroundResource);
            var verboseNodes = new List<DialogueVerboseImporter.NodeDto>();

            foreach (var n in dto.nodes)
            {
                if (n == null || string.IsNullOrEmpty(n.nodeID)) continue;

                var vn = NewNodeDto();
                vn.nodeID = n.nodeID;
                vn.type = NormalizeType(n.type);
                vn.dialogueType = n.dialogueType;
                vn.nextNodeID = n.nextNodeID;
                vn.speakerID = n.speakerID;
                vn.text = n.text;
                if (n.variantTexts != null && n.variantTexts.Length > 0)
                    vn.variantTexts = n.variantTexts;
                vn.queryText = n.queryText;
                vn.eventType = n.eventType;
                vn.flagKey = n.flagKey;
                vn.intValue = n.intValue;
                vn.notificationText = n.notificationText;
                vn.conditionKey = n.conditionKey;
                vn.operation = n.operation;
                vn.conditionValue = n.value;
                vn.trueNodeID = n.trueNodeID;
                vn.falseNodeID = n.falseNodeID;
                vn.outcome = n.outcome;
                vn.outputEmotion = ResolveEmotion(n.outputEmotion, arcID, stageIndex, n.nodeID, report);
                vn.stressModifier = n.stressModifier;

                if (n.options != null && n.options.Count > 0)
                {
                    vn.options = new List<DialogueVerboseImporter.OptionDto>();
                    foreach (var opt in n.options)
                    {
                        if (opt == null) continue;
                        vn.options.Add(new DialogueVerboseImporter.OptionDto
                        {
                            text = opt.text,
                            nextNodeID = opt.nextNodeID,
                            conditionKey = opt.conditionKey,
                            operation = opt.operation,
                            conditionValue = opt.conditionValue,
                            chance = opt.chance
                        });
                    }
                }

                verboseNodes.Add(vn);
            }

            verboseGraph.nodes = verboseNodes;

            return JsonUtility.ToJson(verboseGraph, true);
        }

        private static DialogueVerboseImporter.GraphDto NewGraphDto(string name, string root, string defaultBackgroundResource = null)
        {
            return new DialogueVerboseImporter.GraphDto
            {
                name = name,
                root = root,
                nodes = new List<DialogueVerboseImporter.NodeDto>(),
                defaultBackgroundResource = defaultBackgroundResource
            };
        }

        private static DialogueVerboseImporter.NodeDto NewNodeDto() => new DialogueVerboseImporter.NodeDto();

        private static string NormalizeType(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            switch (raw.Trim().ToLowerInvariant())
            {
                case "start": return "start";
                case "phrase": return "phrase";
                case "choice": return "choice";
                case "event": return "event";
                case "condition": return "condition";
                case "random": return "random";
                case "end": return "end";
                default: return raw;
            }
        }

        private static string ResolveEmotion(string raw, string arcID, int stageIndex, string nodeID, ImportReport report)
        {
            if (string.IsNullOrEmpty(raw)) return "Neutral";
            if (Enum.TryParse<Emotion>(raw, true, out var em)) return em.ToString();
            report.warnings.Add($"[{arcID}] stage {stageIndex} '{nodeID}': emotion '{raw}' не найден → Neutral.");
            return "Neutral";
        }

        /// <summary>
        /// Определяет asset path к фону диалога:
        /// 1. Если в JSON указано backgroundResource — пробует загрузить Sprite по этому пути.
        /// 2. Иначе (или если не нашли) — возвращает путь к 'Assets/Sprites/Backs/DirectorOfficeBack.png'.
        /// Если и fallback не загружается — возвращает null (DialogueVerboseImporter просто ничего не применит).
        /// </summary>
        private const string DefaultBackgroundResource = "Assets/Sprites/Backs/DirectorOfficeBack.png";

        private static string ResolveBackgroundResource(string jsonValue, string context, ImportReport report)
        {
            if (!string.IsNullOrEmpty(jsonValue))
            {
                var sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(jsonValue);
                if (sprite != null) return jsonValue;
                report.warnings.Add($"[{context}] backgroundResource='{jsonValue}': Sprite не найден — используется {DefaultBackgroundResource}.");
            }
            // Всегда возвращаем fallback-путь — даже если ассета нет. DialogueVerboseImporter
            // проверит загрузку ещё раз и тихо пропустит, если ассета нет.
            return DefaultBackgroundResource;
        }

        private static Characters.ClientArchetype ResolveArchetype(ArchetypeDatabase db, string archetypeID, string context, string fieldName, ImportReport report)
        {
            if (string.IsNullOrEmpty(archetypeID)) return null;
            if (db == null)
            {
                report.warnings.Add($"[{context}] {fieldName}='{archetypeID}': ArchetypeDatabase не найден — оставлено null.");
                return null;
            }

            var found = db.GetArchetypeByID(archetypeID);
            if (found != null) return found;

            // Фоллбэк: попробуем найти ассет в проекте по archetypeID (имена вроде "Official").
            var guids = AssetDatabase.FindAssets($"t:{nameof(Characters.ClientArchetype)} {archetypeID}");
            foreach (var g in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                var asset = AssetDatabase.LoadAssetAtPath<Characters.ClientArchetype>(path);
                if (asset != null && string.Equals(asset.archetypeID, archetypeID, StringComparison.OrdinalIgnoreCase))
                    return asset;
                if (asset != null && asset.name.IndexOf(archetypeID, StringComparison.OrdinalIgnoreCase) >= 0)
                    return asset;
            }

                report.warnings.Add($"[{context}] {fieldName}='{archetypeID}': архетип не найден — оставлено null.");
            return null;
        }

        /// <summary>
        /// Возвращает список путей ассетов в проекте, которые (прямо или через subassets)
        /// ссылаются на ассет с заданным GUID. Используется при реимпорте арки, чтобы
        /// предупредить, что ссылки на старый .asset станут невалидными.
        /// </summary>
        private static List<string> FindAssetsReferencingGuid(string guid)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(guid)) return result;
            string needle = "guid: " + guid;
            try
            {
                foreach (var path in AssetDatabase.GetAllAssetPaths())
                {
                    if (path.EndsWith(".cs", System.StringComparison.OrdinalIgnoreCase)) continue;
                    string contents = null;
                    try
                    {
                        contents = System.IO.File.ReadAllText(path);
                    }
                    catch
                    {
                        continue;
                    }
                    if (!string.IsNullOrEmpty(contents) && contents.Contains(needle))
                        result.Add(path);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ArcJsonImporter] Не удалось просканировать ссылки: {ex.Message}");
            }
            return result;
        }

        private static ArchetypeDatabase LoadArchetypeDatabase()
        {
            var db = Resources.Load<ArchetypeDatabase>("Databases/ArchetypeDatabase");
            if (db != null) return db;
            // Поиск по всему проекту на случай, если путь изменился.
            var guids = AssetDatabase.FindAssets($"t:{nameof(ArchetypeDatabase)}");
            foreach (var g in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                var asset = AssetDatabase.LoadAssetAtPath<ArchetypeDatabase>(path);
                if (asset != null) return asset;
            }
            return null;
        }

        private static void EnsureFolder(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return;
            string dir = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(dir) || AssetDatabase.IsValidFolder(dir)) return;
            string[] parts = dir.Split('/');
            string accum = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = accum + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(accum, parts[i]);
                accum = next;
            }
        }

        // JsonUtility не умеет десериализовать массив в корне — оборачиваем и разворачиваем вручную.
        private static T[] JsonArrayFromJson<T>(string json)
        {
            string trimmed = json.TrimStart();
            if (trimmed.StartsWith("["))
            {
                string wrapped = "{\"items\":" + json + "}";
                var wrapper = JsonUtility.FromJson<ArrayWrapper<T>>(wrapped);
                return wrapper.items ?? Array.Empty<T>();
            }
            // Если JSON — одиночный объект, оборачиваем в массив.
            var single = JsonUtility.FromJson<T>(json);
            return single == null ? Array.Empty<T>() : new[] { single };
        }

        [Serializable]
        private class ArrayWrapper<T> { public T[] items; }
    }
}
