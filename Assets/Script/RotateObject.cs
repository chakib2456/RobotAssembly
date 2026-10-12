using UnityEngine;

/// <summary>
/// Rotates a decorative object continuously while the scene is playing.
/// </summary>
public class RotateObject : MonoBehaviour
{
    /// <summary>
    /// Degrees per second used to rotate the object.
    /// </summary>
    public Vector3 rotationSpeed = new Vector3(0f, 90f, 0f);

    /// <summary>
    /// Rotates the object every frame based on the configured speed.
    /// </summary>
    void Update()
    {
        transform.Rotate(rotationSpeed * Time.deltaTime, Space.Self);
    }
}