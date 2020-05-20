using UnityEngine;

public struct CellData {
    public CubeVector Coordinates { get; set; }
    public int Index { get; set; }
    public int ColumnIndex { get; set; }
    public CellShaderData CellShaderData { get; set; }
    public bool IsExplorable { get; set; }
    public Vector3 WorldPosition { get; set; }
    public HexMeshChunk HexGridChunk { get; set; }
    
}
