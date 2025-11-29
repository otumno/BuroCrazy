using System;

namespace Utilities
{
    public class Subscription : IDisposable
    {
        private readonly Action _action;
        
        public Subscription(Action action)
        {
            _action = action;
        }
        
        public void Dispose() => _action?.Invoke();
    }
}