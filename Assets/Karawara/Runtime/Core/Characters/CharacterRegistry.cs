using System;
using System.Collections.Generic;

namespace Karawara.Core.Characters
{
    /// <summary>Owned by a local session. Register again explicitly after revive/re-enable.</summary>
    public sealed class CharacterRegistry : ICharacterRegistry
    {
        private readonly Dictionary<int, CharacterRuntimeState> characters = new Dictionary<int, CharacterRuntimeState>();
        private readonly Dictionary<CharacterRuntimeState, int> ids = new Dictionary<CharacterRuntimeState, int>();
        private int lastId;

        public int Register(CharacterRuntimeState character)
        {
            if (character == null) throw new ArgumentNullException(nameof(character));
            if (!character.IsActive || character.IsDead) throw new ArgumentException("Only active, living characters can register.", nameof(character));
            if (character.Registry != null && character.Registry != this)
                throw new InvalidOperationException("Character already belongs to another session.");
            if (ids.TryGetValue(character, out int existing)) return existing;
            int id = checked(lastId + 1);
            lastId = id;
            characters.Add(id, character);
            ids.Add(character, id);
            character.Registry = this;
            character.Unavailable += OnUnavailable;
            return id;
        }

        public void Unregister(int id)
        {
            Remove(id, true);
        }

        private void Remove(int id, bool invalidate)
        {
            if (!characters.TryGetValue(id, out var character)) return;
            characters.Remove(id);
            ids.Remove(character);
            character.Unavailable -= OnUnavailable;
            character.Registry = null;
            if (invalidate) character.Invalidate();
        }

        public bool TryGet(int id, out CharacterRuntimeState character)
        {
            if (characters.TryGetValue(id, out character) && character.IsActive && !character.IsDead) return true;
            character = null;
            return false;
        }

        public void Clear()
        {
            var registeredIds = new List<int>(characters.Keys);
            foreach (int id in registeredIds) Unregister(id);
        }

        private void OnUnavailable(CharacterRuntimeState character)
        {
            // Already inside the state's invalidation notification. Do not notify twice.
            if (ids.TryGetValue(character, out int id)) Remove(id, false);
        }
    }
}
