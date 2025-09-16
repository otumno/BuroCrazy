// Файл: SystemsBootstrapper.cs
using UnityEngine;

public class SystemsBootstrapper : MonoBehaviour
{
    public static SystemsBootstrapper Instance { get; private set; }

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Централизованно находим и инициализируем все дочерние менеджеры
        SaveLoadManager.Instance = GetComponentInChildren<SaveLoadManager>();
        DirectorManager.Instance = GetComponentInChildren<DirectorManager>();
        MusicPlayer.Instance = GetComponentInChildren<MusicPlayer>();
        HiringManager.Instance = GetComponentInChildren<HiringManager>();
        ExperienceManager.Instance = GetComponentInChildren<ExperienceManager>();
        PayrollManager.Instance = GetComponentInChildren<PayrollManager>();
		MainUIManager.Instance = GetComponentInChildren<MainUIManager>();
    }
}