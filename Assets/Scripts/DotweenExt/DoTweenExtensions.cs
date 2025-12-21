using DG.Tweening;

namespace DoTweenExt
{
    public static class DoTweenExtensions
    {
        public static void SafeKill(this Tween tween)
        {
            if (tween.IsActive()) tween.Kill();
        }
    }
}