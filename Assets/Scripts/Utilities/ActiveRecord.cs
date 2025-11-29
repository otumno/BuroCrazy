using System;
using Newtonsoft.Json;

namespace Plugins.Utilities
{
    public class ActiveRecord<T> : IActiveRecord, IDisposable
    {
        private T val;
        private bool isChanged;

        public event Action<T, T> OnChange;
        public bool IsChanged => isChanged;

        public T Value
        {
            get => val;
            set
            {
                if (val != null && val.Equals(value)) return;
            
                var oldVal = val;
                val = value;
                isChanged  = true;
                OnChange?.Invoke(val, oldVal);
            }
        }

        public ActiveRecord()
        {
        }

        public ActiveRecord(T val) => Value = val;

        public void Refresh() => OnChange?.Invoke(Value, Value);

        public Subscription Subscribe(Action<T, T> callback, bool invoke = false)
        {
            if (invoke)
                callback.Invoke(val, default);
            
            OnChange += callback;
            return new Subscription(() => OnChange -= callback);
        }

        public string Serialize()
        {
            isChanged = false;
            return JsonConvert.SerializeObject(val);
        }

        public void Deserialize(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                Value = default;
                return;
            }

            Value = JsonConvert.DeserializeObject<T>(value);
            isChanged = false;
        }

        public void Dispose()
        {
            OnChange = null;
        }
    }
}