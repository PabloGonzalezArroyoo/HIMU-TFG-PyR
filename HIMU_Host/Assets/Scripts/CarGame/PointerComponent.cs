using NUnit.Framework;
using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PointerComponent : MonoBehaviour
{
    [SerializeField]
    private List<Vector2> screenPositions = new List<Vector2>();

    private int currentPos;

    private RectTransform myTransform;

    private CarButtonComponent currentButton = null;

    public GraphicRaycaster backgroundRaycaster;

    private Vector3 GetUpperLeftPoint()
    {
        Vector3[] corners = new Vector3[4];
        myTransform.GetWorldCorners(corners);
        Vector3 upperLeftWorld = corners[1];

        return RectTransformUtility.WorldToScreenPoint(null, upperLeftWorld);
    }

    private void DetectButton()
    {
        PointerEventData eventData = new PointerEventData(EventSystem.current) { position = GetUpperLeftPoint() };
        List<RaycastResult> results = new List<RaycastResult>();
        backgroundRaycaster.Raycast(eventData, results);

        foreach (RaycastResult result in results)
        {
            if (result.gameObject == gameObject) continue;

            CarButtonComponent targetButton = result.gameObject.GetComponent<CarButtonComponent>();
            if (targetButton != null)
            {
                currentButton = targetButton;
                break;
            }
        }
    }

    public void MoveRight()
    {
        if (currentPos < screenPositions.Count - 1)
            currentPos++;
        myTransform.anchoredPosition = screenPositions[currentPos];
        DetectButton();
    }

    public void MoveLeft()
    {
        if (currentPos > 0)
            currentPos--;
        myTransform.anchoredPosition = screenPositions[currentPos];
        DetectButton();
    }

    public void ExecuteButton()
    {
        if (currentButton != null) currentButton.Click();
    }

    private void Awake()
    {
        RacingGameManager.Instance.SetPauseMenuPointer(this);
    }
    private void Start()
    {
        myTransform = GetComponent<RectTransform>();
        currentPos = 0;
        myTransform.anchoredPosition = screenPositions[currentPos];
        DetectButton();
    }
}
