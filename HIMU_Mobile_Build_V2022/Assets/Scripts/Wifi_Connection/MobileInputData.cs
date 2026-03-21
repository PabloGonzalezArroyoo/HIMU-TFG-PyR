using UnityEngine;
using System;
using UnityEngine.UIElements;

public enum MobileEvent
{
    Touch, Drag, Shake
}

[Serializable]
public class MobileInputData
{
    public float horizontal;
    public float vertical;
    public bool jump;
}