using System.Collections.Generic;
using UnityEngine;

namespace Data.Visuals
{
    [CreateAssetMenu(fileName = "HairDatabase", menuName = "Bureau/Databases/Hair Database")]
    public class HairStyleDatabase : ScriptableObject
    {
        public List<HairStyleData> allHairStyles;

        public HairStyleData GetRandomHairStyle()
        {
            if (allHairStyles == null || allHairStyles.Count == 0)
            {
                Debug.LogWarning("[HairDatabase] База причесок пуста!");
                return null;
            }

            var validHair = new List<HairStyleData>();
            foreach (var hair in allHairStyles)
            {
                if (hair != null) validHair.Add(hair);
            }

            if (validHair.Count == 0) return null;

            return validHair[Random.Range(0, validHair.Count)];
        }

        public HairStyleData GetHairStyleByID(string id)
        {
            if (allHairStyles == null) return null;

            foreach (var hair in allHairStyles)
            {
                if (hair != null && hair.hairID == id)
                {
                    return hair;
                }
            }
            return null;
        }
    }
}
