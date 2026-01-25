using System.Collections.Generic;
using UnityEngine;

namespace Data.Visuals
{
    [CreateAssetMenu(fileName = "OutfitDatabase", menuName = "Bureau/Databases/Outfit Database")]
    public class OutfitDatabase : ScriptableObject
    {
        public List<OutfitData> allOutfits;

        public OutfitData GetRandomOutfit()
        {
            if (allOutfits == null || allOutfits.Count == 0)
            {
                Debug.LogWarning("[OutfitDatabase] База одежды пуста!");
                return null;
            }

            var validOutfits = new List<OutfitData>();
            foreach (var outfit in allOutfits)
            {
                if (outfit != null) validOutfits.Add(outfit);
            }

            if (validOutfits.Count == 0) return null;

            return validOutfits[Random.Range(0, validOutfits.Count)];
        }

        public OutfitData GetOutfitByID(string id)
        {
            if (allOutfits == null) return null;

            foreach (var outfit in allOutfits)
            {
                if (outfit != null && outfit.outfitID == id)
                {
                    return outfit;
                }
            }
            return null;
        }

        public List<OutfitData> GetOutfitsForGender(Gender gender)
        {
            var result = new List<OutfitData>();

            if (allOutfits == null) return result;

            foreach (var outfit in allOutfits)
            {
                // Универсальная одежда (gender == 0 в некоторых enum) или соответствует полу
                bool isUniversal = (int)outfit.gender == 0; 
                if (outfit != null && (isUniversal || outfit.gender == gender))
                {
                    result.Add(outfit);
                }
            }

            return result;
        }
    }
}
