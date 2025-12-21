using DG.Tweening;
using UnityEngine;

namespace DoTweenExt
{
    public static class DoTweenTemplates
    {
        public static Sequence AudioFadeIn(this AudioSource audioSource,
                                           float duration,
                                           float volume,
                                           Ease ease)
        {
            audioSource.volume = 0;
            var sequence = DOTween.Sequence();
            sequence
                .Append(audioSource.DOFade(volume, duration))
                .SetLink(audioSource.gameObject, LinkBehaviour.KillOnDestroy)
                .SetUpdate(UpdateType.Late, isIndependentUpdate: false)
                .SetEase(ease)
                .Play();

            return sequence;
        }

        public static Sequence AudioFadeOut(this AudioSource audioSource,
                                            float duration,
                                            Ease ease)
        {
            var sequence = DOTween.Sequence();
            sequence
                .Append(audioSource.DOFade(0, duration))
                .SetLink(audioSource.gameObject, LinkBehaviour.KillOnDestroy)
                .SetUpdate(UpdateType.Late, isIndependentUpdate: false)
                .SetEase(ease)
                .OnKill(() =>
                {
                    if (!audioSource)
                        return;
                    
                    audioSource.Stop();
                })
                .Play();
            
            return sequence;
        }
    }
}