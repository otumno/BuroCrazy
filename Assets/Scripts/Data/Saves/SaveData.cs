using System.Collections.Generic;

namespace Data.Saves
{
    [System.Serializable]
    public class SaveData
    {
        // Global data
        public int day;
        public int money;
        public int archiveDocumentCount;

        // New fields for Director's Orders
        public List<string> activePermanentOrderNames;
        public List<string> completedOneTimeOrderNames;

        // Lists for storing data about individual objects
        public List<StaffData> allStaffData;
        public List<DocumentData> allDocumentStackData;
    }
}