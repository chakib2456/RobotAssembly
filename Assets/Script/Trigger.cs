using UnityEngine;

public class Trigger : MonoBehaviour
{
    public GameObject doorToOpen;
    public GridManager gridManager;

    void Start()
    {
        if (doorToOpen == null)
            doorToOpen = GameObject.FindGameObjectWithTag("Door");

        if (gridManager == null)
        {
            GameObject gridObject =
                GameObject.FindGameObjectWithTag("GridManager");

            if (gridObject != null)
                gridManager = gridObject.GetComponent<GridManager>();
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Box") && doorToOpen != null)
        {
            Debug.Log("Box entered the trigger area.");

            // Find the door's grid cell before destroying it.
            Vector2Int doorPosition =
                gridManager.GetGridPosition(doorToOpen.transform.position);

            // Update the grid so the robot can enter this cell.
            
            gridManager.UnblockCell(doorPosition);
            Destroy(doorToOpen);
            doorToOpen = null;
        }
    }
}