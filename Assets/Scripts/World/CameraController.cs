using UnityEngine;

public class CameraController : MonoBehaviour
{
    [SerializeField] private GameObject protagEntity;
    private Vector3 startPosition;

    void Awake()
    {
        // Captured once so a restart can return the camera to its authored position (handout:
        // CameraController.GameRestart registered on the OnGameRestart event).
        startPosition = transform.position;
    }

    void LateUpdate()
    {
        if (protagEntity == null) return;
        transform.position = protagEntity.transform.position + new Vector3(0f, 0f, -10f);
    }

    // Response for the OnGameRestart SimpleGameEventListener on this object.
    public void GameRestart()
    {
        transform.position = startPosition;
    }
}
