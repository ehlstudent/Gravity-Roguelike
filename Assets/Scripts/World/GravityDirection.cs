namespace GravityRoguelike.World
{
    /// <summary>Cardinal gravity directions in grid axes, independent of camera rotation.</summary>
    public enum GravityDirection
    {
        /// <summary>Negative Y: (0, -1).</summary>
        Down = 0,
        /// <summary>Positive Y: (0, 1).</summary>
        Up = 1,
        /// <summary>Negative X: (-1, 0).</summary>
        Left = 2,
        /// <summary>Positive X: (1, 0).</summary>
        Right = 3
    }
}
