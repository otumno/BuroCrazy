using UnityEngine;

public class NotificationStyleManager : MonoBehaviour
{
    // Эта статическая переменная будет "глобальной" для всех скриптов нотификаций.
    public static bool useEmojiStyle = true;

    [Header("Настройка стиля в Инспекторе")]
    [Tooltip("Поставьте галочку, чтобы использовать иконки (emoji). Уберите, чтобы использовать старые текстовые символы.")]
    public bool inspectorUseEmojiStyle = true;

    void Update()
    {
        // Этот код постоянно синхронизирует настройку из инспектора с глобальной переменной.
        // Это позволяет вам менять стиль "на лету" прямо во время запущенной игры.
        if (useEmojiStyle != inspectorUseEmojiStyle)
        {
            useEmojiStyle = inspectorUseEmojiStyle;
        }
    }
}