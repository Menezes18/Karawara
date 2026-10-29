using UnityEngine;
namespace RPGKarawara
{
    public class FogWallInteractable : MonoBehaviour
    {
        [Header("Fog")] [SerializeField] GameObject[] fogGameObjects;
        public int fogWallID;
        public ObservableValue<bool> isActive = new ObservableValue<bool>(false);
        void OnEnable()
        {
            isActive.OnValueChanged += OnIsActiveChanged;
            OnIsActiveChanged(false, isActive.Value);
            WorldObjectManager.GetOrCreateInstance().AddFogWallToList(this);
        }
        void OnDisable()
        {
            isActive.OnValueChanged -= OnIsActiveChanged;
            if (WorldObjectManager.instance != null) WorldObjectManager.instance.RemoveFogWallFromList(this);
        }
        void OnIsActiveChanged(bool previous, bool active)
        {
            if (fogGameObjects == null) return;
            foreach (var fog in fogGameObjects) if (fog != null) fog.SetActive(active);
        }
    }
}
