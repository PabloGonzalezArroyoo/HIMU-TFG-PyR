using TMPro;
using UnityEngine;

public class FiniishLineComponent : MonoBehaviour
{
    [SerializeField]
    private int lapsLimit = 5;
    private int lapCounter = 1;

    [SerializeField]
    private TextMeshProUGUI lapText;


    private void OnTriggerExit(Collider other)
    {
        if (lapCounter + 1 > lapsLimit) CarGameManager.Instance.EndGame();
        else
        {
            lapCounter++;
            lapText.text = "Lap " + lapCounter + " / " + lapsLimit;
        }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        lapText.text = "Lap " + lapCounter + " / " + lapsLimit;
    }
}
