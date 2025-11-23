using System.Collections.Generic;
using Scriptables;
using UnityEngine;

namespace Data.Calendar
{
    [System.Serializable]
    public class PeriodSettings
    {
        [Header("Period Type")]
        public CalendarDayPeriodType PeriodType;

        [Header("Duration")]
        public float durationInSeconds;
        
        [Header("Spawn curves")]
        public AnimationCurve clientCount = new(new Keyframe(1, 10), new Keyframe(30, 50));
        public AnimationCurve spawnRate = new(new Keyframe(1, 5));
        public AnimationCurve spawnBatchSize = new(new Keyframe(1, 1));

        [Header("Light Settings")]
        public AnimationCurve crowdSpawnCount = new(new Keyframe(1, 0));
        public AnimationCurve numberOfCrowdsToSpawn = new(new Keyframe(1, 0));

        [Header("Light Settings")]
        public Color panelColor = Color.white;
        public LightingPreset lightingSettings;
        public List<string> lightsToEnableNames;
    }
}