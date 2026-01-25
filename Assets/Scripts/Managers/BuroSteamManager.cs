using Steamworks;
using UnityEngine;

namespace Managers
{
    public class BuroSteamManager : MonoBehaviour
    {
        [SerializeField]
        private SteamManager _steamManager;

        private void Start()
        {
            if (!SteamManager.Initialized)
                return;

            var steamName = SteamFriends.GetPersonaName();
            Debug.Log($"Your Steam Name: {steamName}");
        }
    }
}