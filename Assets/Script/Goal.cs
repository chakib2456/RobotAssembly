
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
/// <summary>
/// Represents the end goal for the current level. Once the fire reaches this object, the next scene loads.
/// </summary>
public class Goal : MonoBehaviour
{
    /// <summary>
    /// Logical position of the goal in the level grid.
    /// </summary>
    public Vector2Int gridPosition;

    /// <summary>
    /// Loads the next scene when the fire object enters the goal trigger.
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Fire"))
        {
            StartCoroutine(LoadNextScene());
            Debug.Log("LEVEL COMPLETE!");
        }
    }

     IEnumerator LoadNextScene()
    {
        yield return new WaitForSeconds(1f);

        SceneManager.LoadScene(
            SceneManager.GetActiveScene().buildIndex + 1
        );
    }
}
