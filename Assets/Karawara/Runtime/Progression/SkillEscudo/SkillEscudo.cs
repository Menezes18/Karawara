using System.Collections.Generic;
using UnityEngine;
namespace RPGKarawara.SkillTree
{
    public class SkillEscudo : MonoBehaviour
    {
        private sealed class BlockOwners { public bool previous; public readonly HashSet<SkillEscudo> active = new HashSet<SkillEscudo>(); }
        private static readonly Dictionary<PlayerManager, BlockOwners> blocks = new Dictionary<PlayerManager, BlockOwners>();
        public PlayerManager Owner { get; private set; }
        private bool ownsBlock;
        public void BindOwner(PlayerManager owner) { Release(); Owner = owner; }
        public void Init(float duration)
        {
            if (Owner == null) Owner = GetComponentInParent<PlayerManager>();
            if (Owner == null) return;
            if (!blocks.TryGetValue(Owner, out var state)) blocks[Owner] = state = new BlockOwners { previous = Owner.State.IsBlocking };
            state.active.Add(this); ownsBlock = true; Owner.State.SetBlocking(true);
            CancelInvoke(nameof(DesativarEscudo)); Invoke(nameof(DesativarEscudo), duration);
        }
        private void DesativarEscudo() => Release();
        private void Release()
        {
            CancelInvoke(nameof(DesativarEscudo));
            if (ownsBlock && !ReferenceEquals(Owner, null) && blocks.TryGetValue(Owner, out var state))
            {
                state.active.Remove(this);
                if (state.active.Count == 0) { blocks.Remove(Owner); if (Owner != null) Owner.State.SetBlocking(state.previous); }
            }
            ownsBlock = false; Owner = null;
        }
        private void OnDisable() => Release();
        private void OnDestroy() => Release();
    }
}
