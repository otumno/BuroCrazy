// Assets/Scripts/Scriptables/Audio/SoundID.cs
namespace Scriptables.Audio
{
    public enum SoundID
    {
        None,
    
        // UI
        UI_Click_Default,
        UI_Hover,
        UI_Popup_Open,
        UI_Money_Income,
        UI_Stamp_Approve,
        UI_Stamp_Reject,
        UI_Pencil_Hatch,
    
        // Music
        Music_Menu,
        Music_Gameplay_Day,
        Music_Gameplay_Night,
    
        // World
        Door_Open,
        Door_Close,
        Footstep_Carpet,
        Footstep_Tile,
        Client_Angry,
        Client_Happy,
		Voice_Secretary,
    
        // Special
        Time_Period_Change,

        // Lighting (Новые)
        Light_MasterSwitch,  // Общий щелчок рубильника (один раз)
        Light_LampTwinkle    // Звук разгорания/потухания отдельной лампы
    }
}