using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class BlockManager : MonoBehaviour, IDragHandler, IPointerUpHandler
{
    public int cellCount;
    public Transform snapPointVisualizer;

    private RectTransform _rectTransform;
    private Vector2 offset;

    private Dictionary<GridElement, int> hitCounts = new();
    public List<GridElement> elementsHit => hitCounts.Keys.ToList();

    private List<GridElement> nearestElements = new();
    private List<Image> spvImages = new();

    private Vector3 medianPos;
    private bool rotated;

    private void OnEnable()
    {
        _rectTransform = GetComponent<RectTransform>();
        _rectTransform.sizeDelta = Vector2.one /*GridManager.Instance.currentGridSize*/;
        spvImages = snapPointVisualizer.GetComponentsInChildren<Image>().ToList();

        var remoteTriggers = GetComponentsInChildren<RemoteTriggerZone>();

        foreach (var triggerZone in remoteTriggers)
        {
            triggerZone.OnObjectEnteredTrigger += OnHitStart;
            triggerZone.OnObjectExitTrigger += OnHitEnd;
        }

        snapPointVisualizer.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            rotated = !rotated;
            transform.localRotation = GetBlockRotation();
        }

    }

    public void LateUpdate() => SetSnappedPosition();

    #region Snapping

    /// <summary>
    /// Sets the snapping position of the block.
    /// </summary>
    void SetSnappedPosition()
    {
        if (elementsHit.Count < cellCount) return;

        nearestElements = GetClosestMatchesPerChild();
        medianPos = nearestElements.Aggregate(Vector3.zero, (acc, el) => acc + el.cellTransform.position) / nearestElements.Count;

        if (MatchesRelativeLayout(nearestElements) && nearestElements.Count >= cellCount)
        {
            if (!snapPointVisualizer.gameObject.activeSelf)
                snapPointVisualizer.gameObject.SetActive(true);

            snapPointVisualizer.position = medianPos;
            snapPointVisualizer.localRotation = GetBlockRotation();
            SetSPVColors(AreAllAvailable(nearestElements) ? Color.green : Color.red);
        }
    }

    /// <summary>
    /// Gets the nearest hitobject for every child.
    /// </summary>
    /// <returns></returns>
    private List<GridElement> GetClosestMatchesPerChild()
    {
        var childRects = GetComponentsInChildren<RectTransform>().Where(r => r != _rectTransform).ToList();

        var remainingHits = new List<GridElement>(elementsHit);
        var closestMatches = new List<GridElement>();

        foreach (var child in childRects)
        {
            GridElement closest = null;
            float minDist = float.MaxValue;

            foreach (var hit in remainingHits)
            {
                float dist = Vector2.Distance(child.position, hit.cellTransform.position);
                if (dist < minDist)
                {
                    minDist = dist;
                    closest = hit;
                }
            }

            if (closest != null)
            {
                closestMatches.Add(closest);
                remainingHits.Remove(closest);
            }
        }

        return closestMatches;
    }

    /// <summary>
    /// Calculates the rotation of the block.
    /// </summary>
    Quaternion GetBlockRotation()
    {
        return rotated ? Quaternion.Euler(0, 0, -90) : Quaternion.identity;
    }

    /// <summary>
    /// Compares the child / grid offsets to confirm the snap point pattern is that of the snapping object.
    /// </summary>
    /// <param name="candidates"></param>
    /// <returns></returns>
    bool MatchesRelativeLayout(List<GridElement> candidates)
    {
        if (candidates.Count != cellCount) return false;

        var elementOffsets = GetElementLocalOffsets(candidates, medianPos).OrderBy(v => v.x).ThenBy(v => v.y).ToList();
        var childOffsets = GetChildLocalOffsets().OrderBy(v => v.x).ThenBy(v => v.y).ToList();

        for (int i = 0; i < cellCount; i++)
            if (Vector2.Distance(elementOffsets[i], childOffsets[i]) > 1f)
                return false;

        return true;
    }

    /// <summary>
    /// Gets the offsets from the children to the transform.
    /// </summary>
    /// <returns></returns>
    private List<Vector2> GetChildLocalOffsets()
    {
        var rects = GetComponentsInChildren<RectTransform>().Where(r => r != _rectTransform).ToList();
        return rects.Select(r => (Vector2)(r.position - _rectTransform.position)).ToList();
    }

    /// <summary>
    /// Gets the offsets from the nearestElements to their median point.
    /// </summary>
    private List<Vector2> GetElementLocalOffsets(List<GridElement> elements, Vector3 medianPos)
    {
        return elements.Select(e => (Vector2)(e.cellTransform.position - medianPos)).ToList();
    }

    /// <summary>
    /// Checks if all hit elements are available for snapping.
    /// </summary>
    bool AreAllAvailable(List<GridElement> candidates)
    {
        return candidates.All(c => !c.occupied);
    }

    /// <summary>
    /// Sets the color of the snap position visualizer.
    /// </summary>
    /// <param name="_color"></param>
    void SetSPVColors(Color _color)
    {
        foreach(Image img in spvImages)
            img.color = _color;
    }

    #endregion

    #region Drag & Trigger handling

    /// <summary>
    /// Moves the block with the mouse.
    /// </summary>
    public void OnDrag(PointerEventData eventData)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _rectTransform.parent as RectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 localPoint
        );
        _rectTransform.anchoredPosition = localPoint + offset;
    }

    /// <summary>
    /// Places the block.
    /// </summary>
    public void OnPointerUp(PointerEventData eventData) // currently not working
    {
        if (AreAllAvailable(nearestElements))
            foreach (GridElement element in nearestElements)
                GridManager.Instance.SetGridElementOccupation(element, true);

        Debug.Log("Pointer Up! - Destroy!");
        //Destroy(gameObject);
    }

    /// <summary>
    /// Registers a hit from one of the child objects.
    /// </summary>
    private void OnHitStart(Collider2D other)
    {
        var hit = GridManager.Instance.GetGridElementFromName(other.gameObject.name);
        if (hit == null) return;

        if (hitCounts.ContainsKey(hit))
            hitCounts[hit]++;
        else
            hitCounts[hit] = 1;
    }

    /// <summary>
    /// Unregisters a hit from one of the child objects.
    /// </summary>
    private void OnHitEnd(Collider2D other)
    {
        var hit = GridManager.Instance.GetGridElementFromName(other.gameObject.name);
        if (hit == null || !hitCounts.ContainsKey(hit)) return;

        hitCounts[hit]--;

        if (hitCounts[hit] <= 0)
            hitCounts.Remove(hit);
    }

    #endregion

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;

        if (nearestElements == null) return;

        foreach (var el in nearestElements)
        {
            if (el != null)
                Gizmos.DrawSphere(el.cellTransform.position, 10f);
        }
    }

}