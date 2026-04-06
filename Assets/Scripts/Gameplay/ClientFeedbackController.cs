using UnityEngine;
using Managers;
using Characters;

public class ClientFeedbackController : MonoBehaviour
{
    private ClientPathfinding _client;
    private CharacterVisuals _visuals;
    private AudioSource _audioSource;

    private float _lastEmotionChangeTime = -1f;
    private const float EMOTION_COOLDOWN = 0.2f;
    private Emotion _lastEmotion;

    private float _lastStateSoundTime = -1f;
    private const float STATE_SOUND_COOLDOWN = 1f;

    public void Initialize(ClientPathfinding client)
    {
        _client = client;
        _visuals = client.GetVisuals();
    }

    public void PlaySound(AudioClip clip)
    {
        if (clip != null)
        {
            if (Managers.AudioManager.Instance != null)
            {
                Managers.AudioManager.Instance.PlayAudioClip2D(clip);
            }
            else
            {
                AudioSource.PlayClipAtPoint(clip, transform.position); // Фолбэк
            }
        }
    }

    public void UpdateStateVisuals(ClientState state, ClientPathfinding.LeaveReason leaveReason)
    {
        if (_visuals == null) return;

        // Cooldown для предотвращения мерцания эмоций
        if (Time.time - _lastEmotionChangeTime < EMOTION_COOLDOWN)
        {
            // Проверяем, та же ли эмоция - если да, пропускаем
            if (_lastEmotion == GetEmotionForState(state, leaveReason))
            {
                // Всё равно обновляем уведомление
                if (_client.notification != null)
                    _client.notification.UpdateNotification();
                return;
            }
        }

        _lastEmotionChangeTime = Time.time;

        // 1. Обновляем уведомление (над головой)
        if (_client.notification != null)
            _client.notification.UpdateNotification();

        // 2. Обновляем эмоцию
        Emotion emotion = GetEmotionForState(state, leaveReason);
        _lastEmotion = emotion;
        _visuals.SetEmotion(emotion);
    }

    private Emotion GetEmotionForState(ClientState state, ClientPathfinding.LeaveReason leaveReason)
    {
        if (state == ClientState.Leaving)
        {
            switch (leaveReason)
            {
                case ClientPathfinding.LeaveReason.Processed: return Emotion.Happy;
                case ClientPathfinding.LeaveReason.Angry: return Emotion.Angry;
                case ClientPathfinding.LeaveReason.Theft: return Emotion.Sly;
                default: return Emotion.Sad;
            }
        }

        // Queue jumper (Наглец) should show Sly emotion while moving or waiting
        if (_client.isQueueJumper && (state == ClientState.MovingToGoal || state == ClientState.AtWaitingArea || state == ClientState.SittingInWaitingArea))
        {
            return Emotion.Sly;
        }

        switch (state)
        {
            case ClientState.Confused: return Emotion.Confused;
            case ClientState.Grumbling: return Emotion.Irritated;
            case ClientState.Enraged: return Emotion.Angry;
            case ClientState.SittingInWaitingArea:
            case ClientState.AtWaitingArea: return Emotion.Neutral;
            case ClientState.AtRegistration:
            case ClientState.AtDesk1:
            case ClientState.AtDesk2: return Emotion.Thinking;
            case ClientState.AtCashier: return Emotion.Neutral;
            case ClientState.AtToilet: return Emotion.Scared;
            case ClientState.MovingToGoal: return Emotion.Neutral;
            default: return Emotion.Neutral;
        }
    }

    public void PlayStateSound(ClientState state)
    {
        if (Time.time - _lastStateSoundTime < STATE_SOUND_COOLDOWN)
            return;

        _lastStateSoundTime = Time.time;

        if (state == ClientState.Confused && _client.confusedSound != null)
            PlaySound(_client.confusedSound);
    }
}