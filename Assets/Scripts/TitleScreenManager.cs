using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace SG
{
    public class TitleScreenManager : MonoBehaviour
    {
        

        public void StartNewGame()
        {
            StartCoroutine(WorldSaveGameManager.instance.LoadNewGame());
        }
    }
}
