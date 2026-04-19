using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VibrantUIComponent : MonoBehaviour
{
    public float scaleMultiplier = 1.5f;
    public float timeToScale = 3f;

    private float scalePerTick;
    private float scaleDifference;
    private double timer;
    private bool isExpanding;

    //private Transform transform;

    // Start is called before the first frame update
    void Start()
    {
        isExpanding = true;
        scaleDifference = scaleMultiplier - 1;
        scalePerTick = scaleDifference / timeToScale;
        timer = 0.0;
    }

    // Update is called once per frame
    void Update()
    {
        timer += Time.deltaTime;

        if (timer >= timeToScale) { 
            isExpanding = (!isExpanding);
            timer = 0.0;
        }
        transform.localScale = transform.localScale + transform.localScale * scalePerTick * Time.deltaTime * (isExpanding ? 1 : -1);
    }
}
