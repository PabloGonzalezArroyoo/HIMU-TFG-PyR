using UnityEngine;

public class MovementControls : MonoBehaviour
{
    [SerializeField]
    private WebRTCReceiver reciever;

    public void UpMovement()
    {
        InputData inputData = new InputData(Vector2.up, Vector2.zero, false, false, false);
        reciever.SendInput(JsonUtility.ToJson(inputData));
    }

    public void DownMovement()
    {
        InputData inputData = new InputData(Vector2.down, Vector2.zero, false, false, false);
        reciever.SendInput(JsonUtility.ToJson(inputData));
    }

    public void LeftMovement()
    {
        InputData inputData = new InputData(Vector2.left, Vector2.zero, false, false, false);
        reciever.SendInput(JsonUtility.ToJson(inputData));
    }

    public void RightMovement()
    {
        InputData inputData = new InputData(Vector2.right, Vector2.zero, false, false, false);
        reciever.SendInput(JsonUtility.ToJson(inputData));
    }

    public void SetReceiver(WebRTCReceiver r)
    {
        reciever = r;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
