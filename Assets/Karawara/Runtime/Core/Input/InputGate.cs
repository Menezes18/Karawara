using System;
namespace Karawara.Core.Input
{
    /// <summary>Each caller owns one lease; releasing it cannot release another caller.</summary>
    public sealed class InputGate
    {
        private int locks;
        private readonly System.Collections.Generic.Dictionary<InputBlockReason, int> reasons = new System.Collections.Generic.Dictionary<InputBlockReason, int>();
        public bool IsBlocked => locks != 0;
        public bool IsBlockedExcept(InputBlockReason allowedReason)
        {
            foreach (var reason in reasons) if (reason.Key != allowedReason && reason.Value != 0) return true;
            return false;
        }
        public event Action Changed;
        public IDisposable Acquire(InputBlockReason reason)
        {
            if (!Enum.IsDefined(typeof(InputBlockReason), reason)) throw new ArgumentOutOfRangeException(nameof(reason));
            var token = new Token(this, reason);
            reasons.TryGetValue(reason, out int count); reasons[reason] = count + 1;
            locks++;
            if (locks == 1) Changed?.Invoke();
            return token;
        }
        private sealed class Token : IDisposable
        {
            private InputGate owner;
            private readonly InputBlockReason reason;
            internal Token(InputGate owner, InputBlockReason reason) { this.owner = owner; this.reason = reason; }
            public void Dispose()
            {
                var gate = owner; if (gate == null) return;
                owner = null;
                if (--gate.reasons[reason] == 0) gate.reasons.Remove(reason);
                if (--gate.locks == 0) gate.Changed?.Invoke();
            }
        }
    }
}
