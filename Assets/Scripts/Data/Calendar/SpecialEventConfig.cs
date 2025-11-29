using System;
using System.Collections.Generic;
using System.Linq;

namespace Data.Calendar
{
    [Serializable]
    public class SpecialEventConfig
    {
        public List<SpecialDaySettings> SpecialDaySettings;

        public SpecialDaySettings GetSpecialEvent()
        {
            if (SpecialDaySettings == null || SpecialDaySettings.Count == 0)
                return null;

            var validSettings = SpecialDaySettings
                .Where(t => t.EventType != SpecialEventType.None && t.Weight > 0 && t.Settings != null)
                .ToList();

            if (validSettings.Count == 0)
                return null;

            var totalWeight = SpecialDaySettings.Sum(t => t.Weight);
            var randomValue = UnityEngine.Random.Range(0, totalWeight);

            var cumulative = 0f;
            foreach (var singleSettings in SpecialDaySettings)
            {
                cumulative += singleSettings.Weight;
                if (randomValue <= cumulative)
                    return singleSettings;
            }

            throw new Exception("Check errors in random selection");
        }
    }
}