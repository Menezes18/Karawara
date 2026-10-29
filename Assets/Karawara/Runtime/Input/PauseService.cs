using System;
using System.Collections.Generic;
using Karawara.Core.Input;
using UnityEngine;
namespace RPGKarawara
{
    /// <summary>Session-owned clock; pause panels and cutscenes own independent leases.</summary>
    public sealed class PauseService : IDisposable
    {
        private readonly InputGate gate;
        private readonly HashSet<Lease> leases = new HashSet<Lease>();
        private IDisposable panelPause;
        private float previousScale;
        private bool disposed;
        public PauseService(InputGate gate) { this.gate = gate ?? throw new ArgumentNullException(nameof(gate)); }
        public void SetPaused(bool paused)
        {
            if (paused) { if (panelPause == null) panelPause = Acquire(InputBlockReason.Pause); }
            else { panelPause?.Dispose(); panelPause = null; }
        }
        public IDisposable Acquire(InputBlockReason reason)
        {
            if (disposed) throw new ObjectDisposedException(nameof(PauseService));
            if (leases.Count == 0) previousScale = Time.timeScale;
            var lease = new Lease(this, gate.Acquire(reason));
            leases.Add(lease); Time.timeScale = 0;
            return lease;
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (var lease in new List<Lease>(leases)) lease.Dispose();
            panelPause = null;
        }
        private sealed class Lease : IDisposable
        {
            private PauseService owner; private IDisposable input;
            public Lease(PauseService owner, IDisposable input) { this.owner = owner; this.input = input; }
            public void Dispose()
            {
                var service = owner; if (service == null) return;
                owner = null;
                service.leases.Remove(this);
                if (service.leases.Count == 0) Time.timeScale = service.previousScale;
                input.Dispose(); input = null;
            }
        }
    }
}
