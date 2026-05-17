// === FILE: Assets/Scripts/Cinematic/Nodes/EventNode.cs ===
using System.Collections;
using UnityEngine;
using Managers;
using Characters;

namespace CinematicSystem.Nodes
{
    /// <summary>
    /// Типы событий для EventNode.
    /// </summary>
    public enum EventType
    {
        AddMoney,
        AddInfluence,
        AddStrike,
        SetFlag,
        LockControl,
        UnlockControl,
        SetCursor,
        ActivateObject,
        DeactivateObject,
        AddDocumentToHand,
        RemoveDocumentFromHand,
        PlaySound,
        PlayMusic,
        StopMusic,
        RestorePreviousMusic,
        OpenDirectorDesk,
        CloseDirectorDesk,
        ShowNotification,
        ApplyTrait,
        RemoveTrait,
        SetSpeedMultiplier,
        TriggerAllergy,
        AddReputation,
        SetReputation,
        RemoveReputation
    }

    /// <summary>
    /// Узел выполнения игрового события.
    /// Может менять флаги, деньги, влияние, управлять объектами и т.д.
    /// </summary>
    [CreateAssetMenu(menuName = "Bureau/Cinematic/Nodes/Event")]
    public class EventNode : NextNode
    {
        public EventType eventType;
        
        public int intValue;
        
        public string stringValue;
        
        public bool boolValue;
        
        [Tooltip("Ключ объекта в SceneObjectRegistry (для Activate/Deactivate)")]
        public string targetObjectKey;
        
        [Tooltip("ID персонажа (для ApplyTrait, SetSpeedMultiplier)")]
        public string characterID = "Director";
        
        // === Новые поля для аудио ===
        [Tooltip("AudioClip для проигрывания звука (PlaySound)")]
        public UnityEngine.AudioClip soundClip;
        
        [Tooltip("AudioClip для музыки (PlayMusic)")]
        public UnityEngine.AudioClip musicClip;
        
        [Tooltip("Восстановить предыдущую музыку после завершения графа")]
        public bool restorePreviousMusic;

        public override string GetNodeType() => "event";

        public override IEnumerator Execute(CinematicPlayer player)
        {
            switch (eventType)
            {
                case EventType.AddMoney:
                    PlayerWallet.Instance?.AddMoney(intValue, "Кинематик");
                    break;
                    
                case EventType.AddInfluence:
                    ProgressionManager.Instance?.AddInfluence(intValue);
                    break;
                    
                case EventType.AddStrike:
                    DirectorManager.Instance?.AddStrike();
                    break;
                    
                case EventType.SetFlag:
                    if (!string.IsNullOrEmpty(stringValue))
                        StoryStateManager.Instance?.SetFlag(stringValue, intValue);
                    break;
                    
                case EventType.LockControl:
                    var inputCtrl = UnityEngine.Object.FindObjectOfType<PlayerInputController>();
                    if (inputCtrl != null)
                        inputCtrl.IsCutscenePlaying = true;
                    break;
                    
                case EventType.UnlockControl:
                    var inputCtrl2 = UnityEngine.Object.FindObjectOfType<PlayerInputController>();
                    if (inputCtrl2 != null)
                        inputCtrl2.IsCutscenePlaying = false;
                    break;
                    
                case EventType.SetCursor:
                    var cursor = FindObjectOfType<CursorController>();
                    if (cursor != null)
                        cursor.SetCutsceneCursor(boolValue);
                    break;
                    
                case EventType.ActivateObject:
                    var go = SceneObjectRegistry.Instance.GetGameObject(targetObjectKey);
                    if (go != null) go.SetActive(true);
                    break;
                    
                case EventType.DeactivateObject:
                    var go2 = SceneObjectRegistry.Instance.GetGameObject(targetObjectKey);
                    if (go2 != null) go2.SetActive(false);
                    break;
                    
                case EventType.AddDocumentToHand:
                    var director = DirectorAvatarController.Instance;
                    var stackHolder = director?.GetComponent<StackHolder>();
                    stackHolder?.ShowSingleDocumentSprite();
                    break;
                    
                case EventType.RemoveDocumentFromHand:
                    var director2 = DirectorAvatarController.Instance;
                    var stackHolder2 = director2?.GetComponent<StackHolder>();
                    stackHolder2?.HideStack();
                    break;
                    
                case EventType.PlaySound:
                    // Если назначен AudioClip - проигрываем его напрямую
                    if (soundClip != null)
                    {
                        var audioSource = MusicPlayer.Instance?.gameObject.AddComponent<UnityEngine.AudioSource>();
                        if (audioSource != null)
                        {
                            audioSource.clip = soundClip;
                            audioSource.playOnAwake = false;
                            audioSource.Play();
                            
                            // Удаляем компонент после воспроизведения
                            MusicPlayer.Instance?.StartCoroutine(CleanupAudioSource(audioSource));
                        }
                    }
                    else if (AudioManager.Instance != null && intValue >= 0)
                    {
                        AudioManager.Instance.PlaySound((Scriptables.Audio.SoundID)intValue);
                    }
                    break;
                    
                case EventType.PlayMusic:
                    // Если назначен AudioClip - проигрываем его
                    if (musicClip != null)
                    {
                        MusicPlayer.Instance?.PlayOneShotTrack(musicClip);
                    }
                    else if (MusicPlayer.Instance != null && intValue >= 0)
                    {
                        // intValue интерпретируется как индекс трека
                        // Здесь можно добавить логику выбора трека по индексу
                    }
                    break;
                    
                case EventType.StopMusic:
                    MusicPlayer.Instance?.StopMusic();
                    break;
                    
                case EventType.RestorePreviousMusic:
                    MusicPlayer.Instance?.RestorePreviousMusic();
                    break;
                    
                case EventType.OpenDirectorDesk:
                    MainUIManager.Instance?.ShowDirectorDesk();
                    break;
                    
                case EventType.CloseDirectorDesk:
                    MainUIManager.Instance?.HideDirectorDesk();
                    break;
                    
                case EventType.ShowNotification:
                    var notif = FindObjectOfType<UI.Notifications.NotificationManager>();
                    notif?.ShowNotification(null, UI.Notifications.NotificationType.System, stringValue, 3f);
                    break;
                    
                case EventType.ApplyTrait:
                    var character = CharacterRegistry.Instance.GetCharacter(characterID) as StaffController;
                    if (character != null && System.Enum.TryParse<StaffController.TraitType>(stringValue, out var trait))
                        character.permanentTrait = trait;
                    break;
                    
                case EventType.RemoveTrait:
                    var char2 = CharacterRegistry.Instance.GetCharacter(characterID) as StaffController;
                    if (char2 != null) char2.permanentTrait = StaffController.TraitType.None;
                    break;
                    
                case EventType.SetSpeedMultiplier:
                    var char3 = CharacterRegistry.Instance.GetCharacter(characterID);
                    var mover = char3?.GetComponent<AgentMover>();
                    if (mover != null) mover.ApplySpeedMultiplier(intValue);
                    break;
                    
                case EventType.TriggerAllergy:
                    // Вызов аллергии у персонажа (для тестов)
                    break;
                    
                case EventType.AddReputation:
                    DirectorManager.Instance?.HealReputation(intValue);
                    break;
                    
                case EventType.SetReputation:
                    if (DirectorManager.Instance != null)
                    {
                        float maxRep = DirectorManager.Instance.GetMaxReputation();
                        DirectorManager.Instance.currentReputation = Mathf.Clamp(intValue, 0, (int)maxRep);
                    }
                    break;
                    
                case EventType.RemoveReputation:
                    DirectorManager.Instance?.TakeDamage(intValue, "Событие кинематик");
                    break;
            }
            
            player.GoToNextNode(nextNode);
            yield break;
        }
        
        // === Вспомогательный метод для очистки AudioSource ===
        private System.Collections.IEnumerator CleanupAudioSource(UnityEngine.AudioSource source)
        {
            if (source == null) yield break;
            
            // Ждём пока закончится воспроизведение
            while (source.isPlaying)
            {
                yield return null;
            }
            
            // Удаляем компонент
            if (source != null)
            {
                UnityEngine.Object.Destroy(source);
            }
        }
    }
}