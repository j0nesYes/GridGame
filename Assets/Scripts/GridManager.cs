using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static UnityEditor.Rendering.FilterWindow;

public class GridManager : MonoBehaviour
{
    [Header("Grid Properties:")]
    [Range(10, 30)] public int gridSize;
    [Range(.1f, 1)] public float gridScale;
    [Range(.4f, .6f)] public float noiseThreshold;
    public bool occupyOnStart;

    [Header("Prefabs:")]
    public GameObject rowPrefab;
    public GameObject cellPrefab;

    [Header("Database:")]
    public List<GridElement> gridElements = new();

    private Transform container;
    [HideInInspector] public Vector2 currentGridSize;
    [HideInInspector] public float currentGridSpacing;

    public static GridManager Instance { get; private set; }

    private void OnValidate()
    {
        if(Instance == null)
            Instance = this;    
    }

    #region Grid generation.

    /// <summary>
    /// Generates a grid.
    /// </summary>
    public void Generate() => StartCoroutine(GenerateGrid());
   
    /// <summary>
    /// Generates a grid. (Coroutine)
    /// </summary>
    IEnumerator GenerateGrid()
    {
        if (container == null)
            container = GameObject.Find("Grid_Main").transform;

        ClearGrid();
        int elementCount = 0;

        for (int i = 0; i < gridSize; i++)
        {
            GameObject goRow = Instantiate(rowPrefab, container);
            goRow.name = $"ROW_{i + 1}";

            for (int j = 0; j < gridSize; j++)
            {
                string columnName = ConvertToColumnLetter(j + 1);

                GameObject goCell = Instantiate(cellPrefab, goRow.transform);
                string cellName = $"{columnName}{i + 1}_{elementCount}";
                goCell.name = cellName;

                GridElement newElement = new GridElement
                {
                    column = columnName,
                    row = i + 1,
                    occupied = false,
                    cellTransform = goCell.GetComponent<RectTransform>(),
                    _image = goCell.GetComponent<Image>(),
                    trigger = goCell.GetComponent<BoxCollider2D>()
                };

                elementCount++;
                gridElements.Add(newElement);
            }
        }

        if(container != null)
            currentGridSpacing = container.GetComponent<VerticalLayoutGroup>().spacing;

        Debug.Log($"<color=green><b> Grid generated with {gridElements.Count} elements! </b></color>");

#if UNITY_EDITOR
        if (!Application.isPlaying)
            EditorApplication.delayCall += () => SetGridDefaultState();
#endif
        yield return new WaitForEndOfFrame();
        SetGridDefaultState();
    }

    /// <summary>
    /// Clears the current grid.
    /// </summary>
    void ClearGrid()
    {
        gridElements.Clear();

        for (int i = container.childCount - 1; i >= 0; i--)
            DestroyImmediate(container.GetChild(i).gameObject);
    }

    /// <summary>
    /// Sets the sizes of all cell triggers.
    /// </summary>
    public void SetGridDefaultState()
    {
        float xOffset, yOffset;
        bool hasOccupiedCells;

        currentGridSize = new Vector2(gridElements[0].cellTransform.rect.width, gridElements[0].cellTransform.rect.height);

        do
        {
            hasOccupiedCells = false;
            xOffset = Random.Range(0f, 1000f);
            yOffset = Random.Range(0f, 1000f);

            foreach (var element in gridElements)
            {
                // set trigger size
                element.trigger.size = currentGridSize;
                SetGridElementOccupation(element, false);

                // apply perlin noise occupation
                if (occupyOnStart)
                {
                    float noiseValue = Mathf.PerlinNoise((ConvertColumnLetterToIndex(element.column) + xOffset) * gridScale, (element.row + yOffset) * gridScale);

                    if (noiseValue > noiseThreshold)
                    {
                        SetGridElementOccupation(element, true);
                        hasOccupiedCells = true;
                    }
                }
            }

        } while (!hasOccupiedCells);

        if (occupyOnStart)
            FixGridIntegrity();

        Debug.Log("Grid successfully generated!");
    }

    /// <summary>
    /// Fixes one-cell-gaps.
    /// </summary>
    void FixGridIntegrity()
    {
        List<GridElement> elementsToFill = new();
        List<GridElement> elementsToClear = new();

        foreach (var element in gridElements)
        {
            int occupiedNeighbors = CountOccupiedNeighbors(element);

            if (!element.occupied && occupiedNeighbors == 4)
                elementsToFill.Add(element);
            else if (element.occupied && occupiedNeighbors == 0)
                elementsToClear.Add(element);
        }

        foreach (var element in elementsToFill)
            SetGridElementOccupation(element, true);

        foreach (var element in elementsToClear)
            SetGridElementOccupation(element, false);

        Debug.Log($"Fixed {elementsToFill.Count} gaps and removed {elementsToClear.Count} isolated elements.");
    }


    /// <summary>
    /// Assigns a cell its name.
    /// </summary>
    string ConvertToColumnLetter(int columnNumber)
    {
        string columnName = "";

        while (columnNumber > 0)
        {
            columnNumber--;
            columnName = (char)('A' + (columnNumber % 26)) + columnName;
            columnNumber /= 26;
        }
        return columnName;
    }

    #endregion

    #region Single Cell Operations

    /// <summary>
    /// Gets a grid element with its coordinates.
    /// </summary>
    public GridElement GetGridElementAt(int row, int col)
    {
        if (row >= 0 && row < gridSize && col >= 0 && col < gridSize)
            return gridElements[row * gridSize + col];
        return null;
    }

    /// <summary>
    /// Gets a certain element from the name of its trigger.
    /// </summary>
    public GridElement GetGridElementFromName(string name)
    {
        string[] substrings = name.Split('_');
        if (int.TryParse(substrings[1], out int number))
            return gridElements[number];
        else
        {
            Debug.LogWarning($"<color=red> Not a valid element number!</color>");
            return null;
        }
    }

    /// <summary>
    /// Converts a column letter to a column index.
    /// </summary>
    public int ConvertColumnLetterToIndex(string column)
    {
        int index = 0;
        foreach (char c in column)
        {
            index = index * 26 + (c - 'A' + 1);
        }
        return index - 1;
    }

    /// <summary>
    /// Checks how many cells, neighboring a certain cell are occupied.
    /// </summary>
    public int CountOccupiedNeighbors(GridElement element)
    {
        (int, int)[] offsets = { (-1, 0), (1, 0), (0, -1), (0, 1) };
        int occupiedCount = 0;

        int colIndex = ConvertColumnLetterToIndex(element.column);

        foreach (var (di, dj) in offsets)
        {
            GridElement neighbor = GetGridElementAt(element.row - 1 + di, colIndex + dj);
            if (neighbor != null && neighbor.occupied)
                occupiedCount++;
        }

        return occupiedCount;
    }

    /// <summary>
    /// Sets the occupation state of a grid element.
    /// </summary>
    public void SetGridElementOccupation(GridElement gridElement, bool occupied)
    {
        gridElement.occupied = occupied;
        gridElement._image.color = occupied ? Color.grey : Color.white;
    }

    #endregion
}

[System.Serializable]
public class GridElement
{
    [Header("Location")]
    public string column;
    public int row;

    [Header("State")]
    public bool occupied;

    [HideInInspector] public RectTransform cellTransform;
    [HideInInspector] public Image _image;
    [HideInInspector] public BoxCollider2D trigger;
}

#if UNITY_EDITOR
[CustomEditor(typeof(GridManager))]
public class GridGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GridManager gridGen = (GridManager)target;

        if (GUILayout.Button("Generate Grid"))
            gridGen.Generate();
    }
}
#endif
