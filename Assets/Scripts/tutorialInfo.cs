using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RPGKarawara
{
    [CreateAssetMenu(menuName = "Tutorial Infos")]
    public class tutorialInfo : ScriptableObject
    {
         [TextArea(3, 10)]
        public String textoExplicativo;
        public String titulo;
        public CustomRenderTexture video;
    }
}
