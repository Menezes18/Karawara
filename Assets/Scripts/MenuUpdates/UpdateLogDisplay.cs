using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class UpdateLogDisplay : MonoBehaviour
{
    public UpdateLog[] updateLog; 
    public TextMeshProUGUI updateText; 
    public Button nextButton, prevButton; 
    public int indexUpdateLog;
    private int currentPage = 0; 
    private int peoplePerPage = 2; 
    void Start()
    {
        if (updateLog != null && updateLog[indexUpdateLog].people != null)
        {
           // UpdatePage();
            
            nextButton.onClick.AddListener(NextPage);
            prevButton.onClick.AddListener(PreviousPage);
        }
    }

    void UpdatePage()
    {

        int startIndex = currentPage * peoplePerPage;
        int endIndex = Mathf.Min(startIndex + peoplePerPage, updateLog[indexUpdateLog].people.Length);


        string pageContent = $"<size=50><color=#321d15>{updateLog[indexUpdateLog].updates[0].version}</color></size>\n";
        pageContent += $"<color=white>{updateLog[indexUpdateLog].updates[0].text}</color>\n\n";

        for (int i = startIndex; i < endIndex; i++)
        {
            var person = updateLog[indexUpdateLog].people[i];
            pageContent += $"<size=30><color=#1e110c>{person.name}</color></size>\n"; 
            foreach (var activity in person.activities)
            {
                pageContent += $"<size=25><color=#46281e>{activity}</color></size>\n"; 
            }
            pageContent += "\n"; 
        }


        updateText.text = pageContent;


        prevButton.interactable = currentPage > 0;
        nextButton.interactable = endIndex < updateLog[indexUpdateLog].people.Length;
    }

    public void NextPage()
    {
        if ((currentPage + 1) * peoplePerPage < updateLog[indexUpdateLog].people.Length)
        {
            currentPage++;
            UpdatePage();
        }
    }

    public void PreviousPage()
    {
        if (currentPage > 0)
        {
            currentPage--;
            UpdatePage();
        }
    }
    public void SetUpdateLog(int newIndex)
    {
        if (newIndex >= 0 && newIndex < updateLog.Length)
        {
            indexUpdateLog = newIndex;
            currentPage = 0; 
            UpdatePage();
        }
    }
}
