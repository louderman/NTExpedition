using UnityEngine;

/// <summary>
/// A world fixture the drag system can pull around that is NOT an item. The normal drag target is an
/// ItemComponent, which is what gives an item its hand, container, throw and portal-carry behaviour; this
/// interface exists for objects like the authored StorageCrate that are bolted into the scene and must
/// never be picked up, stored, thrown or carried through a portal.
///
/// Implementing this adds no acquisition path: nothing in HandSystem, InventoryContainer or
/// PlayerLoadoutCarry looks at this interface. A fixture only ever gains the drag leash, the pull and the
/// collision sweep. Eligibility to START a drag is separate from eligibility to be picked up.
/// </summary>
public interface IDragTarget
{
    /// <summary>World transform the drag leash and the reach test are measured from.</summary>
    Transform DragTransform { get; }

    /// <summary>Collider used for the click hit test (and for the movement sweep radius).</summary>
    Collider2D DragCollider { get; }

    /// <summary>Name used in the [Drag] log lines and in the portal release message.</summary>
    string DragLabel { get; }

    /// <summary>False when the fixture refuses to be taken hold of right now.</summary>
    bool CanStartDrag { get; }

    /// <summary>False when an in-progress drag of this fixture has to end.</summary>
    bool CanKeepDrag { get; }

    /// <summary>
    /// Sprite the hand HUD shows as the drag indicator on the hand this fixture occupies, so the occupied
    /// hand is recognisable at a glance and not only readable as a label. Null is legal: a fixture that has
    /// no art of its own keeps the text-only feedback it had before.
    /// </summary>
    Sprite DragIcon { get; }
}
