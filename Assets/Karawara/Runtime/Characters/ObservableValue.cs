using System;
using System.Collections.Generic;
namespace RPGKarawara
{
    /// <summary>A local observable value, optionally backed directly by an authoritative model.</summary>
    public sealed class ObservableValue<T>
    {
        private T value;
        private readonly Func<T> read;
        private readonly Action<T> write;
        private readonly bool sourcePublishes;
        public event Action<T, T> OnValueChanged;
        public ObservableValue(T initialValue = default) { value = initialValue; }
        public ObservableValue(Func<T> read, Action<T> write, bool sourcePublishes = false)
        { this.read = read; this.write = write; this.sourcePublishes = sourcePublishes; }
        public T Value
        {
            get => read != null ? read() : value;
            set
            {
                T previous = Value;
                if (EqualityComparer<T>.Default.Equals(previous, value)) return;
                if (write != null) write(value); else this.value = value;
                if (!sourcePublishes) Publish(previous, Value);
            }
        }
        public void Publish(T previous, T next)
        { if (!EqualityComparer<T>.Default.Equals(previous, next)) OnValueChanged?.Invoke(previous, next); }
        // Equipment commands can intentionally reload an unchanged item ID.
        public void SetAndNotify(T next)
        {
            T previous = Value;
            if (EqualityComparer<T>.Default.Equals(previous, next)) OnValueChanged?.Invoke(previous, next);
            else Value = next;
        }
    }
}
