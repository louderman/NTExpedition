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
        // The ship console rides the persistent ship (HomeShipStaging), so it survives the load it triggers: the
        // one-shot `travelling` guard would otherwise stay set for the rest of the session and the console would
        // be dead in every map the player arrives in. Clear it once the destination scene is up.
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        travelling = false;
    }

    void Update()
    {
        // The ship console rides the persistent ship (HomeShipStaging): after a scene hop its authored `player`
        // reference is the player of the unloaded scene, so re-resolve it before the null guard below.
        if (player == null) player = FindFirstObjectByType<PlayerMovement>();

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
        if (!enterToTravel || other.attachedRigidbody == null) return;
        if (player == null) player = FindFirstObjectByType<PlayerMovement>();
        if (player == null) return;
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
        // The ship's own storage crate travels with the ship, so it is deliberately not the container captured
        // here (HomeShipStaging.SceneStorageContainer skips aboard containers; it falls back to the plain scene
        // lookup when there is no persistent ship).
        var hands = player != null ? player.GetComponent<HandSystem>() : null;
        PlayerLoadoutCarry.Capture(hands, HomeShipStaging.SceneStorageContainer());
        // An item being dragged is neither held nor stowed, so it needs its own snapshot before the swap.
        PlayerLoadoutCarry.CaptureDragged(player != null ? player.GetComponent<ItemDragSystem>() : null);
        SceneManager.LoadSceneAsync(destination, LoadSceneMode.Single);
    }
}
