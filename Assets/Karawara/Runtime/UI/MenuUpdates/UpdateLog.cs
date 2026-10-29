using UnityEngine;

[CreateAssetMenu(fileName = "NewUpdateLog", menuName = "ScriptableObjects/UpdateLog")]
public class UpdateLog : ScriptableObject
{
    [System.Serializable]
    public class VersionInfo
    {
        public string version; // Ex: "Versão 1.0"
        [TextArea(3, 10)]
        public string text; // Texto de atualização
    }

    [System.Serializable]
    public class PersonInfo
    {
        public string name; // Nome da pessoa
        [TextArea(1, 5)]
        public string[] activities; // Atividades da pessoa
    }

    public VersionInfo[] updates; // Informações de versão
    public PersonInfo[] people; // Informações de pessoas e atividades

    public string GetFormattedUpdateLog()
    {
        string output = $"<size=50><color=#321d15>{updates[0].version}</color> - <color=white>{updates[0].text}</color> </size>\n"; // Texto da versão

        foreach (var person in people)
        {
            output += $"<size=30><color=#1e110c>{person.name}</color></size>"; // Nome da pessoa como h2
            foreach (var activity in person.activities)
            {
                output += $"<size=25><color=#46281e>{activity}</color></size>\n"; // Atividade como h3
            }
        }
        return output;
    }

}