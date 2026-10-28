namespace Karawara.Core.Characters
{
    /// <summary>Session-local identities. Never serialize these IDs into a save.</summary>
    public interface ICharacterRegistry
    {
        int Register(CharacterRuntimeState character);
        void Unregister(int id);
        bool TryGet(int id, out CharacterRuntimeState character);
        void Clear();
    }
}
