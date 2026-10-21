using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Collider2D))]
public class SupplyStation : MonoBehaviour
{
    public PowerupPickup pickupPrefab;
    public PlayerMovement player;
    public TMP_Text label;
    public bool Dispensed { get; private set; }

    void Update()
    {
        if (Dispensed || player == null || Time.timeScale == 0f) return;
        if (Vector2.Distance(player.transform.position, transform.position) > 1.8f) return;
        bool useKey = Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
        bool clicked = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame &&
            !player.combatMode && Camera.main != null && GetComponent<Collider2D>().OverlapPoint(
                Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue()));
        if (useKey || clicked) Dispense();
    }

    public PowerupPickup Dispense()
    {
        if (Dispensed || pickupPrefab == null || Time.timeScale == 0f) return null;
        Dispensed = true;
        if (label != null) label.text = "Dispensed";
        var pickup = Instantiate(pickupPrefab, transform.position, Quaternion.identity);
        var flow = FindAnyObjectByType<ExpeditionFlow>();
        if (flow != null) flow.PlayEffect(pickup.config.spawnSound);
        return pickup;
    }
}
