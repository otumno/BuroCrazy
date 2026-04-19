using UnityEngine;

#if !DISABLESTEAMWORKS
using Steamworks;
#endif

namespace Managers
{
    public class BuroSteamManager : MonoBehaviour
    {
        [SerializeField]
        private SteamManager _steamManager;

#if !DISABLESTEAMWORKS
        private void Start()
        {
            if (!SteamManager.Initialized)
                return;

            var steamName = SteamManager.GetPersonaName();
            Debug.Log($"Your Steam Name: {steamName}");
        }
#endif
    }
}