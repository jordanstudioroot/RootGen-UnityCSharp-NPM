using UnityEngine;

public static class MeshConstants {
/// <summary>
/// The size of a mesh chunk along the x axis in offset coordinates.
/// </summary>
    public const int ChunkSizeX = 5;

/// <summary>
/// The size of a mesh chunk along the z axis in offset coordinates.
/// </summary>
    public const int ChunkSizeZ = 5;

    public const int DefaulthexOuterRadius = 10;

    public static readonly Vector2 ChunkSize =
        new Vector2(
            ChunkSizeX,
            ChunkSizeZ
        );
}