using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Proximity-operated airlock: opens when a tracked sensor (normally the player)
/// walks into the doorway aperture from <em>either</em> side, stays open while the
/// sensor is inside the aperture or the wider hold radius, and closes after a
/// short dwell once the aperture is clear.
/// </summary>
/// <remarks>
/// Deliberately minimal, in the spirit of the rest of this project: no power,
/// atmospherics, access or docking simulation. It only plays the upstream SS14
/// airlock sprite states (opening / open / closing / closed) that were imported
/// from the matching RSI, using the upstream per-frame delays.
///
/// Wiring: this component sits on the door's own tile GameObject (the parent of
/// the panel renderer child). <see cref="panel"/> is that child's SpriteRenderer;
/// sprite swapping never touches its transform, so rotation, sorting order and
/// the child's art layers are preserved exactly as the layout baked them.
///
/// This component intentionally adds no collider. The ship's doors are art-only
/// like the rest of the shuttle, and the player is a physics body with no
/// obstruction anywhere else on the ship; adding a collider here alone would trap
/// the player in a compartment rather than add gameplay. See the task report for
/// the trade-off and how to enable blocking if it is ever wanted
/// (<see cref="closedCollider"/> is prepared for that but left disabled).
/// </remarks>
[DisallowMultipleComponent]
public class ProximityAirlock : MonoBehaviour
{
    public enum DoorState
    {
        Closed,
        Opening,
        Open,
        Closing,
    }

    [Header("Panel (child SpriteRenderer that shows the door art)")]
    public SpriteRenderer panel;

    [Header("Upstream RSI states (frames in RSI order)")]
    public Sprite[] openingFrames = new Sprite[0];
    public Sprite openSprite;
    public Sprite[] closingFrames = new Sprite[0];
    public Sprite closedSprite;

    [Header("Upstream per-frame delays in seconds (RSI nextFrameDelay)")]
    public float[] openingDelays = { 0.1f, 0.1f, 0.07f, 0.07f, 0.07f, 0.2f };
    public float[] closingDelays = { 0.1f, 0.1f, 0.07f, 0.07f, 0.07f, 0.2f };

    [Header("Proximity thresholds (world units; 1 unit = 1 ship tile)")]
    [Tooltip("Open once a sensor is within this distance of the door along the passage axis. " +
             "One tile (1.0) is the HomeShip interior requirement: never start opening earlier than " +
             "one tile from the doorway.")]
    public float openDistance = 1.0f;

    [Tooltip("Only run the close timer once every sensor is beyond this distance (hysteresis vs openDistance).")]
    public float holdDistance = 2.25f;

    [Tooltip("Half-width of the doorway aperture along the wall axis; sensors further out than this do not count.")]
    public float apertureHalfWidth = 0.8f;

    [Tooltip("Seconds the aperture must stay clear before the door starts closing.")]
    public float closeDelay = 0.5f;

    [Header("Doorway orientation")]
    [Tooltip("True when the passage through this door runs along the GameObject's local Y (both interior airlocks).")]
    public bool passageAlongLocalY = true;

    [Header("Sensors (empty = auto-detect PlayerMovement)")]
    public Transform[] sensors = new Transform[0];

    [Header("Diagnostics")]
    [Tooltip("Log every state transition to the Unity console.")]
    public bool logTransitions;

    /// <summary>Collider enabled while the door is not fully open. Left disabled by default.</summary>
    [HideInInspector]
    public Collider2D closedCollider;

    // ---- observable state, used by the verification harness -------------------

    public DoorState State { get; private set; } = DoorState.Closed;

    /// <summary>Distance along the passage axis to the closest sensor inside the aperture.</summary>
    public float NearestApertureDistance { get; private set; } = float.PositiveInfinity;

    /// <summary>Distance along the passage axis to the closest sensor inside the padded hold window.</summary>
    public float NearestHoldDistance { get; private set; } = float.PositiveInfinity;

    /// <summary>Current sprite name, handy for logs and evidence capture.</summary>
    public string CurrentSpriteName => panel != null && panel.sprite != null ? panel.sprite.name : "(none)";

    /// <summary>Raised on every state change: (door, previous, next).</summary>
    public event Action<ProximityAirlock, DoorState, DoorState> StateChanged;

    private int _frameIndex;
    private float _frameTimer;
    private float _closeTimer;

    private void Awake()
    {
        if (panel == null)
        {
            panel = GetComponentInChildren<SpriteRenderer>(true);
        }

        if (State == DoorState.Closed && panel != null && closedSprite != null)
        {
            panel.sprite = closedSprite;
        }

        RefreshSensors();
    }

    private void Update()
    {
        Tick(Time.deltaTime);
    }

    /// <summary>
    /// Advances the door by <paramref name="dt"/> seconds. Public and free of any
    /// Unity timing dependency so tests can step it deterministically; <see cref="Update"/>
    /// is just this with Time.deltaTime.
    /// </summary>
    public void Tick(float dt)
    {
        RefreshSensors();
        UpdateDistances();

        switch (State)
        {
            case DoorState.Closed:
                if (NearestApertureDistance <= openDistance)
                {
                    BeginOpening();
                }

                break;

            case DoorState.Opening:
                if (Advance(openingFrames, openingDelays, openSprite, dt))
                {
                    SetState(DoorState.Open);
                }

                break;

            case DoorState.Open:
                if (NearestHoldDistance > holdDistance)
                {
                    _closeTimer += dt;
                }
                else
                {
                    _closeTimer = 0f;
                }

                if (_closeTimer >= closeDelay)
                {
                    BeginClosing();
                }

                break;

            case DoorState.Closing:
                // Someone stepped back into the aperture: re-open instead of
                // finishing the close, which is what stops doorway chatter.
                if (NearestApertureDistance <= openDistance)
                {
                    BeginOpening();
                    break;
                }

                if (Advance(closingFrames, closingDelays, closedSprite, dt))
                {
                    SetState(DoorState.Closed);
                }

                break;
        }
    }

    /// <summary>Snaps the door to a terminal state without animating. Test/diagnostic helper.</summary>
    public void SetStateImmediate(DoorState state)
    {
        _frameIndex = 0;
        _frameTimer = 0f;
        _closeTimer = 0f;

        if (state == DoorState.Closed)
        {
            if (panel != null && closedSprite != null)
            {
                panel.sprite = closedSprite;
            }

            SetState(DoorState.Closed);
        }
        else
        {
            if (panel != null && openSprite != null)
            {
                panel.sprite = openSprite;
            }

            SetState(DoorState.Open);
        }
    }

    /// <summary>True while the door art presents the fully open state.</summary>
    public bool IsFullyOpen => State == DoorState.Open;

    private void BeginOpening()
    {
        _closeTimer = 0f;
        _frameIndex = 0;
        _frameTimer = 0f;

        if (panel != null && openingFrames.Length > 0)
        {
            panel.sprite = openingFrames[0];
        }

        SetState(DoorState.Opening);
    }

    private void BeginClosing()
    {
        _closeTimer = 0f;
        _frameIndex = 0;
        _frameTimer = 0f;

        if (panel != null && closingFrames.Length > 0)
        {
            panel.sprite = closingFrames[0];
        }

        SetState(DoorState.Closing);
    }

    /// <summary>
    /// Steps the animation for <paramref name="dt"/> and returns true when the
    /// terminal sprite has been reached.
    /// </summary>
    private bool Advance(Sprite[] frames, float[] delays, Sprite terminal, float dt)
    {
        if (frames == null || frames.Length == 0 || panel == null)
        {
            if (panel != null && terminal != null)
            {
                panel.sprite = terminal;
            }

            return true;
        }

        if (_frameIndex >= frames.Length)
        {
            panel.sprite = terminal;
            return true;
        }

        _frameTimer += dt;

        while (_frameIndex < frames.Length && _frameTimer >= DelayAt(delays, _frameIndex))
        {
            _frameTimer -= DelayAt(delays, _frameIndex);
            _frameIndex++;

            if (_frameIndex >= frames.Length)
            {
                panel.sprite = terminal;
                return true;
            }

            panel.sprite = frames[_frameIndex];
        }

        return false;
    }

    private static float DelayAt(float[] delays, int index)
    {
        if (delays == null || delays.Length == 0)
        {
            // Upstream default when an RSI omits delays.
            return 0.1f;
        }

        return delays[Mathf.Clamp(index, 0, delays.Length - 1)];
    }

    private void SetState(DoorState next)
    {
        DoorState previous = State;
        State = next;

        if (closedCollider != null)
        {
            closedCollider.enabled = next != DoorState.Open;
        }

        if (previous != next && logTransitions)
        {
            Debug.Log($"[ProximityAirlock] {name}: {previous} -> {next} (sprite {CurrentSpriteName})");
        }

        if (previous != next)
        {
            StateChanged?.Invoke(this, previous, next);
        }
    }

    private void RefreshSensors()
    {
        bool usable = sensors != null && sensors.Length > 0;
        if (usable)
        {
            for (int i = 0; i < sensors.Length; i++)
            {
                if (sensors[i] == null)
                {
                    usable = false;
                    break;
                }
            }
        }

        if (usable)
        {
            return;
        }

        List<Transform> found = new List<Transform>();

        foreach (PlayerMovement player in FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None))
        {
            if (player != null)
            {
                found.Add(player.transform);
            }
        }

        sensors = found.ToArray();
    }

    private void UpdateDistances()
    {
        NearestApertureDistance = float.PositiveInfinity;
        NearestHoldDistance = float.PositiveInfinity;

        if (sensors == null || sensors.Length == 0)
        {
            return;
        }

        // The door tile GameObject sits at the centre of the doorway, so the
        // sensor's position in this transform's local space is already relative
        // to the door centre regardless of the ship's own transform.
        float paddedHalfWidth = apertureHalfWidth + 0.35f;

        for (int i = 0; i < sensors.Length; i++)
        {
            Transform sensor = sensors[i];
            if (sensor == null)
            {
                continue;
            }

            Vector3 local = transform.InverseTransformPoint(sensor.position);
            float lateral = passageAlongLocalY ? Mathf.Abs(local.x) : Mathf.Abs(local.y);
            float along = passageAlongLocalY ? Mathf.Abs(local.y) : Mathf.Abs(local.x);

            if (lateral <= apertureHalfWidth)
            {
                NearestApertureDistance = Mathf.Min(NearestApertureDistance, along);
            }

            if (lateral <= paddedHalfWidth)
            {
                NearestHoldDistance = Mathf.Min(NearestHoldDistance, along);
            }
        }
    }
}
