using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CanvasController : MonoBehaviour
{
    // Start is called before the first frame update

    [SerializeField]
    Button button;
    [SerializeField]
    private GameObject text;

    private bool clickable = true;
    private double timer = 0.0;


    void Update()
    {
        if (!clickable)
        {
            timer += Time.deltaTime;
            if (timer >= 5)
            {
                timer = 0;
                text.SetActive(false);
                button.gameObject.GetComponent<Image>().color = Color.green;
                clickable = true;
            }
        }
    }


    public void OnButtonClick()
    {
        if (clickable)
        {
            clickable = false;
            text.SetActive(true);
            button.gameObject.GetComponent<Image>().color = Color.red;
        } 
    }
}
