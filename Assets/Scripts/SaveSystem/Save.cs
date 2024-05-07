using RPGKarawara;
using System.IO;
using UnityEngine;

namespace RPGKarawara
{
    public class Save : MonoBehaviour
    {
        public static Save Instance;
        string path;

        private void Awake()
        {
            if(Instance == null)
            {
                Instance = this;
            }   
            path = Application.dataPath + "/save.txt";
        }

        public void Carrega()
        {
            if (File.Exists(path))
            {
                string s = File.ReadAllText(path);
                SceneData data = JsonUtility.FromJson<SceneData>(s);
                GameManager.manager.SetPlayerData(data.player);
            }
        }

        public void Salva()
        {
            SceneData data = new SceneData();
            data.player = GameManager.manager.GetPlayer();
            string s = JsonUtility.ToJson(data, true);
            File.WriteAllText(path, s);
        }
    }

    class SceneData
    {
        public PlayerBaseDate player;
    }
}