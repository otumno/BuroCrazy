using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BootFadeEffect : MonoBehaviour
{
    [Header("Панели для затемнения (верхняя → нижняя)")]
    [SerializeField] private List<Image> fadePanels;

    [Header("Настройки")]
    [SerializeField] private float fadeSpeed = 0.6f;

    private const string BOOT_FADE_PLAYED_KEY = "BootFadeEffect_Played";

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        if (gameObject.activeSelf)
        {
            StartCoroutine(PlayFadeSequence());
        }
    }

    private IEnumerator PlayFadeSequence()
    {
        foreach (Image panel in fadePanels)
        {
            if (panel != null)
            {
                Color startColor = panel.color;
                Color targetColor = new Color(startColor.r, startColor.g, startColor.b, 0f);

                float t = 0f;
                while (t < 1f)
                {
                    t += Time.deltaTime / fadeSpeed;
                    panel.color = Color.Lerp(startColor, targetColor, t);
                    yield return null;
                }

                panel.color = targetColor;
                panel.gameObject.SetActive(false);
            }
        }

        gameObject.SetActive(false);
    }

    [ContextMenu("Сбросить флаг запуска")]
    public void ResetBootFlag()
    {
        PlayerPrefs.DeleteKey(BOOT_FADE_PLAYED_KEY);
        Debug.Log("[BootFadeEffect] Флаг сброшен. Эффект будет при следующем запуске.");
    }
}
