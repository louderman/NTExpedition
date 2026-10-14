using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// AI-assisted bounded demo: click a nearby ship console, or walk into the arena return zone.
[RequireComponent(typeof(Collider2D))]
public class ScenePortal : MonoBehaviour
{
    [SerializeField] private string destinationScene;
    [SerializeField] private bool enterToTravel;
    [SerializeField] private PlayerMovement player;
    [SerializeField] private float interactionDistance = 2f;
    private Collider2D portalCollider;
    private bool travelling;

    void Awake()
    {
        portalCollider = GetComponent<Collider2D>();
    }

    void Update()
    {
        if (enterToTravel || travelling || player == null || player.combatMode) return;
        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;
        if (Camera.main == null) return;
        // Pointer pixels must be converted before testing against a world collider.
        Vector2 point = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        if (portalCollider.OverlapPoint(point) &&
            Vector2.Distance(player.transform.position, transform.position) <= interactionDistance)
            Travel();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // Compare the actual player's physics root; enemies cannot activate this portal.
        if (!enterToTravel || player == null || other.attachedRigidbody == null) return;
        if (other.attachedRigidbody.gameObject == player.gameObject) Travel();
    }

    public void Travel()
    {
        if (travelling) return; // Multiple overlapping colliders must not load twice.
        if (!Application.CanStreamedLevelBeLoaded(destinationScene))
        {
            Debug.LogError($"Scene is not enabled in Build Settings: {destinationScene}", this);
            return;
        }
        travelling = true;
        Time.timeScale = 1f;
        SceneManager.LoadSceneAsync(destinationScene, LoadSceneMode.Single);
    }
}
