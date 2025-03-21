using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class BlockManager : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    private RectTransform _rectTransform;
    private Vector2 offset;

    public List<GridElement> elementsHit = new();

    private void Start()
    {
        _rectTransform = GetComponent<RectTransform>();
        _rectTransform.sizeDelta = GridManager.Instance.currentGridSize;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        Debug.Log("Click Element");

        RectTransformUtility.ScreenPointToLocalPointInRectangle(_rectTransform.parent as RectTransform, eventData.position, eventData.pressEventCamera, out offset);
        offset = _rectTransform.anchoredPosition - offset;
    }

    public void OnDrag(PointerEventData eventData)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_rectTransform.parent as RectTransform, eventData.position, eventData.pressEventCamera, out Vector2 localPoint);
        _rectTransform.anchoredPosition = localPoint + offset;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        GridElement hitElement = GridManager.Instance.GetGridElementFromName(other.gameObject.name);

        if (!elementsHit.Contains(hitElement))
            elementsHit.Add(hitElement);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        GridElement hitElement = GridManager.Instance.GetGridElementFromName(other.gameObject.name);

        if (elementsHit.Contains(hitElement))
            elementsHit.Remove(hitElement);
    }
}
