using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    public class SaveButton : MonoBehaviour
    {
        public void OnButtonSave()
        {
            Save.Instance.Salva();
        }
        public void OnButtonLoad()
        {
            Save.Instance.Carrega();
        }
    }
}
