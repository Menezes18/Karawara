using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    public class Active : MonoBehaviour
    {
        [SerializeField] private GameObject objecto;
        void Start()
        {
            objecto.SetActive(!objecto.active);
        }
    }
}
