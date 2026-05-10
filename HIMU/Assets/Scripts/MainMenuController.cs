using System.Drawing;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Color = UnityEngine.Color;

public class MainMenuController : MonoBehaviour
{
    [SerializeField]
    private Image fadeObject; 
    [SerializeField]
    private float timeTofade = 2.5f;
    private float timer = 0.0f;
    private bool isFading = false;
    private bool canChange = true;
    private void OnEnable()
    {
        Color color = Color.black;
        color.a = 0.0f;
        fadeObject.color = color;
    }

    // Update is called once per frame
    void Update()
    {
        if (isFading)
        {
            timer += Time.deltaTime;
            Color color = fadeObject.color;
            float alpha = timer / timeTofade;
            color.a = alpha;
            fadeObject.color = color;
            if (timer >= timeTofade) {
                isFading = false;
                timer = 0.0f;
                ChangeScene(); 
            }
        }
    }

    public void StartFading()
    {
        if (canChange)
        {
            isFading = true;
            canChange = false;
        }
    }

    private void ChangeScene()
    {
        SceneManager.LoadScene("ConnectionSelectionScene");
    }
}
