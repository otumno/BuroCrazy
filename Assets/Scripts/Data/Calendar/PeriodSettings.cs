using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class PeriodSettings
{
    public string periodName;

    // --- ВАЖНОЕ ИЗМЕНЕНИЕ: Заменяем числа на кривые ---
    // Ключ (время) на кривой - это день (от 1 до 30).
    // Значение кривой - это значение параметра в этот день.
    public AnimationCurve clientCount = new AnimationCurve(new Keyframe(1, 10), new Keyframe(30, 50));
    public AnimationCurve durationInSeconds = new AnimationCurve(new Keyframe(1, 60));
    
    // <<< ДОБАВЛЕННЫЕ ПОЛЯ >>>
    public AnimationCurve spawnRate = new AnimationCurve(new Keyframe(1, 5));
    public AnimationCurve spawnBatchSize = new AnimationCurve(new Keyframe(1, 1));
    // <<< КОНЕЦ ДОБАВЛЕНИЙ >>>

    public AnimationCurve crowdSpawnCount = new AnimationCurve(new Keyframe(1, 0));
    public AnimationCurve numberOfCrowdsToSpawn = new AnimationCurve(new Keyframe(1, 0));

    // Настройки света и цвета остаются простыми, так как они обычно не меняются каждый день
    public LightingPreset lightingSettings;
    public Color panelColor = Color.white;
    public List<string> lightsToEnableNames;
}