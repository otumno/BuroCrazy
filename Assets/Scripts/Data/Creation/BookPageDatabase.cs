using System.Collections.Generic;
using UnityEngine;

namespace Data.Creation
{
    [CreateAssetMenu(fileName = "BookPageDatabase", menuName = "Bureau/Databases/Book Page Database")]
    public class BookPageDatabase : ScriptableObject
    {
        public static BookPageDatabase Instance { get; private set; }

        public List<BookPageData> allPages;

        private void OnEnable()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Debug.LogWarning("[BookPageDatabase] Multiple instances detected!");
            }
        }

        private void OnDisable()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
