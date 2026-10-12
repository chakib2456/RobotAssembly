using UnityEngine;
using System.Collections;

/// <summary>
/// Handles robot movement, turning, box pickup, and box placement on the grid.
/// </summary>
public class RobotMovement : MonoBehaviour
{
    /// <summary>
    /// Current logical position of the robot in grid coordinates.
    /// </summary>
    public Vector2Int gridPosition;

    /// <summary>
    /// True while the robot is actively moving through a smooth animation.
    /// </summary>
    public bool isMoving = false;
    public bool isTurningLeft = false;
    public bool isTurningRight = false;
    public bool IsMoving => isMoving;

    /// <summary>
    /// Reference to the grid that defines valid tile positions.
    /// </summary>
    public GridManager gridManager;

    /// <summary>
    /// Indicates whether the robot currently carries a box.
    /// </summary>
    public bool isCarryingBox = false;
    public Animator animator;
    public float moveDuration = 0.3f;

    public float heightOffset = 0.5f;

    [Header("Box Handling")]
    public GameObject carriedBox;

    /// <summary>
    /// Finds the grid manager automatically when the object starts.
    /// </summary>
    void Start()
    {
        animator = GetComponentInChildren<Animator>();
        if (gridManager == null)
        {
            GameObject gridManagerObject =
                GameObject.FindGameObjectWithTag("GridManager");
            if (gridManagerObject != null)
            {
                gridManager = gridManagerObject.GetComponent<GridManager>();
            }
        }
    }

    /// <summary>
    /// Attempts to move the robot one cell forward if the destination is valid.
    /// </summary>
    public void MoveRobot()
    {
        if (gridManager == null)
        {
            Debug.LogError("GridManager is not assigned!");
            return;
        }

        Vector3 forward = transform.forward;

        Vector2Int gridDirection = new Vector2Int(
            Mathf.RoundToInt(forward.x),
            Mathf.RoundToInt(forward.z)
        );

        Vector2Int targetPosition = gridPosition + gridDirection;

        // Check for walls.
        if (gridManager.IsCellBlocked(targetPosition))
        {
            Debug.Log("Movement blocked by a wall!");
            return;
        }

        // Check for boxes.
        if (FindBoxAtPosition(targetPosition) != null)
        {
            Debug.Log("Movement blocked by a box!");
            return;
        }

        gridPosition = targetPosition;
        Vector3 targetWorldPosition =
            gridManager.GetWorldPosition(gridPosition);

        targetWorldPosition.y += heightOffset;

        transform.position = targetWorldPosition;
    }

    /// <summary>
    /// Smoothly moves the robot to a target grid position over time.
    /// </summary>
    IEnumerator MoveToPosition(Vector2Int targetPosition)
    {
        isMoving = true;

        Vector3 startPosition = transform.position;

        Vector3 targetWorldPosition =
            gridManager.GetWorldPosition(targetPosition);

        targetWorldPosition.y += heightOffset;

        float elapsed = 0f;

        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;

            float progress = Mathf.Clamp01(elapsed / moveDuration);

            transform.position = Vector3.Lerp(
                startPosition,
                targetWorldPosition,
                progress
            );

            yield return null;
        }

        transform.position = targetWorldPosition;
        gridPosition = targetPosition;

        isMoving = false;
    }

    /// <summary>
    /// Finds a box occupying the given grid cell, excluding the one currently being carried.
    /// </summary>
    BoxObject FindBoxAtPosition(Vector2Int position)
    {
        BoxObject[] boxes = FindObjectsByType<BoxObject>(
            FindObjectsSortMode.None
        );

        foreach (BoxObject box in boxes)
        {
            if (box == null || box.gameObject == carriedBox)
                continue;

            if (gridManager.GetGridPosition(box.transform.position)
                == position)
            {
                return box;
            }
        }

        return null;
    }

    /// <summary>
    /// Rotates the robot 90 degrees to the right.
    /// </summary>
    public void TurnRight()
    {
        transform.Rotate(0, 90, 0);
    }

    /// <summary>
    /// Rotates the robot 90 degrees to the left.
    /// </summary>
    public void TurnLeft()
    {
        transform.Rotate(0, -90, 0);
    }

    /// <summary>
    /// Returns true when the robot is currently holding a box.
    /// </summary>
    public bool IsCarryingBox()
    {
        return carriedBox != null;
    }

    /// <summary>
    /// Picks up a box that is directly in front of the robot.
    /// </summary>
    public void PickUpBox()
    {
        if (carriedBox != null)
        {
            Debug.Log("Robot is already carrying a box!");
            return;
        }

        Vector2Int frontPosition = GetFrontPosition();
        BoxObject box = FindBoxAtPosition(frontPosition);

        if (box == null)
        {
            Debug.Log("No box in front of the robot!");
            return;
        }

        isCarryingBox = true;
        animator.SetBool("PickUp", isCarryingBox);
        carriedBox = box.gameObject;
        carriedBox.transform.SetParent(transform);
        carriedBox.transform.localPosition = new Vector3(0f, 1f, 1f);

        Debug.Log("Box picked up!");
    }

    /// <summary>
    /// Drops the carried box in front of the robot if the destination is valid.
    /// </summary>
    public void DropBox()
    {
        if (carriedBox == null)
        {
            Debug.Log("Robot is not carrying a box!");
            return;
        }

        Vector2Int frontPosition = GetFrontPosition();

        // Don't drop into a wall or an occupied cell.
        if (gridManager.IsCellBlocked(frontPosition) ||
            FindBoxAtPosition(frontPosition) != null)
        {
            Debug.Log("Cannot drop the box there!");
            return;
        }

        carriedBox.transform.SetParent(null);
        Vector3 dropPosition = gridManager.GetWorldPosition(frontPosition);
        dropPosition.y += 0.5f; // Raise the box above the floor

        carriedBox.transform.position = dropPosition;

        isCarryingBox = false;
        animator.SetBool("PickUp", isCarryingBox);
        carriedBox = null;

        Debug.Log("Box dropped in front of the robot!");
    }

    /// <summary>
    /// Gets the grid cell directly in front of the robot according to its facing direction.
    /// </summary>
    Vector2Int GetFrontPosition()
    {
        Vector3 forward = transform.forward;

        Vector2Int direction = new Vector2Int(
            Mathf.RoundToInt(forward.x),
            Mathf.RoundToInt(forward.z)
        );

        return gridPosition + direction;
    }
}