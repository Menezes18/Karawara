using UnityEngine;

namespace System.MiniMap
{

    public class PlayerMiniMap : MonoBehaviour
    {

        private void OnEnable()
        {
            AssignAsTarget();
        }


        public void AssignAsTarget()
        {
            var miniMap = SystemMiniMap.ActiveMiniMap;
            if (miniMap == null) return;

            miniMap.SetTarget(gameObject);
        }
    }
}