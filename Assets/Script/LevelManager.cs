using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Builds the current puzzle board from a text-based level definition and spawns the gameplay objects it describes.
/// </summary>
public class LevelManager : MonoBehaviour
{
    /// <summary>
    /// Shared grid controller used to map world positions and blocked cells.
    /// </summary>
    public GridManager gridManager;

    public GameObject floorPrefab;
    public GameObject robotPrefab;
    public GameObject wallPrefab;
    public GameObject goalPrefab;
    public GameObject doorPrefab;
    public GameObject boxPrefab;
    public GameObject firePrefab;
    public GameObject triggerPrefab;
    public TextAsset levelFile;

   
    [Header("Floor Optimization")]
    public Transform floorParent;

    private List<GameObject> floorTiles = new List<GameObject>();

    /// <summary>
    /// Loads the active level as soon as the scene starts.
    /// </summary>
    void Start()
    {
        LoadLevel();
    }

    /// <summary>
    /// Reads the map layout and instantiates all tiles and interactive objects.
    /// </summary>
    void LoadLevel()
    {
          if (gridManager == null)
            {
                Debug.LogError("LevelManager: GridManager is not assigned!");
                return;
            }

        string[] levelLayout = levelFile.text.Split(
            new[] { '\r', '\n' },
            System.StringSplitOptions.RemoveEmptyEntries
        );


        if (!ValidateLevel(levelLayout))
        return;

        for (int y = 0; y < levelLayout.Length; y++)
        {
            for (int x = 0; x < levelLayout[y].Length; x++)
            {
                char tile = levelLayout[y][x];

                Vector2Int gridPosition = new Vector2Int(x, y);

                // Every cell gets a floor
                SpawnFloor(gridPosition);

                if (tile == '#') SpawnWall(gridPosition, 0f);

                else if (tile == '-') SpawnWall(gridPosition, 90f);

                else if (tile == '|') SpawnWall(gridPosition, 180f);

                else if (tile == '+') SpawnWall(gridPosition, 270f);
                
                else if (tile == 'D')
                {
                    SpawnDoor(gridPosition);
                }
                else if (tile == 'T')
                {
                    SpawnTrigger(gridPosition);
                }
                else if (tile == 'R')
                {
                    SpawnRobot(gridPosition);
                }
                else if (tile == 'G')
                {
                    SpawnGoal(gridPosition);
                }
                else if (tile == 'B')
                {
                    SpawnBox(gridPosition);
                }
                else if (tile == 'F')
                {
                    SpawnFire(gridPosition);
                }
            }
        }
        CombineFloorMeshes();
    }

    /// <summary>
    /// Spawns one floor tile for the specified grid coordinate.
    /// </summary>
    void SpawnFloor(Vector2Int gridPosition)
    {
        Vector3 worldPosition =
            gridManager.GetWorldPosition(gridPosition);

        GameObject floor = Instantiate(
            floorPrefab,
            worldPosition,
            Quaternion.identity,
            floorParent
        );

        floorTiles.Add(floor);
    }

    /// <summary>
    /// Creates a wall piece and locks that cell so the robot cannot pass through it.
    /// </summary>
    void SpawnWall(Vector2Int gridPosition, float rotationY)
    {
        Vector3 worldPosition =
            gridManager.GetWorldPosition(gridPosition);
        worldPosition.y += 1f; // Raise the wall slightly above the floor

        Quaternion rotation = Quaternion.Euler(0f, rotationY, 0f);

        GameObject wall = Instantiate(
            wallPrefab,
            worldPosition,
            rotation
        );

        gridManager.SetCellBlocked(gridPosition);
    }

    /// <summary>
    /// Spawns a door tile and marks its grid space as inaccessible.
    /// </summary>
    void SpawnDoor(Vector2Int gridPosition)
    {
        Vector3 worldPosition =
            gridManager.GetWorldPosition(gridPosition);

        Instantiate(
            doorPrefab,
            worldPosition,
            Quaternion.identity
        );

        // Register this cell as blocked.
        gridManager.SetCellBlocked(gridPosition);
    }

    /// <summary>
    /// Creates a trigger object used to detect when a box enters the nearby door area.
    /// </summary>
    void SpawnTrigger(Vector2Int gridPosition)
    {
        Vector3 worldPosition =
            gridManager.GetWorldPosition(gridPosition);

        Instantiate(
            triggerPrefab,
            worldPosition,
            Quaternion.identity
        );
    }

    /// <summary>
    /// Spawns the robot and attaches the grid reference and instruction manager.
    /// </summary>
    void SpawnRobot(Vector2Int gridPosition)
    {
        Vector3 worldPosition =
            gridManager.GetWorldPosition(gridPosition);

        worldPosition.y += 0.5f; // Spawn robot slightly higher

        GameObject robotObject = Instantiate(
            robotPrefab,
            worldPosition,
            Quaternion.identity
        );

        RobotMovement movement =
            robotObject.GetComponent<RobotMovement>();

        if (movement != null)
        {
            movement.gridManager = gridManager;
            movement.gridPosition = gridPosition;
        }

        InstructionManager instructionManager =
            FindFirstObjectByType<InstructionManager>();

        if (instructionManager != null && movement != null)
        {
            instructionManager.robot = movement;
        }
    }

    /// <summary>
    /// Instantiates the goal object for the level.
    /// </summary>
    void SpawnGoal(Vector2Int gridPosition)
    {
        Vector3 worldPosition =
            gridManager.GetWorldPosition(gridPosition);

        GameObject goalObject = Instantiate(
            goalPrefab,
            worldPosition,
            Quaternion.identity
        );

        Goal goal = goalObject.GetComponent<Goal>();

        if (goal != null)
        {
            goal.gridPosition = gridPosition;
        }
    }

    /// <summary>
    /// Spawns a box item at the provided grid coordinate.
    /// </summary>
    void SpawnBox(Vector2Int gridPosition)
    {
        Vector3 worldPosition =
            gridManager.GetWorldPosition(gridPosition);
        worldPosition.y += 0.5f;

        Instantiate(
            boxPrefab,
            worldPosition,
            Quaternion.identity
        );
    }

    /// <summary>
    /// Spawns the fire hazard for the current map.
    /// </summary>
    void SpawnFire(Vector2Int gridPosition)
    {
        Vector3 worldPosition =
            gridManager.GetWorldPosition(gridPosition);
        worldPosition.y += 0.5f;

        Instantiate(
            firePrefab,
            worldPosition,
            Quaternion.identity
        );
    }

    /// <summary>
    /// Combines all floor tiles into one mesh for performance.
    /// </summary>
    void CombineFloorMeshes()
    {
        MeshFilter[] meshFilters =
            floorParent.GetComponentsInChildren<MeshFilter>();

        List<CombineInstance> combineInstances =
            new List<CombineInstance>();

        foreach (MeshFilter meshFilter in meshFilters)
        {
            if (meshFilter.transform == floorParent)
                continue;

            Mesh mesh = meshFilter.sharedMesh;

            if (mesh == null)
                continue;

            CombineInstance combine = new CombineInstance();
            combine.mesh = mesh;
            combine.transform =
                floorParent.worldToLocalMatrix *
                meshFilter.transform.localToWorldMatrix;

            combineInstances.Add(combine);
        }

        if (combineInstances.Count == 0)
            return;

        Mesh combinedMesh = new Mesh();
        combinedMesh.indexFormat =
            UnityEngine.Rendering.IndexFormat.UInt32;

        combinedMesh.CombineMeshes(combineInstances.ToArray());

        MeshFilter parentMeshFilter =
            floorParent.GetComponent<MeshFilter>();

        if (parentMeshFilter == null)
            parentMeshFilter =
                floorParent.gameObject.AddComponent<MeshFilter>();

        MeshRenderer parentRenderer =
            floorParent.GetComponent<MeshRenderer>();

        if (parentRenderer == null)
            parentRenderer =
                floorParent.gameObject.AddComponent<MeshRenderer>();
        
        if (floorTiles.Count == 0)
        {
            Debug.LogWarning("No floor tiles were spawned.");
            return;
        }
        // Use the floor material.
        MeshRenderer firstRenderer =
            floorTiles[0].GetComponent<MeshRenderer>();

        if (firstRenderer != null)
            parentRenderer.sharedMaterials = firstRenderer.sharedMaterials;

        parentMeshFilter.sharedMesh = combinedMesh;

        // Disable the original tiles after combining.
        foreach (GameObject tile in floorTiles)
            tile.SetActive(false);
    }




    /// <summary>
    /// Checks that the level layout contains valid characters and the required unique objects.
    /// </summary>
    bool ValidateLevel(string[] layout)
    {
        if (layout.Length == 0)
        {
            Debug.LogError("Level file is empty!");
            return false;
        }

        int expectedWidth = layout[0].Length;
        int robotCount = 0;
        int goalCount = 0;

        if (expectedWidth == 0)
        {
            Debug.LogError("Level contains an empty row!");
            return false;
        }

        for (int y = 0; y < layout.Length; y++)
        {
            if (layout[y].Length != expectedWidth)
            {
                Debug.LogError(
                    $"Invalid level: row {y + 1} has " +
                    $"{layout[y].Length} tiles, but expected " +
                    $"{expectedWidth}."
                );

                return false;
            }

            for (int x = 0; x < layout[y].Length; x++)
            {
                char tile = layout[y][x];

                switch (tile)
                {
                    case '#': // Wall: 0 degrees
                    case '-': // Wall: 90 degrees
                    case '|': // Wall: 180 degrees
                    case '+': // Wall: 270 degrees
                    case '.': // Empty floor
                        break;

                    case 'B': // Box
                        break;
                    case 'F': // Fire
                        break;

                    case 'D': // Door
                    case 'T': // Trigger
                        break;

                    case 'R': // Robot
                        robotCount++;
                        break;

                    case 'G': // Goal
                        goalCount++;
                        break;

                    default:
                        Debug.LogError(
                            $"Invalid character '{tile}' " +
                            $"at row {y + 1}, column {x + 1}."
                        );

                        return false;
                }
            }
        }

        if (robotCount != 1)
        {
            Debug.LogError(
                $"Level must contain exactly one robot (R). " +
                $"Found: {robotCount}."
            );

            return false;
        }

        if (goalCount < 1)
        {
            Debug.LogError(
                "Level must contain at least one goal (G)."
            );

            return false;
        }

        return true;
    }
}