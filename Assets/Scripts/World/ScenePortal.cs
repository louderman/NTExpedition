using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// AI-assisted bounded demo: click a nearby ship console, or walk into the arena return zone.
[RequireComponent(typeof(Collider2D))]
public class ScenePortal : MonoBehaviour
{
    [SerializeField] private string destinationScene;
    /// <summary>
    /// Dan's rule: two or more names here = the console rolls a fresh coin flip on every launch, so the
    /// pick must happen at the moment of travel, never be resolved once and cached. Empty list leaves
    /// the fixed destinationScene in charge (DebugArena stays the fallback / test entry).
    /// </summary>
    [SerializeField] private string[] randomDestinations;
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

    /// <summary>
    /// Resolves the destination for THIS trip. Called from Travel(), so with two entries it is a
    /// UnityEngine.Random.Range(0, 2) coin flip per launch instead of a choice made once per session.
    /// </summary>
    public string PickDestination()
    {
        if (randomDestinations != null && randomDestinations.Length > 0)
            return randomDestinations[UnityEngine.Random.Range(0, randomDestinations.Length)];
        return destinationScene;
    }

    public void Travel()
    {
        if (travelling || Time.timeScale == 0f) return; // Multiple overlapping colliders must not load twice.
        string destination = PickDestination();
        Debug.Log($"ScenePortal {name}: travelling to {destination}", this);
        if (!Application.CanStreamedLevelBeLoaded(destination))
        {
            Debug.LogError($"Scene is not enabled in Build Settings: {destination}", this);
            return;
        }
        travelling = true;
        Time.timeScale = 1f;
        // Dan's rule: held + stowed items travel with the player; snapshot the loadout while this scene is alive.
        var hands = player != null ? player.GetComponent<HandSystem>() : null;
        PlayerLoadoutCarry.Capture(hands, FindFirstObjectByType<InventoryContainer>());
        // An item being dragged is neither held nor stowed, so it needs its own snapshot before the swap.
        PlayerLoadoutCarry.CaptureDragged(player != null ? player.GetComponent<ItemDragSystem>() : null);
        SceneManager.LoadSceneAsync(destination, LoadSceneMode.Single);
    }
}
