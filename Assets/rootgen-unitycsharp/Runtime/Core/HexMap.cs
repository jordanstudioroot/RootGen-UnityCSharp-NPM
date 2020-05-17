﻿using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using RootLogging;
using RootCollections;
using QuikGraph;
using QuikGraph.Algorithms;
using MathNet.Numerics.LinearAlgebra;

public class HexMap : MonoBehaviour{
// FIELDS ~~~~~~~~~~

// ~ Static

// ~~ public

// ~~ private

// ~ Non-Static

// ~~ public

// ~~ private
    private bool _editMode;
    public Transform[] Columns {
        get; private set;
    }
    private Text _cellLabelPrefab;
    private List<HexUnit> _units = new List<HexUnit>();
    private int WidthInChunks {
        get {
            return Width / MeshConstants.ChunkSizeX;
        }
    }

    private int HeightInChunks {
        get {
            return Width / MeshConstants.ChunkSizeZ;
        }
    }

    private int _searchFrontierPhase;
//Init to -1 so new maps always get centered.
/// <summary>
/// The index of the MeshChunk column currently centered below the camera.
/// </summary>
    private int _currentCenterColumnIndex = -1;
    public HexGridChunk[] Chunks {
        get; private set;
    }
    private CellShaderData _cellShaderData;
    private Material _terrainMaterial;
    private bool _uiVisible;

    public HexGrid<HexCell> HexGrid {
        get; private set;
    }

    public NeighborGraph NeighborGraph {
        get {
            if (HexGrid == null)
                throw new NullHexGridException();
            return NeighborGraph.FromHexGrid(HexGrid);
        }
    }

    public RiverGraph RiverGraph {
        get {
            if (HexGrid == null)
                throw new NullHexGridException();
            return RiverGraph.FromHexGrid(HexGrid);
        }
    }

    public RoadGraph RoadGraph {
        get {
            if (HexGrid == null)
                throw new NullHexGridException();
            return RoadGraph.FromHexGrid(HexGrid);
        }
    }

    public ElevationGraph ElevationGraph {
        get {
            if (HexGrid == null)
                throw new NullHexGridException();
            return ElevationGraph.FromHexGrid(HexGrid);
        }
    }

    public int Width {
        get {
            return HexGrid.Columns;
        }
    }

    public int Height {
        get {
             return HexGrid.Rows;
        }
    }

    public bool ShowGrid {
        set {
            HexGridShaderKeywords.GridOn = value;
        }
    }

    public bool EditMode {
        set {
            HexGridShaderKeywords.HexMapEditMode = value;
            ShowUIAllChunks(!value);
            _editMode = value;
        }

        get {
            return _editMode;
        }
    }

    public HexCell Center {
        get {
            if (HexGrid == null)
                throw new NullHexGridException();
            return HexGrid.Center;
        }
    }

    public int Size {
        get {
            if (HexGrid == null)
                throw new NullHexGridException();
            return HexGrid.Size;
        }
    }

    public bool IsWrapping {
        get {
            if (HexGrid == null)
                throw new NullHexGridException();
            return HexGrid.IsWrapping;
        }
    }

    public int WrapSize {
        get {
            if (HexGrid == null)
                throw new NullHexGridException();

            return HexGrid.WrapSize;
        }
    }

// ~~ private

// INDEXERS ~~~~~~~~~~

// ~ Static

// ~~ public

// ~~ private

// ~ Non-Static

// ~~ public

// ~~ private

// METHODS ~~~~~~~~~

// ~ Static

// ~~ public

    private void Render(
        RoadGraph roadGraph,
        RiverGraph riverGraph,
        NeighborGraph neighborGraph,
        HexGrid<HexCell> hexGrid,
        float cellOuterRadius,
        ElevationGraph elevationGraph
    ) {
        foreach(HexGridChunk chunk in Chunks) {
            chunk.Triangulate(
                hexGrid,
                cellOuterRadius,
                neighborGraph,
                riverGraph,
                roadGraph,
                elevationGraph            
            );
        }
    }
/// <summary>
/// Initialize the hex map to an empty flat plain.
/// </summary>
/// <param name="bounds">
///     A bounds object representing the dimensions of the hex map. Will
///     be scaled to fit within a multiple of MeshConstants.ChunkSize.
/// </param>
/// <param name="wrapping">
///     Should the horizontal bounds of the grid wrap into their opposite
///     side?
/// </param>
/// <param name="editMode">
///     Should the map be editable immediately after being initialized?
/// </param>
/// <param name="cellSize">
///     The distance of each hex cell from its center to a circle 
///     intersecting each corner of the hexagon. Scales the size of all
///     other visual elements on the hex map.
/// </param>
/// <param name="seed">
///     The random seed used to initialize the hash grid for the map.
/// </param>
    public HexMap Initialize(
        Rect bounds,
        int seed,
        float cellSize,
        bool wrapping,
        bool editMode
    ) {
        if (
            bounds.x < 0 || bounds.y < 0 ||
            !bounds.size.IsFactorOf(MeshConstants.ChunkSize)
        ) {
            RootLog.Log(
                "Unsupported map size. Clamping dimensions to chunk size.",
                Severity.Warning,
                "HexMap"
            );

            Vector2 clamped =
                bounds.size.ClampToFactorOf(MeshConstants.ChunkSize);

            bounds = new Rect(
                0,
                0,
                clamped.x,
                clamped.y
            );
        }

        HexGrid = new HexGrid<HexCell>(
            (int)bounds.height,
            (int)bounds.width,
            wrapping
        );

        Columns = CreateColumns(
            Width / MeshConstants.ChunkSizeX
        );
        
        Chunks = CreateChunks(
            CreateColumns(Width / MeshConstants.ChunkSizeX),
            Width / MeshConstants.ChunkSizeX,
            Height / MeshConstants.ChunkSizeZ
        );

        for (
            int index = 0;
            index < (int)bounds.width * (int)bounds.height;
            index++
        ) {
            int axialRow = HexGrid.AxialRowFromIndex(index);
            int axialColumn = HexGrid.AxialColumnFromIndex(index);

            HexGrid.SetElement(
                CreateCell(
                    axialColumn,
                    axialRow,
                    index,
                    cellSize,
                    HexGrid
                ),
                index
            );

            int chunkX = axialRow / MeshConstants.ChunkSizeX;
            int chunkZ = axialColumn / MeshConstants.ChunkSizeZ;
            
            HexGridChunk chunk = Chunks[
                chunkX + chunkZ * WidthInChunks
            ];
            
            int localX = axialRow - chunkX * MeshConstants.ChunkSizeX;
            int localZ = axialColumn - chunkZ * MeshConstants.ChunkSizeZ;
            
            AddCellToChunk(
                localX,
                localZ,
                HexGrid.GetElement(axialRow, axialColumn),
                chunk
            );
        }

        _editMode = editMode;
// Set to -1 so new maps always gets centered.
        _currentCenterColumnIndex = -1;

        // TODO: This value will need to be serialized when games are saved,
//       so it should probably be stored in such a way that when the
//       maps Save() method is called it also gets saved and restored.
        HexagonPoint.InitializeHashGrid(seed);

        ClearUnits(_units);
        ClearColumns(Columns);

        if (!_terrainMaterial)
            _terrainMaterial = Resources.Load<Material>("Terrain");

        RootLog.Log(
            "HexCell matrix created.\n\n" +
            NeighborGraph.ToString(),
            Severity.Information,
            "HexMap"
        );

        _cellShaderData.Initialize(
            Width,
            Height
        );

        Render(
            RoadGraph,
            RiverGraph,
            NeighborGraph,
            HexGrid,
            cellSize,
            ElevationGraph
        );

        return this;
    }

    public HexCell GetCellAtIndex(int index) {
        return HexGrid.GetElement(index);
    }

    public HexCell GetCellAtRowAndColumn(int row, int column) {
        return HexGrid.GetElement(row, column);
    }

    public static HexMap Empty(
        Rect bounds,
        int seed,
        float cellOuterRadius,
        bool wrapping,
        bool editMode
    ) {
        HexMap result = CreateHexMapGameObject();

        result.GetComponent<HexMap>().Initialize(
            bounds,
            seed,
            cellOuterRadius,
            wrapping,
            editMode
        );

        return result;
    }

    public void ClearColumns(Transform[] columns) {
         if (columns != null) {
            for (int i = 0; i < columns.Length; i++) {
                Destroy(columns[i].gameObject);
            }
        }
    }

/// <summary>
/// Returns true if the provided dimensions 
/// </summary>
/// <param name="sizeX"></param>
/// <param name="sizeZ"></param>
/// <param name="meshChunkSizeX"></param>
/// <param name="meshChunkSizeZ"></param>
/// <returns></returns>
    public bool Is2DFactorOf(
        int sizeX,
        int sizeZ,
        int meshChunkSizeX,
        int meshChunkSizeZ
    ) {
        if (
            sizeX < meshChunkSizeX ||
            sizeX % meshChunkSizeX != 0 ||
            sizeZ < meshChunkSizeZ ||
            sizeZ % meshChunkSizeZ != 0
        ) {
            return false;
        }

        return true;
    }

    public Rect ClampToChunkSize(
        int x,
        int z,
        int chunkSizeX,
        int chunkSizeZ
    ) {
        int xClamped = Mathf.Clamp(
            x,
            chunkSizeX,
            x - (x % chunkSizeX)
        );

        int zClamped = Mathf.Clamp(
            z,
            chunkSizeZ,
            z - (z % chunkSizeZ)
        );

        return new Rect(0, 0, xClamped, zClamped);
    }

    public HexCell GetCell(
        Vector3 position,
        float outerCellRadius
    ) {

        if (HexGrid == null)
            throw new System.NullReferenceException(
                "HexMap has not been initialized with a HexGrid."
            );

        position = transform.InverseTransformPoint(position);
        
        CubeVector coordinates =
            CubeVector.FromPosition(
                position,
                outerCellRadius,
                HexGrid.WrapSize
            );
        
        return GetCell(coordinates);
    }

    public HexCell GetCell(
        CubeVector coordinates
    ) {
        int z = coordinates.Z;

        // Check for array index out of bounds.
        if (z < 0 || z >= Height) {
            return null;
        }

        int x = coordinates.X + z / 2;
        
        // Check for array index out of bounds.
        if (x < 0 || x >= Width) {
            return null;
        }
        
        return HexGrid.GetElement(x + z * Width);
    }

    public HexCell GetCell(
        Ray ray,
        float outerRadius,
        int wrapSize
    ) {
        RaycastHit hit;
        
        if (Physics.Raycast(ray, out hit)) {
            return GetCell(
                hit.point,
                outerRadius
            );
        }

        return null;
    }

    public HexCell GetCell(int xOffset, int zOffset) {
        return HexGrid.GetElement(xOffset + zOffset * Width);
    }

    public HexCell GetCell(int cellIndex) {
        return HexGrid.GetElement(cellIndex);
    }

    public IEnumerable<HexEdge> GetPath(
        HexCell fromCell,
        HexCell toCell,
        HexUnit unit,
        NeighborGraph graph
    ) {
        return AStarSearch(fromCell, toCell, unit, graph);

// Presentation concerns should not be in this method.        
//        SetPathDistanceLabelAndEnableHighlights(toCell, unit.Speed);
    }

    public void AddUnit(
        HexUnit unit,
        HexCell location,
        float orientation
    ) {
        _units.Add(unit);
        
        unit.Grid = this;
        unit.Location = location;
        unit.Orientation = orientation;
    }

    public void RemoveUnit(HexUnit unit) {
        _units.Remove(unit);
        unit.Die();
    }

// TODO: This is a presentation concern and should be moved out of
//       this class.
    public void IncreaseVisibility(
        HexCell fromCell,
        int range
    ) {
        List<HexCell> cells =
        GetVisibleCells(
            fromCell
//            range
        );

        for (int i = 0; i < cells.Count; i++) {
            cells[i].IncreaseVisibility();
        }

        ListPool<HexCell>.Add(cells);
    }

// TODO: This is a presentation concern and should be moved out of
//       this class.
    public void DecreaseVisibility(
        HexCell fromCell,
        int range,
        ElevationGraph elevationGraph
    ) {
        List<HexCell> cells =
            GetVisibleCells(
                fromCell
//              range
            );

        for (int i = 0; i < cells.Count; i++) {
            cells[i].DecreaseVisibility();
        }

        ListPool<HexCell>.Add(cells);
    }

// TODO: This is a presentation concern and should be moved out of this
//       class
    public void ResetVisibility() {
        for (int i = 0; i < HexGrid.Size; i++) {
            HexGrid.GetElement(i).ResetVisibility();
        }

        for (int i = 0; i < _units.Count; i++) {
            HexUnit unit = _units[i];
            
            IncreaseVisibility(
                unit.Location,
                unit.VisionRange
            );
        }
    }


    public void CenterMap(
        float xPosition,
        float cellOuterRadius
    ) {
        float innerDiameter = 
            HexagonPoint.GetOuterToInnerRadius(cellOuterRadius) * 2f;
        // Get the column index which the x axis coordinate is over.
        int centerColumnIndex =
            (int) (xPosition / (innerDiameter * MeshConstants.ChunkSizeX));

        if (centerColumnIndex == _currentCenterColumnIndex) {
            return;
        }

        _currentCenterColumnIndex = centerColumnIndex;

        int minColumnIndex = centerColumnIndex - WidthInChunks / 2;
        int maxColumnIndex = centerColumnIndex + WidthInChunks / 2;

        Vector3 position;
        position.y = position.z = 0f;

        for (int i = 0; i < Columns.Length; i++) {
            if (i < minColumnIndex) {
                position.x = WidthInChunks *
                                (innerDiameter * MeshConstants.ChunkSizeX);
            }
            else if (i > maxColumnIndex) {
                position.x = WidthInChunks *
                                -(innerDiameter * MeshConstants.ChunkSizeX);
            }
            else {
                position.x = 0f;
            }

            Columns[i].localPosition = position;
        }
    }

    public void MakeChildOfColumn(Transform child, int columnIndex) {
        child.SetParent(Columns[columnIndex], false);
    }

/// <summary>
///     Creates an empty parentless GameObject and adds an uninitialized
///     HexMap component.
/// </summary>
/// <returns>
///     The unitialized HexMap component which has been added to the
///     GameObject.
/// </returns>
    public static HexMap CreateHexMapGameObject() {
        GameObject resultObj = new GameObject("Hex Map");
        HexMap resultMono = resultObj.AddComponent<HexMap>();
        return resultMono;
    }

/// <summary>
///     Switches the UI on and off for all HexGridChunks, enabling and disabling
///     features such as the distance from the currently selected hex cell.
/// </summary>
/// <param name="visible">
///     The visible state of all HexGridChunks.
/// </param>
    private void ShowUIAllChunks(bool visible) {
        for (int i = 0; i < Chunks.Length; i++) {
            Chunks[i].ShowUI(visible);
        }
    }

/// <summary>
/// Destroy all HexUnits on this HexGrid.
/// </summary>
    private void ClearUnits(List<HexUnit> units) {
        for (int i = 0; i < units.Count; i++) {
            units[i].Die();
        }

        units.Clear();
    }

    private double GetPathfindingEdgeWeight(HexEdge edge) {
        return 0d;
    }

    private double GetPathfindingHeursitic(HexCell cell) {
        return 0d;
    }

/// <summary>
/// Search this HexGrid.
/// </summary>
/// <param name="start"></param>
/// <param name="end"></param>
/// <param name="unit"></param>
/// <returns></returns>
    private IEnumerable<HexEdge> AStarSearch(
        HexCell start,
        HexCell end,
        HexUnit unit,
        NeighborGraph graph
    ) {
        IEnumerable<HexEdge> result;
        
//        AlgorithmExtensions.ShortestPathsAStar<HexCell, HexEdge>(
//            graph,
//            GetPathfindingEdgeWeight,
//            GetPathfindingHeursitic,
//            start
//        ).Invoke(end, out result);
        
        return null;

/*        int speed = unit.Speed;

        _searchFrontierPhase += 2;

        if (_searchFrontier == null)
        {
            _searchFrontier = new CellPriorityQueue();
        }
        else
        {
            _searchFrontier.Clear();
        }

// Temporarily using a list instead of a priority queue.
// Should optimize this later.
//
        start.SearchPhase = _searchFrontierPhase;
        start.Distance = 0;
        _searchFrontier.Enqueue(start);

        while (_searchFrontier.Count > 0)
        {
            HexCell current = _searchFrontier.Dequeue();
            current.SearchPhase += 1;

            if (current == end)
            {
                return true;
            }

            int currentTurn = (current.Distance - 1) / speed;

            for (HexDirection direction = HexDirection.Northeast; direction <= HexDirection.Northwest; direction++)
            {
                HexCell neighbor = current.GetNeighbor(direction);

                if 
                (
                    neighbor == null ||
                    neighbor.SearchPhase > _searchFrontierPhase
                )
                {
                    continue;
                }

                if (!unit.IsValidDestination(neighbor))
                {
                    continue;
                }

                int moveCost = unit.GetMoveCost(current, neighbor, direction);

                if (moveCost < 0)
                {
                    continue;
                }

// Wasted movement points are factored into the cost of cells outside
// the boundary of the first turn by adding the turn number multiplied
// by the speed plus the cost to move into the cell outside the boundary
// of the first turn. This method ensures that the the distances with
// which the algorithm is using to calculate the best path take into
// account wasted movement points.
//
                int distance = current.Distance + moveCost;
                int turn = (distance - 1) / speed;

                if (turn > currentTurn) {
                    distance = turn * speed + moveCost;
                }

                if (neighbor.SearchPhase < _searchFrontierPhase) {
                    neighbor.SearchPhase = _searchFrontierPhase;
                    neighbor.Distance = distance;
                    neighbor.PathFrom = current;
                    neighbor.SearchHeuristic =
                        neighbor.Coordinates.DistanceTo(end.Coordinates);

                    _searchFrontier.Enqueue(neighbor);
                }
                else if (distance < neighbor.Distance) {
                    int oldPriority = neighbor.SearchPriority;
                    neighbor.Distance = distance;
                    neighbor.PathFrom = current;
                    _searchFrontier.Change(neighbor, oldPriority);
                }
            }
        }

        return false;
*/   
    }

// This should be a simple graph traversal which stops when it hits an
// edge which has a higher elevation.
    private List<HexCell> GetVisibleCells(
        HexCell fromCell
//        int sightRange
    ) {
        ElevationGraph elevationGraph = ElevationGraph.FromHexGrid(
            HexGrid
        );

        List<HexCell> visibleCells = new List<HexCell>();
        
        Queue<HexCell> open = new Queue<HexCell>();
        List<HexCell> closed = new List<HexCell>();

        HexCell current = fromCell;
        open.Enqueue(current);
        visibleCells.Add(current);
        
        List<HexEdge> visibleEdges = elevationGraph.GetVisibleEdges(
            current
        );

        while (visibleEdges.Count > 0) {
            foreach(HexEdge edge in visibleEdges) {
                if (!closed.Contains(edge.Target)) {
                    open.Enqueue(edge.Target);
                    visibleCells.Add(edge.Target);
                    closed.Add(current);
                }
            }

            current = open.Dequeue();
        }

        return visibleCells;

// USE OUT EDGES OF ADJACENCY GRAPH INSTEAD
// This method represents returning a breadth first list of the graph
// which terminates when an edge is encountered where the out edge
// target is higher in elevation than the out edge source.
/*        Queue<ElevationEdge> edgeQueue =
            (Queue<ElevationEdge>)graph.OutEdges(fromCell);

        List<HexCell> result = new List<HexCell>();
        result.Add(fromCell);

        while (edgeQueue.Count > 0) {
            ElevationEdge current = edgeQueue.Dequeue();

            if (
                current.Delta <= 0
            ) {
                result.Add(current.Target);
                
                foreach (
                    ElevationEdge edge in
                    (List<HexEdge>)graph.OutEdges(current.Target)
                ) {
                   edgeQueue.Enqueue(edge); 
                }
            }
        }

        return result;
*/
/*       
        List<HexCell> visibleCells = ListPool<HexCell>.Get();

        _searchFrontierPhase += 2;

        if (_searchFrontier == null)
        {
            _searchFrontier = new CellPriorityQueue();
        }
        else
        {
            _searchFrontier.Clear();
        }

        sightRange += fromCell.ViewElevation;

// Temporarily using a list instead of a priority queue.
// Should optimize this later.
//
        fromCell.SearchPhase = _searchFrontierPhase;
        fromCell.Distance = 0;
        _searchFrontier.Enqueue(fromCell);

        HexCoordinates fromCoordinates = fromCell.Coordinates;

        while (_searchFrontier.Count > 0)
        {
            HexCell current = _searchFrontier.Dequeue();
            current.SearchPhase += 1;

            visibleCells.Add(current);

            for (HexDirection direction = HexDirection.Northeast; direction <= HexDirection.Northwest; direction++)
            {
                HexCell neighbor = current.GetNeighbor(direction);

                if
                (
                    neighbor == null ||
                    neighbor.SearchPhase > _searchFrontierPhase ||
                    !neighbor.IsExplorable
                )
                {
                    continue;
                }
                
                int distance = current.Distance + 1;

                if 
                (
                    distance + neighbor.ViewElevation > sightRange ||
                    distance > fromCoordinates.DistanceTo(neighbor.Coordinates)
                )
                {
                    continue;
                }

                if (neighbor.SearchPhase < _searchFrontierPhase)
                {
                    neighbor.SearchPhase = _searchFrontierPhase;
                    neighbor.Distance = distance;
                    neighbor.SearchHeuristic = 0;
                    _searchFrontier.Enqueue(neighbor);
                }
                else if (distance < neighbor.Distance)
                {
                    int oldPriority = neighbor.SearchPriority;
                    neighbor.Distance = distance;
                    _searchFrontier.Change(neighbor, oldPriority);
                }
            }
        }

        return visibleCells;
        */
    }

    private Transform[] CreateColumns(
        int chunkColumns
    ) {
        Transform[] result = new Transform[chunkColumns];

        for (int column = 0; column < chunkColumns; column++) {
            GameObject columnObj = Instantiate(
                new GameObject("Column"),
                transform,
                false
            ) as GameObject;

            result[column] = columnObj.transform;
        }

        return result;
    }

    private HexGridChunk[] CreateChunks(
        Transform[] columns,
        int chunkRows,
        int chunkColumns
    ) {
        HexGridChunk[] result =
            new HexGridChunk[chunkRows * chunkColumns];

        for (int z = 0, i = 0; z < chunkColumns; z++) {
            for (int x = 0; x < chunkRows; x++) {
                HexGridChunk chunk = result[i++] =
                    HexGridChunk.CreateChunk();
                chunk.transform.SetParent(columns[x], false);
            }
        }

        return result;
    }

    private Vector3 CoordinateToLocalPosition(
        int x,
        int z,
        float innerDiameter,
        float cellOuterRadius
    ) {
        return new Vector3(
// The distance between the center of two hexagons on the x axis is equal to
// twice the inner radius of a given hexagon. Additionally, for half of the
// cells, the position on the z axis (cartesian y axis) is added to its position
// on the x axis as an offset, and the integer division of the position of
// the cell on the z axis is subtracted from that value. For even rows this
// negates the offset. For odd rows, the integer is rounded down and the offset
// is retained.
            (x + z * 0.5f - z / 2) * innerDiameter,
            0,
// The distance between the center of two hexagons on the z axis (cartesian y axis) is equal to
// one and one half the outer radius of a given hexagon.
            z * (cellOuterRadius * 1.5f)
        );
    }


/// <summary>
/// Create a Cell representing the data 
/// </summary>
/// <param name="x"></param>
/// <param name="z"></param>
/// <param name="i"></param>
/// <param name="cellOuterRadius"></param>
/// <param name="chunkSizeX"></param>
/// <returns></returns>    
    private HexCell CreateCell(
        int x,
        int z,
        int i,
        float cellOuterRadius,
        HexGrid<HexCell> hexGrid
    ) {
// metrics.
        float innerDiameter =
            HexagonPoint.GetOuterToInnerRadius(cellOuterRadius) * 2f;

// Create the HexCell's object and monobehaviour.
        HexCell result = HexCell.Instantiate();

// Set the HexCell's transform.
        result.transform.localPosition = CoordinateToLocalPosition(
            x,
            z,
            innerDiameter,
            cellOuterRadius
        );

// Set the HexCell's monobehaviour properties.
        result.HexCoordinates = CubeVector.FromAxialCoordinates(
            x,
            z,
            hexGrid.WrapSize
        );

        result.Index = i;
        result.ColumnIndex = x / MeshConstants.ChunkSizeX;
        result.ShaderData = _cellShaderData;

// If wrapping is enabled, cell is not explorable if the cell is on the
// top or bottom border.
        if (IsWrapping) {
            result.IsExplorable = z > 0 && z < Height - 1;
        }
// If wrapping is disabled, cell is not explorable if the cell is on
// any border.
        else {
            result.IsExplorable =
                x > 0 &&
                z > 0 &&
                x < Width - 1 &&
                z < Height - 1;
        }

// THIS IS NOW HANDLED BY MAPPING THE DENSEARRAY TO AN ADJACENCY GRAP
// 
// At the beginning of each row, x == 0. Therefore, if x is greater than
// 0, set the east/west connection of the cell between the current cell
// and the previous cell in the array.
//        if (x > 0) {
//            result.SetNeighborPair(HexDirection.West, result[i - 1]);
//
//            if (_wrapping && x == _cellCountX - 1) {
//                result.SetNeighborPair(HexDirection.East, result[i - x]);
//            }
//        }
//        
// At the first row, z == 0. The first row has no southern neighbors. Therefore
//
//        if (z > 0)
//        {
// Use the bitwise operator to mask off all but the first bit. If the result is 0,
// the number is even:
//      11 (3) & 1(1) == 1
//       ^
//       |
//       AND only compares the length of the smallest binary sequence
//       |
//      10 (2) & 1(1) == 0
//      
// Because all  cells in even rows have a southeast neighbor, they can be connected.
//
//            if ((z & 1) == 0)
//            {
//                result.SetNeighborPair(HexDirection.SouthEast, result[i - _cellCountX]);
//
//                //All even cells except for the first cell in each row have a southwest neighbor.
//                if (x > 0)
//                {
//                    result.SetNeighborPair(HexDirection.SouthWest, result[i - _cellCountX - 1]);
//                }
//                else if (_wrapping)
//                {
//                    result.SetNeighborPair(HexDireFtion.SouthWest, result[i - 1]);
//                }
//            }
//            else
//            {
//                result.SetNeighborPair(HexDirection.SouthWest, result[i - _cellCountX]);
//
//                //All odd cells except the last cell in each row have a southeast neighbor
//                if (x < _cellCountX - 1)
//                {
//                    result.SetNeighborPair(HexDirection.SouthEast, result[i - _cellCountX + 1]);
//                }
//                else if (_wrapping)
//                {
//                    result.SetNeighborPair(HexDirection.SouthEast, result[i - _cellCountX * 2 + 1]);
//                }
//            }
//        }

// TODO: Presentation considerations should be moved to a separate class.
        Text label = new GameObject().AddComponent<Text>();
        
        label.rectTransform.anchoredPosition =
            new Vector2(
                result.transform.localPosition.x,
                result.transform.localPosition.z
            );

        result.uiRect = label.rectTransform;
        result.SetElevation(
            0,
            cellOuterRadius,
            hexGrid.WrapSize
        );
        
        return result;
    }

    private void AddCellToChunk(
        int localX,
        int localZ,
        HexCell cell,
        HexGridChunk chunk
    ) {
        chunk.AddCell(
            localX + localZ * MeshConstants.ChunkSizeX, cell
        );
    }

    private void Awake() {
//        ResetVisibility();

        // TODO: Is there a more transparent way to represent this dependency?
//       Right now it is buried in awake which makes it very hard to
//       tell that ShaderData depends on this class. Also, this
//       dependency is circular.
        _cellShaderData = gameObject.AddComponent<CellShaderData>();
        _cellShaderData.HexMap = this;

        Chunks = new HexGridChunk[0];
    
// TODO: This is a presentation concern and should not be in this class.
        _cellLabelPrefab = Resources.Load<Text>("Hex Cell Label");
    }
}

