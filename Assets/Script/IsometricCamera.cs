using System.Collections;
using System.Collections.Generic;
using UnityEngine; 

public class IsometricCamera : MonoBehaviour
{
    public Transform player; // Reference to the player object
    public float distance = 10f; // Distance from the player
    public float height = 10f; // Height above the player

    [Range(0, 90)]
    public float tiltAngle = 45f; // Controls the downward tilt of the camera (X-axis)

    [Range(0, 360)]
    public float rotationAroundPlayer = 45f; // Controls the camera rotation around the player (Y-axis)

    public float fov = 60f; // Field of View of the camera

    private Camera cam; // Reference to the Camera component

    private void Start()
    {
        cam = GetComponent<Camera>(); // Get the Camera component on this GameObject
    }

    private void LateUpdate()
    {
        // If no player is assigned, exit function
        if (player == null) return;

        // Set the camera's field of view (FOV)
        cam.fieldOfView = fov;

        // Calculate the camera's rotation based on tilt and rotation
        Quaternion rotation = Quaternion.Euler(tiltAngle, rotationAroundPlayer, 0);

        // Determine the offset position based on rotation, height, and distance
        Vector3 offset = rotation * new Vector3(0, height, -distance);

        // Set the camera position relative to the player
        transform.position = player.position + offset;

        // Ensure the camera always looks at the player
        transform.LookAt(player.position);
    }
}

