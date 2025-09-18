using UnityEngine;

public enum Direction { Top, Bottom, Left, Right }

public class Doorway : MonoBehaviour
{
    public Direction direction;
    public bool isConnected = false;

    public Direction GetOppositeDirection()
    {
        return direction switch
        {
            Direction.Top => Direction.Bottom,
            Direction.Bottom => Direction.Top,
            Direction.Left => Direction.Right,
            Direction.Right => Direction.Left,
            _ => Direction.Top,
        };
    }
}
