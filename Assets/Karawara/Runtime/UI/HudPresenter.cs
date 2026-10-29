using System;
using Karawara.Core.Characters;

namespace RPGKarawara
{
    /// <summary>Owns state subscriptions; the serialized HUD is only a view.</summary>
    public sealed class HudPresenter : IDisposable
    {
        private readonly PlayerUIHudManager view;
        private CharacterRuntimeState state;
        public HudPresenter(PlayerUIHudManager view) { this.view = view ?? throw new ArgumentNullException(nameof(view)); }
        public void Bind(CharacterRuntimeState current)
        {
            Unbind(); state = current;
            if (state == null) return;
            state.HealthChanged += OnHealthChanged; state.MaxHealthChanged += OnMaxHealthChanged;
            Refresh();
        }
        public void Refresh()
        {
            if (state == null || view == null) return;
            view.SetMaxHealthValue(state.MaxHealth); view.SetNewHealthValue(state.Health, state.Health);
        }
        private void OnHealthChanged(int previous, int next) { if (view != null) view.SetNewHealthValue(previous, next); }
        private void OnMaxHealthChanged(int previous, int next) { Refresh(); }
        public void Unbind()
        {
            if (state == null) return;
            state.HealthChanged -= OnHealthChanged; state.MaxHealthChanged -= OnMaxHealthChanged; state = null;
        }
        public void Dispose() => Unbind();
    }
}
