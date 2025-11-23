using System;
using System.Collections.Generic;
using Data.Calendar;
using UnityEngine;

namespace Scriptables
{
    [Serializable]
    public class CalendarDayPeriodSettings
    {
        public CalendarDayPeriodType PeriodType;
        public float durationInSeconds = 60f;
        public float spawnRate = 5f;
        public int spawnBatchSize = 1;
        public int crowdSpawnCount = 0;
        public int numberOfCrowdsToSpawn = 0;
        public LightingPreset lightingSettings;
        public Color panelColor = new Color(1,1,1,0);
        public List<GameObject> lightsToEnable;
    }
}
