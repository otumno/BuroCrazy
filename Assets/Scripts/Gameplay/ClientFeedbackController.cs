using UnityEngine;
using Managers;

public class ClientFeedbackController : MonoBehaviour
{
    private ClientPathfinding _client;
    private CharacterVisuals _visuals;
    private AudioSource _audioSource; // Кешируем, чтобы не создавать мусор

    public void Initialize(ClientPathfinding client)
    {
        _client = client;
        _visuals = client.GetVisuals();
        // Можно добавить AudioSource компонент, если его нет
    }

    public void PlaySound(AudioClip clip)
    {
        if (clip != null)
        {
            // Используем AudioSource.PlayClipAtPoint или менеджер, как было
            AudioSource.PlayClipAtPoint(clip, transform.position);
        }
    }

    public void UpdateStateVisuals(ClientState state, ClientPathfinding.LeaveReason leaveReason)
    {
        // 1. Обновляем уведомление (над головой)
        if (_client.notification != null) 
            _client.notification.UpdateNotification();

        // 2. Обновляем эмоцию
        if (_visuals == null) return;

        if (state == ClientState.Leaving)
        {
            switch (leaveReason)
            {
                case ClientPathfinding.LeaveReason.Processed: _visuals.SetEmotion(Emotion.Happy); break;
                case ClientPathfinding.LeaveReason.Angry: _visuals.SetEmotion(Emotion.Angry); break;
                case ClientPathfinding.LeaveReason.Theft: _visuals.SetEmotion(Emotion.Sly); break;
                default: _visuals.SetEmotion(Emotion.Sad); break;
            }
        }
        else
        {
            _visuals.SetEmotionForState(state);
        }
    }

    public void PlayStateSound(ClientState state)
    {
        // Логика звуков при смене состояния
        if (state == ClientState.Confused && _client.confusedSound != null)
            PlaySound(_client.confusedSound);
        // Сюда можно добавить другие звуки для других состояний
    }
}