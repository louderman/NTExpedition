using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Genuine play-mode exercise of the proximity airlocks in the saved HomeShip scene.
///
/// Runs the real scene in play mode, drives the real Player GameObject through both
/// interior doorways from both sides, holds position in a doorway, and runs an
/// approach/retreat chatter test, recording every door state transition with the player
/// position at that moment. It also renders closed/open evidence frames and asserts that
/// no hull-side airlock gained the component.
///
/// Headless entry point: <see cref="RunRuntimeTest"/> (needs a graphics display; do not
/// pass -quit, the test drives its own exit).
/// </summary>
public static class ProximityAirlockVerify
{
    const string ScenePath = "Assets/Scenes/HomeShip.unity";

    // Deliberately outside Assets: writing inside the project during play mode can
    // trigger an import/domain reload in the middle of the run.
    const string ScratchDir = "/home/dan/.hermes/profiles/coder/cache/scratch";
    const string RenderDir = ScratchDir + "/airlock_renders";
    const string ReportPath = ScratchDir + "/airlock-runtime-report.txt";

    // The runner writes its exit code here as soon as the report is complete. Teardown must not
    // depend on a delegate surviving the play-mode domain reload: in the run of 2026-11-18 the
    // reload dropped the runner's Finished handler, the editor idled in play mode for a further
    // 266 s and the abort notice then overwrote the real report. Poll reads this file instead.
    const string CodePath = ScratchDir + "/airlock-runtime-report.code";

    /// <summary>
    /// One passage tile in world units - measured, not assumed. The salvageshuttle layout grid in
    /// Assets/ShipData/salvageshuttle.layout.tsv steps 1.00 per tile in world x/y (the floor column at
    /// x=0.5 has rows at y=-11.5, -10.5, ...), and the tile art is 32 px at spritePixelsToUnits = 32
    /// (Assets/Sprites/SS14/SalvageShuttle/**/*.png.meta). The requirement is that the interior
    /// airlocks start opening at most one tile from the doorway.
    /// </summary>
    const float OneTile = 1.0f;

    static readonly Vector2 DoorSalvage = new Vector2(2.5f, 1.5f);
    static readonly Vector2 DoorEngineering = new Vector2(2.5f, -8.5f);

    // Play-mode entry performs a domain reload, which wipes static delegates. The intent
    // to run therefore has to live in SessionState (it survives the reload) and the
    // play-mode hook has to be re-installed by [InitializeOnLoadMethod] in the new domain.
    // On top of that, everything below is driven by a poll on EditorApplication.update, so
    // no single callback has to survive: if the hook never fires, the poll spawns the runner.
    const string SessionRun = "NTExpedition.AirlockVerify.Run";
    const string SessionStarted = "NTExpedition.AirlockVerify.StartedAt";
    const string SessionPlayStart = "NTExpedition.AirlockVerify.PlayStartedAt";
    const string SessionExit = "NTExpedition.AirlockVerify.ExitCode";
    const string SessionRunnerUp = "NTExpedition.AirlockVerify.RunnerUp";
    const string SessionSpawnAttempts = "NTExpedition.AirlockVerify.SpawnAttempts";

    // Bounds: a headless play-mode run must never be able to hang or to flood the log.
    const float BeginTimeout = 90f;   // waiting for the asset pipeline to go idle + play mode to start
    const float RunTimeout = 240f;    // hard cap once play mode is actually running
    const float SpawnGrace = 2f;      // if the state-change hook missed it, spawn the runner ourselves
    const int RespawnLimit = 2;       // never respawn the runner more than this many times

    [InitializeOnLoadMethod]
    static void HookPlayModeState()
    {
        EditorApplication.playModeStateChanged -= OnPlayState;
        EditorApplication.playModeStateChanged += OnPlayState;
        if (SessionState.GetBool(SessionRun, false))
        {
            EditorApplication.update -= Poll;
            EditorApplication.update += Poll;
            Debug.Log("[AirlockTest] hook re-installed after a domain reload");
        }
    }

    [MenuItem("Tools/Salvage/5 verify proximity airlocks (play mode)")]
    public static void RunRuntimeTest()
    {
        SessionState.SetBool(SessionRun, true);
        SessionState.SetBool(SessionRunnerUp, false);
        SessionState.SetInt(SessionExit, 9);
        SessionState.SetInt(SessionSpawnAttempts, 0);
        SessionState.SetFloat(SessionStarted, (float)EditorApplication.timeSinceStartup);
        SessionState.SetFloat(SessionPlayStart, 0f);

        // A stale exit-code file from an earlier run must never be mistaken for this one's result.
        try
        {
            if (File.Exists(CodePath))
            {
                File.Delete(CodePath);
            }
        }
        catch (Exception)
        {
            // worst case a leftover file ends the run early; not worth failing over
        }

        EditorApplication.update -= Poll;
        EditorApplication.update += Poll;
        Debug.Log("[AirlockTest] queued; will enter play mode once the asset pipeline is idle");
    }

    /// <summary>
    /// Single bounded state machine driven from EditorApplication.update (re-installed after
    /// every domain reload by <see cref="HookPlayModeState"/>):
    ///   waiting  -> asset pipeline idle? open the scene and enter play mode
    ///   entering -> play mode up? make sure the runner exists (spawn it if the hook missed it)
    ///   running  -> hard cap, then write a failure report and exit non-zero
    /// </summary>
    static void Poll()
    {
        // The runner has already reported (SessionExit left its 9 sentinel): leave the editor
        // at once. ExitPlaymode() defers, and when it is called from inside a play-mode
        // coroutine it can be swallowed outright - that is what left the editor idling in play
        // mode for 266 s after the report was written. Exiting from the editor loop here is the
        // only reliable teardown.
        int reported = SessionState.GetInt(SessionExit, 9);
        if (reported < 9)
        {
            EditorApplication.update -= Poll;
            Debug.Log($"[AirlockTest] runner reported {reported}; exiting the editor");
            EditorApplication.Exit(reported);
            return;
        }

        if (!SessionState.GetBool(SessionRun, false))
        {
            EditorApplication.update -= Poll;
            return;
        }

        float elapsed = (float)EditorApplication.timeSinceStartup -
                        SessionState.GetFloat(SessionStarted, (float)EditorApplication.timeSinceStartup);

        if (!EditorApplication.isPlayingOrWillChangePlaymode)
        {
            if (elapsed > BeginTimeout)
            {
                Bail(3, $"the asset pipeline never went idle within {BeginTimeout:F0}s " +
                         $"(isCompiling={EditorApplication.isCompiling}, isUpdating={EditorApplication.isUpdating})");
                return;
            }

            // Entering play mode while an import is still settling is what triggered a forced
            // synchronous recompile + domain reload in the middle of the previous run.
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return;
            }

            Debug.Log($"[AirlockTest] asset pipeline idle after {elapsed:F1}s -> open scene + enter play mode");
            SessionState.SetFloat(SessionStarted, (float)EditorApplication.timeSinceStartup);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
            return;
        }

        if (!EditorApplication.isPlaying)
        {
            if (elapsed > BeginTimeout)
            {
                Bail(3, "play mode did not start in time");
            }

            return;
        }

        float playStart = SessionState.GetFloat(SessionPlayStart, 0f);
        if (playStart <= 0f)
        {
            playStart = (float)EditorApplication.timeSinceStartup;
            SessionState.SetFloat(SessionPlayStart, playStart);
            Debug.Log("[AirlockTest] play mode is running");
        }

        // Delegate-independent teardown: the runner writes its exit code to disk once the report
        // is complete, so losing the Finished handler (which did happen) cannot strand the run in
        // play mode until the hard cap.
        int fileCode = ReadCodeFile();
        if (fileCode >= 0 && fileCode < 9)
        {
            SessionState.SetInt(SessionExit, fileCode);
            SessionState.SetBool(SessionRun, false);
            SessionState.SetBool(SessionRunnerUp, false);
            SessionState.SetInt(SessionSpawnAttempts, 0);
            EditorApplication.update -= Poll;
            Debug.Log($"[AirlockTest] runner reported {fileCode} via {CodePath}; exiting the editor");
            EditorApplication.Exit(fileCode);
            return;
        }

        bool runnerAlive = SessionState.GetBool(SessionRunnerUp, false) &&
                           UnityEngine.Object.FindObjectsByType<AirlockVerifyRunner>(FindObjectsSortMode.None).Length > 0;

        if (!runnerAlive && !EditorApplication.isCompiling && !EditorApplication.isUpdating &&
            (float)EditorApplication.timeSinceStartup - playStart > SpawnGrace)
        {
            int attempts = SessionState.GetInt(SessionSpawnAttempts, 0);
            if (attempts < RespawnLimit)
            {
                SessionState.SetInt(SessionSpawnAttempts, attempts + 1);
                SpawnRunner($"poll fallback #{attempts + 1}: no runner present in play mode");
            }
        }

        if (elapsed > BeginTimeout + RunTimeout)
        {
            Bail(5, $"run exceeded the {RunTimeout:F0}s play-mode cap " +
                    $"(runner alive = {SessionState.GetBool(SessionRunnerUp, false)}); " +
                    "see " + ReportPath + ".partial for the phases that did run");
        }
    }

    static void SpawnRunner(string why)
    {
        SessionState.SetBool(SessionRunnerUp, true);
        var host = new GameObject("__airlock_verify_host");
        var runner = host.AddComponent<AirlockVerifyRunner>();
        runner.Finished += code =>
        {
            // Report first, then leave. Calling ExitPlaymode() here does not work (see Poll):
            // this handler runs inside the play-mode coroutine, so the request is deferred and
            // can be dropped. EditorApplication.Exit is honoured from anywhere.
            SessionState.SetInt(SessionExit, code);
            SessionState.SetBool(SessionRun, false);
            SessionState.SetBool(SessionRunnerUp, false);
            SessionState.SetInt(SessionSpawnAttempts, 0);
            EditorApplication.update -= Poll;
            EditorApplication.Exit(code);
        };
        Debug.Log("[AirlockTest] runner spawned (" + why + ")");
    }

    /// <summary>Exit code written by the runner, or -1 when there is not one yet.</summary>
    static int ReadCodeFile()
    {
        try
        {
            if (!File.Exists(CodePath))
            {
                return -1;
            }

            return int.TryParse(File.ReadAllText(CodePath).Trim(), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out int code) ? code : -1;
        }
        catch (Exception)
        {
            return -1;
        }
    }

    /// <summary>Logs why, writes an abort note, and exits the editor with <paramref name="code"/>.</summary>
    static void Bail(int code, string reason)
    {
        Debug.LogError("[AirlockTest] aborting: " + reason);
        SessionState.SetBool(SessionRun, false);
        EditorApplication.update -= Poll;

        // Never write to ReportPath here: a real report may already be on disk, and that is exactly
        // how the previous run's evidence was destroyed - the abort notice overwrote it.
        string abortPath = ReportPath + ".abort";
        try
        {
            Directory.CreateDirectory(ScratchDir);
            File.WriteAllText(abortPath,
                "Proximity airlock runtime verification (play mode)\n" +
                "scene: " + ScenePath + "\n" +
                "generated: " + DateTime.UtcNow.ToString("u", CultureInfo.InvariantCulture) + "\n" +
                "unity: " + Application.unityVersion + "\n" +
                "result: FAIL (aborted)\n" +
                "reason: " + reason + "\n" +
                "note: " + (File.Exists(ReportPath)
                    ? "a completed report also exists at " + ReportPath
                    : "no completed report was written") + "\n");
        }
        catch (Exception)
        {
            // reporting must never mask the real failure
        }

        EditorApplication.Exit(code);
    }

    static void OnPlayState(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(SessionRun, false))
        {
            return;
        }

        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            SpawnRunner("playModeStateChanged hook");
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(SessionRun, false);
            SessionState.SetInt(SessionSpawnAttempts, 0);
            int code = SessionState.GetInt(SessionExit, 9);
            EditorApplication.update -= Poll;
            EditorApplication.Exit(code);
        }
    }

    /// <summary>Play-mode driver. Created in memory only; never part of a saved scene.</summary>
    public class AirlockVerifyRunner : MonoBehaviour
    {
        public event Action<int> Finished;

        readonly List<string> _log = new List<string>();
        readonly List<string> _failures = new List<string>();
        readonly List<string> _renders = new List<string>();

        // one entry per walk segment: label (+ "retreat" marker) and the states seen
        readonly List<(string Label, bool Retreated, List<string> States)> _segments =
            new List<(string, bool, List<string>)>();

        int _transitions;
        int _closingEvents;
        int _frames;
        float _frameSeconds;

        PlayerMovement _player;
        Rigidbody2D _body;
        Transform _playerT;
        Camera _cam;
        GameObject _camGo;
        List<string> _currentSeq;

        void Start()
        {
            StartCoroutine(SubscribeAfterSpawn());
            StartCoroutine(Scenario());
        }

        // Headless play mode runs at a few thousand frames per second. Recording the real frame
        // pacing makes the measurement in phase 6 checkable rather than assumed.
        void Update()
        {
            _frames++;
            _frameSeconds += Time.deltaTime;
        }

        IEnumerator SubscribeAfterSpawn()
        {
            yield return null;
            foreach (var d in FindObjectsByType<ProximityAirlock>(FindObjectsSortMode.None))
            {
                var door = d;
                door.StateChanged += (_, prev, next) =>
                {
                    _transitions++;
                    if (next == ProximityAirlock.DoorState.Closing) _closingEvents++;
                    _currentSeq?.Add(next.ToString());
                    string line = $"{door.name}: {prev}->{next} at player " +
                                  $"{(_playerT != null ? Fmt(_playerT.position) : "?")} " +
                                  $"apertureDist={door.NearestApertureDistance:F2} t={Time.time:F2}";
                    _log.Add("transition " + line);
                    Debug.Log("[AirlockTest] transition " + line);
                };
            }
        }

        void Line(string s)
        {
            _log.Add(s);
            Debug.Log("[AirlockTest] " + s);
            if (_log.Count % 10 == 0)
            {
                FlushPartial();
            }
        }

        /// <summary>Cheap incremental persistence, so even a killed run leaves evidence on disk.</summary>
        void FlushPartial()
        {
            try
            {
                Directory.CreateDirectory(ScratchDir);
                File.WriteAllText(ReportPath + ".partial",
                    string.Join("\n", _log) + "\n--- failures ---\n" + string.Join("\n", _failures) + "\n");
            }
            catch (Exception)
            {
                // reporting must never break the run
            }
        }

        void Fail(string s)
        {
            _failures.Add(s);
            Debug.LogError("[AirlockTest][FAIL] " + s);
        }

        void Check(bool ok, string what)
        {
            if (ok)
            {
                Line("PASS  " + what);
            }
            else
            {
                Fail(what);
            }
        }

        // ------------------------------------------------------------------ scenario

        IEnumerator Scenario()
        {
            yield return null;
            yield return new WaitForSeconds(0.5f);

            var doors = FindObjectsByType<ProximityAirlock>(FindObjectsSortMode.None)
                .OrderByDescending(d => d.transform.position.y).ToArray();
            Line($"universe: automatic airlocks in scene = {doors.Length}");
            Check(doors.Length == 2, "exactly 2 automatic airlocks are wired");

            foreach (var d in doors)
            {
                Line($"wired {d.gameObject.name} tile=({d.transform.position.x}," +
                     $"{d.transform.position.y}) state={d.State} sprite={d.CurrentSpriteName} " +
                     $"panel={d.panel?.name} sortOrder={d.panel?.sortingOrder}");
                Check(d.panel != null, $"{d.gameObject.name} has a panel renderer");
                Check(d.openingFrames.Length == 6 && d.closingFrames.Length == 6,
                    $"{d.gameObject.name} drives 6 opening + 6 closing frames (upstream states)");
                Check(d.openSprite != null && d.closedSprite != null,
                    $"{d.gameObject.name} has open + closed terminal sprites");
                Check(d.GetComponents<Collider2D>().Length == 0,
                    $"{d.gameObject.name} stays art-only (no new collider; ship has no hull colliders either)");
                Check(d.openDistance <= OneTile + 1e-4f,
                    $"{d.gameObject.name} starts opening at most one tile away " +
                    $"(openDistance={d.openDistance} m, one tile={OneTile} m)");
            }

            var salvage = doors.FirstOrDefault(d => d.gameObject.name.StartsWith("AirlockSalvage"));
            var engineering = doors.FirstOrDefault(d => d.gameObject.name.StartsWith("AirlockEngineering"));
            Check(salvage != null && engineering != null, "both interior passage doors are the automatic ones");
            if (salvage == null || engineering == null)
            {
                Finish(2);
                yield break;
            }

            // ---- player -------------------------------------------------------
            _player = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None).FirstOrDefault();
            Check(_player != null, "the scene's PlayerMovement (player) exists");
            if (_player == null)
            {
                Finish(2);
                yield break;
            }

            _playerT = _player.transform;
            _body = _player.GetComponent<Rigidbody2D>();
            var cols = _player.GetComponents<Collider2D>();
            Line($"player colliders = {cols.Length} ({string.Join(",", cols.Select(c => c.GetType().Name))}) " +
                 $"rigidbody = {(_body != null ? _body.bodyType.ToString() : "none")}");
            Line($"player spawn position = {Fmt(_playerT.position)}");
            Line($"Main Camera present = {GameObject.Find("Main Camera") != null}");

            yield return Capture("00_start_closed", DoorSalvage, 3.0f, 480, 360);

            // ============ ENGINEERING DOOR: north side -> south side ============
            Line("--- phase 1: AirlockEngineering approached from the north side ---");
            yield return Walk(new Vector2(2.5f, -6.0f), new Vector2(2.5f, -9.8f), 1.6f, engineering, false);
            yield return Observe(engineering, "passed north->south", "Open");
            yield return Capture("01_engineering_open", DoorEngineering, 3.0f, 480, 360);
            yield return Walk(new Vector2(2.5f, -9.8f), new Vector2(2.5f, -12.2f), 1.6f, engineering, true);
            yield return Observe(engineering, "retreating away south", "ClosingOrClosed");
            yield return Settle(2.0f);
            yield return Observe(engineering, "settled south", "Closed");

            // ============ ENGINEERING DOOR: south side -> north side ============
            Line("--- phase 2: AirlockEngineering approached from the south side ---");
            yield return Walk(new Vector2(2.5f, -12.2f), new Vector2(2.5f, -6.0f), 1.6f, engineering, false);
            yield return Observe(engineering, "passed south->north", "Open");
            yield return Capture("02_engineering_open_from_south", DoorEngineering, 3.0f, 480, 360);
            yield return Walk(new Vector2(2.5f, -6.0f), new Vector2(2.5f, -4.0f), 1.6f, engineering, true);
            yield return Settle(2.0f);
            yield return Observe(engineering, "settled north", "Closed");

            // ============ SALVAGE DOOR: pass through, then return ===============
            Line("--- phase 3: AirlockSalvage (the door just outside the cockpit) ---");
            yield return Walk(new Vector2(2.5f, -4.0f), new Vector2(2.5f, 4.6f), 2.4f, salvage, false);
            yield return Settle(2.5f);
            yield return Observe(salvage, "closed again, player standing clear in the cockpit", "Closed");
            yield return Capture("03_salvage_closed", DoorSalvage, 3.0f, 480, 360);

            yield return Walk(new Vector2(2.5f, 4.6f), new Vector2(2.5f, -1.2f), 1.6f, salvage, false);
            yield return Observe(salvage, "passed cockpit->bridge", "Open");
            yield return Capture("04_salvage_open", DoorSalvage, 3.0f, 480, 360);

            // ============ STAND IN THE DOORWAY =================================
            Line("--- phase 4: hold position inside the AirlockSalvage doorway for 3 s ---");
            yield return Place(new Vector2(2.5f, 1.9f));
            yield return Settle(0.8f);
            yield return Observe(salvage, "arrived in the doorway", "Open");
            int before = _transitions;
            yield return Settle(3.0f);
            yield return Observe(salvage, "after 3 s standing in the doorway", "Open");
            Check(_transitions == before,
                $"door stayed put while the player stood in the doorway ({_transitions - before} transitions)");

            // ============ APPROACH / RETREAT CHATTER ===========================
            Line("--- phase 5: approach/retreat cycles (chatter test) ---");
            for (int i = 0; i < 3; i++)
            {
                // Fast approach, then a slow monotone retreat. At 1.5 u/s the retreat lasts
                // long enough that the 0.5 s close delay really does elapse inside the
                // segment, so the Closing transition is exercised rather than skipped.
                yield return Walk(new Vector2(2.5f, 5.2f), new Vector2(2.5f, 0.9f), 5f, salvage, false);
                yield return Settle(0.35f);
                yield return Walk(new Vector2(2.5f, 0.9f), new Vector2(2.5f, 5.2f), 1.5f, salvage, true);
                yield return Settle(1.6f);
            }

            yield return Settle(1.5f);
            yield return Observe(salvage, "settled after the chatter cycles", "Closed");
            Check(NoChatterWhileRetreating(),
                "no Closing->Opening re-open inside any monotone retreat " +
                $"({_segments.Count(s => s.Retreated)} retreat segments checked)");
            yield return Capture("05_salvage_after_chatter", DoorSalvage, 3.0f, 480, 360);

            // ============ HYSTERESIS BAND ======================================
            // openDistance 1.0 < holdDistance 2.25. Hovering between the two radii must never
            // begin a close - that is exactly "no premature closing / no chatter" - so this
            // phase asserts a count of zero Closing transitions across 8 threshold crossings.
            Line("--- phase 5b: hover across the hysteresis band (open 1.0 < hold 2.25) ---");
            yield return Place(new Vector2(2.5f, 1.9f));
            yield return Settle(0.8f);
            yield return Observe(salvage, "back in the doorway before the hover", "Open");
            int closingBeforeHover = _closingEvents;
            for (int i = 0; i < 4; i++)
            {
                yield return Walk(new Vector2(2.5f, 1.9f), new Vector2(2.5f, 3.1f), 1.2f, salvage, false);
                yield return Walk(new Vector2(2.5f, 3.1f), new Vector2(2.5f, 1.9f), 1.2f, salvage, false);
            }

            yield return Observe(salvage, "after hovering inside the hold band", "Open");
            Check(_closingEvents == closingBeforeHover,
                "hovering inside the hold band never began a close " +
                $"({_closingEvents - closingBeforeHover} Closing transitions over 8 crossings)");

            // ============ THRESHOLD ===========================================
            // Both interior doors, approached from opposite sides, thresholds read back from the
            // component itself. See MeasureThreshold for why this is stepped in fixed 0.05 m
            // increments instead of by Time.deltaTime.
            Line("--- phase 6: stepped threshold measurement for both interior doors ---");
            yield return MeasureThreshold(salvage, DoorSalvage, +1);
            yield return MeasureThreshold(engineering, DoorEngineering, -1);

            // ============ REGRESSION SWEEP ====================================
            var live = FindObjectsByType<ProximityAirlock>(FindObjectsSortMode.None);
            Check(live.Length == 2, "still exactly 2 automatic airlocks at the end of the run");
            foreach (var d in live)
            {
                bool interior = d.gameObject.name.StartsWith("AirlockSalvage") ||
                                d.gameObject.name.StartsWith("AirlockEngineering");
                Check(interior, $"automatic airlock is an interior passage door ({d.gameObject.name})");
            }

            foreach (var n in new[]
                     {
                         "AirlockExternalGlassShuttleArrivals#92", "AirlockExternalGlassShuttleArrivals#93",
                         "AirlockExternalGlassShuttleArrivals#94", "AirlockExternalGlassShuttleArrivals#95",
                         "AirlockExternalGlassShuttleArrivals#96", "AirlockExternalGlassShuttleArrivals#97",
                     })
            {
                var go = GameObject.Find(n);
                Check(go != null && go.GetComponent<ProximityAirlock>() == null,
                    $"hull-side airlock left untouched ({n})");
            }

            // ============ EVIDENCE FRAMES (final revision) ====================
            // Deliberate, labelled frames for the operator's screenshot request: for each interior
            // airlock, one frame with the player just outside the one-tile trigger (door closed) and
            // one on approach inside it (door open). Both states are asserted, so a wrong state can
            // never be presented as evidence.
            Line("--- phase 7: labelled evidence frames for both interior airlocks ---");
            yield return EvidenceFrames(salvage, DoorSalvage, "06_salvage");
            yield return EvidenceFrames(engineering, DoorEngineering, "07_engineering");

            Line($"frame pacing over this run: {_frames} frames in {_frameSeconds:F1}s " +
                 $"(mean deltaTime={(_frames > 0 ? _frameSeconds / _frames : 0f):F5}s, " +
                 $"~{(_frameSeconds > 0f ? _frames / _frameSeconds : 0f):F0} fps)");
            Line($"total door state transitions during the run: {_transitions}");
            Finish(_failures.Count == 0 ? 0 : 1);
        }

        // ------------------------------------------------------------------ assertions

        /// <summary>
        /// Chatting would show up as the door re-opening while the player is walking
        /// away. Inside one monotone retreat segment that must never happen.
        /// </summary>
        bool NoChatterWhileRetreating()
        {
            bool ok = true;
            foreach (var seg in _segments.Where(s => s.Retreated))
            {
                for (int i = 1; i < seg.States.Count; i++)
                {
                    if (seg.States[i - 1] == "Closing" && seg.States[i] == "Opening")
                    {
                        Line($"chatter detected in retreat segment '{seg.Label}': " +
                             string.Join("->", seg.States));
                        ok = false;
                    }
                }
            }

            return ok;
        }

        // ------------------------------------------------------------------ driving

        void Snap(Vector2 p)
        {
            if (_body != null)
            {
                _body.velocity = Vector2.zero;
                _body.angularVelocity = 0f;
            }

            _playerT.position = new Vector3(p.x, p.y, _playerT.position.z);
        }

        IEnumerator Place(Vector2 p)
        {
            Snap(p);
            yield return null;
        }

        IEnumerator Walk(Vector2 from, Vector2 to, float speed, ProximityAirlock watch, bool retreat)
        {
            float dist = Vector2.Distance(from, to);
            float dur = dist / Mathf.Max(0.01f, speed);
            float t = 0f;
            var seq = new List<string>();
            _currentSeq = seq;
            Snap(from);
            while (t < dur)
            {
                t += Time.deltaTime;
                Snap(Vector2.Lerp(from, to, Mathf.Clamp01(t / dur)));
                yield return null;
            }

            Snap(to);
            yield return null;
            _currentSeq = null;
            _segments.Add(($"{watch?.name}: {Fmt(from)}->{Fmt(to)} @{speed}u/s", retreat, seq));
            Line($"walk {(retreat ? "retreat" : "approach")} {Fmt(from)}->{Fmt(to)} @{speed}u/s " +
                 $"({dur:F2}s) states={string.Join("->", seq)}");
        }

        IEnumerator Settle(float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                if (_body != null)
                {
                    _body.velocity = Vector2.zero;
                }

                yield return null;
            }
        }

        /// <summary>
        /// Settles at one exact position: the player is re-snapped every frame, so nothing -
        /// residual velocity, a physics step, another system nudging the transform - can drift a
        /// measurement. Used by the threshold phase, where the distance *is* the measurement.
        /// </summary>
        IEnumerator Hold(Vector2 p, float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                Snap(p);
                yield return null;
            }
        }

        /// <summary>
        /// Two labelled evidence frames for one door, from the saved scene in play mode:
        ///   1. door closed with the player just outside the one-tile trigger,
        ///   2. door open with the player on approach inside it.
        /// Each frame's state is asserted first, so a frame can never show the wrong state.
        /// </summary>
        IEnumerator EvidenceFrames(ProximityAirlock door, Vector2 tile, string stem)
        {
            string tag = door.name;
            float delay = door.closeDelay;
            float outsideD = OneTile + 0.15f;   // 1.15 m - outside the 1.00 m trigger
            float insideD = OneTile - 0.45f;    // 0.55 m - inside it

            var clearP = new Vector2(tile.x, tile.y + 3.2f);
            Snap(clearP);
            yield return Hold(clearP, delay + 1.0f);
            Check(door.State == ProximityAirlock.DoorState.Closed,
                $"[{tag}] evidence: door closed before the frames");

            // frame 1: closed, just outside one tile
            var outP = new Vector2(tile.x, tile.y + outsideD);
            Snap(outP);
            yield return Hold(outP, 0.6f);
            Check(door.State == ProximityAirlock.DoorState.Closed && door.CurrentSpriteName.StartsWith("closed"),
                $"[{tag}] evidence frame 1: still Closed at {outsideD:F2} m (just outside one tile), " +
                $"player y={_playerT.position.y:F2}, sprite={door.CurrentSpriteName}");
            yield return Capture($"{stem}_closed_just_outside_one_tile", tile, 3.0f, 640, 480);

            // frame 2: open, on approach inside one tile
            var inP = new Vector2(tile.x, tile.y + insideD);
            Snap(inP);
            yield return Hold(inP, 1.2f);
            Check(door.State == ProximityAirlock.DoorState.Open && door.CurrentSpriteName.StartsWith("open"),
                $"[{tag}] evidence frame 2: Open at {insideD:F2} m (inside one tile), " +
                $"player y={_playerT.position.y:F2}, sprite={door.CurrentSpriteName}");
            yield return Capture($"{stem}_open_on_approach", tile, 3.0f, 640, 480);
        }

        IEnumerator Observe(ProximityAirlock door, string label, string expect)
        {
            Line($"observe {door.name} / {label}: state={door.State} sprite={door.CurrentSpriteName} " +
                 $"player={Fmt(_playerT.position)} apertureDist={door.NearestApertureDistance:F2} " +
                 $"holdDist={door.NearestHoldDistance:F2}");
            switch (expect)
            {
                case "Open":
                    Check(door.State == ProximityAirlock.DoorState.Open, $"{label}: door is fully open");
                    Check(door.CurrentSpriteName.StartsWith("open"), $"{label}: service-open sprite showing");
                    break;
                case "Closed":
                    Check(door.State == ProximityAirlock.DoorState.Closed, $"{label}: door is closed");
                    Check(door.CurrentSpriteName.StartsWith("closed"), $"{label}: closed sprite showing");
                    break;
                case "ClosingOrClosed":
                    Check(door.State == ProximityAirlock.DoorState.Closing ||
                          door.State == ProximityAirlock.DoorState.Closed,
                        $"{label}: door is closing or closed");
                    break;
            }

            yield return null;
        }

        /// <summary>
        /// Frame-rate independent threshold measurement - the phase the first attempt got wrong.
        /// It stepped by 2 * Time.deltaTime and gave up after 3000 iterations, but headless play
        /// mode runs at a few thousand frames per second (deltaTime ~4e-4 s), so 3000 iterations
        /// only covered 2.4 m of the 4.5 m approach: the walker never reached the door and the
        /// phase measured nothing. Here the step is a fixed 0.05 m and each step gets two frames
        /// to settle, so the sampled state always belongs to the snapped position.
        ///
        /// <paramref name="side"/> = +1 approaches from the higher-y side of the doorway, -1 from
        /// the lower-y side (the two doors together cover both directions).
        /// </summary>
        IEnumerator MeasureThreshold(ProximityAirlock door, Vector2 tile, int side)
        {
            const int StartCm = 320;   // 3.20 m clear of the doorway
            const int StepCm = 5;      // 0.05 m per step - fine enough to bracket the one-tile trigger
            const int Steps = 71;      // 3.20 m down to -0.30 m (0.30 m past the doorway centre)

            string tag = door.name;
            float open = door.openDistance;
            float hold = door.holdDistance;
            float delay = door.closeDelay;
            float stepM = StepCm / 100f;

            Line($"--- phase 6 [{tag}]: stepped approach from the {(side > 0 ? "higher-y" : "lower-y")} " +
                 $"side, fixed {stepM:F2} m steps, 2 settle frames each " +
                 $"(deltaTime right now {Time.deltaTime:F5}s) ---");
            Line($"[{tag}] configured openDistance={open} holdDistance={hold} closeDelay={delay} " +
                 $"apertureHalfWidth={door.apertureHalfWidth} passageAlongLocalY={door.passageAlongLocalY} " +
                 $"tile={Fmt(tile)} lossyScale={Fmt(door.transform.lossyScale)} " +
                 $"zRotation={door.transform.rotation.eulerAngles.z:F1}");

            // Start clear of the hold radius and let the door settle closed.
            var startP = new Vector2(tile.x, tile.y + side * (StartCm / 100f));
            Snap(startP);
            yield return Hold(startP, delay + 1.0f);
            Check(door.State == ProximityAirlock.DoorState.Closed,
                $"[{tag}] starts closed with the player {StartCm / 100f:F2} m clear");
            Check(door.CurrentSpriteName.StartsWith("closed"),
                $"[{tag}] closed sprite showing before the approach (sprite={door.CurrentSpriteName})");

            var dist = new List<float>();      // signed offset along the passage axis (negative past the centre)
            var states = new List<ProximityAirlock.DoorState>();
            var aperture = new List<float>();  // the door's own distance to the nearest sensor (a magnitude)
            var playerY = new List<float>();   // measured player y, never derived from the tile
            float worst = 0f;

            for (int i = 0; i < Steps; i++)
            {
                // Integer centimetres keep the samples exact, including the 1.50 m one.
                float d = (StartCm - i * StepCm) / 100f;
                Snap(new Vector2(tile.x, tile.y + side * d));
                yield return null;   // the door ticks at the new position...
                yield return null;   // ...and again, so the sample is settled
                float live = door.NearestApertureDistance;
                float delta = Mathf.Abs(live - Mathf.Abs(d));
                worst = Mathf.Max(worst, delta);
                dist.Add(d);
                states.Add(door.State);
                aperture.Add(live);
                playerY.Add(_playerT.position.y);   // measured, not derived from the tile
                Line($"[{tag}] step {i:D2} d={d:F2} m playerY={_playerT.position.y:F2} " +
                     $"state={door.State} apertureDist={live:F3} holdDist={door.NearestHoldDistance:F3} " +
                     $"delta={delta:F4} sprite={door.CurrentSpriteName}");
            }

            // 1. The door must be reading the live player: its own measured distance has to match the
            //    distance that was just snapped. Both sides are magnitudes here - aperture distance is
            //    unsigned and the snapped offset goes negative past the doorway centre. (Comparing the
            //    signed offset instead produced a phantom 0.600 m error at the -0.30 m sample, which is
            //    what made the previous run report two failures that the printed data contradicted.)
            Check(worst <= stepM * 0.5f + 0.001f,
                $"[{tag}] door tracked the live player at every step " +
                $"(worst |apertureDist - |snapped offset|| = {worst:F4} m over {Steps} samples)");

            // 2. State against the door's own measurement: closed whenever it is clearly further
            //    away than openDistance, never closed when it is clearly inside. Samples within
            //    0.02 m of the threshold carry no signal either way and are skipped.
            bool closedWhileFar = true;
            bool closedWhileInside = false;
            bool closingOnApproach = false;
            for (int i = 0; i < Steps; i++)
            {
                if (states[i] == ProximityAirlock.DoorState.Closing)
                {
                    closingOnApproach = true;
                }

                if (aperture[i] > open + 0.02f)
                {
                    if (states[i] != ProximityAirlock.DoorState.Closed)
                    {
                        closedWhileFar = false;
                        Line($"[{tag}] unexpected: {states[i]} at apertureDist={aperture[i]:F3} m " +
                             $"(further out than openDistance={open})");
                    }
                }
                else if (aperture[i] < open - 0.02f)
                {
                    if (states[i] == ProximityAirlock.DoorState.Closed)
                    {
                        closedWhileInside = true;
                        Line($"[{tag}] unexpected: still Closed at apertureDist={aperture[i]:F3} m " +
                             $"(inside openDistance={open})");
                    }
                }
            }

            Check(closedWhileFar, $"[{tag}] closed at every sample further than openDistance={open} m");
            Check(!closedWhileInside, $"[{tag}] never Closed at a sample inside openDistance={open} m");
            Check(!closingOnApproach, $"[{tag}] never began a close while the player was approaching");

            // 3. The decisive bracket, in measured snapped distance: one step outside the configured
            //    threshold and one step inside it.
            int iFar = Mathf.RoundToInt((StartCm / 100f - (open + stepM)) / stepM);
            int iNear = Mathf.RoundToInt((StartCm / 100f - (open - stepM)) / stepM);
            Check(iFar >= 0 && iFar < Steps && states[iFar] == ProximityAirlock.DoorState.Closed,
                $"[{tag}] still closed at d={Sample(dist, iFar)} m (player y={Sample(playerY, iFar)}), " +
                $"one step outside openDistance={open}");
            Check(iNear >= 0 && iNear < Steps && states[iNear] != ProximityAirlock.DoorState.Closed,
                $"[{tag}] already opening at d={Sample(dist, iNear)} m, one step inside openDistance={open}");

            // 3b. The requirement in tiles: one passage tile is 1.00 world unit, so the door must still
            //     be shut just beyond one tile and must have started opening by the time the player is one
            //     tile out. This phase runs +1 on one door and -1 on the other, so both sides are covered.
            int iBeyondTile = Mathf.RoundToInt((StartCm / 100f - (OneTile + stepM)) / stepM);
            int iAtTile = Mathf.RoundToInt((StartCm / 100f - OneTile) / stepM);
            Check(iBeyondTile >= 0 && iBeyondTile < Steps &&
                  states[iBeyondTile] == ProximityAirlock.DoorState.Closed,
                $"[{tag}] still Closed just beyond one tile: d={Sample(dist, iBeyondTile)} m " +
                $"(one tile + {stepM:F2} m), player y={Sample(playerY, iBeyondTile)}");
            Check(iAtTile >= 0 && iAtTile < Steps && states[iAtTile] != ProximityAirlock.DoorState.Closed,
                $"[{tag}] opening at one tile: d={Sample(dist, iAtTile)} m (one tile={OneTile:F2} m), " +
                $"player y={Sample(playerY, iAtTile)}");

            // The 6 upstream opening frames need real time (0.61 s), so let them play out before
            // checking the fully open state; the player is 0.30 m past the doorway centre by then.
            yield return Hold(new Vector2(tile.x, tile.y + side * dist[Steps - 1]), 1.2f);
            yield return Observe(door, $"[{tag}] standing in the doorway at the end of the approach", "Open");

            // 4. Hold band: openDistance (1.0) < d <= holdDistance (2.25). Standing inside it for
            //    longer than closeDelay must keep the door open - no premature closing.
            int closingBefore = _closingEvents;
            float bandD = (open + hold) / 2f;
            var bandP = new Vector2(tile.x, tile.y + side * bandD);
            Snap(bandP);
            yield return Hold(bandP, delay + 1.0f);
            yield return Observe(door,
                $"[{tag}] holding inside the band at d={bandD:F2} m for {delay + 1.0f:F1}s", "Open");
            Check(_closingEvents == closingBefore,
                $"[{tag}] no close began while the player held inside the hold band " +
                $"({_closingEvents - closingBefore} Closing transitions)");

            // 5. Departure: clear of holdDistance and sustained. The close must not start before
            //    closeDelay has elapsed, and must complete once it has.
            float outD = hold + 0.85f;
            var outP = new Vector2(tile.x, tile.y + side * outD);
            Snap(outP);
            yield return Hold(outP, delay * 0.5f);
            Check(door.State == ProximityAirlock.DoorState.Open,
                $"[{tag}] still open {delay * 0.5f:F2}s after stepping clear to d={outD:F2} m " +
                $"(closeDelay={delay}s)");
            yield return Hold(outP, delay * 0.5f + 0.25f);
            Check(door.State == ProximityAirlock.DoorState.Closing ||
                  door.State == ProximityAirlock.DoorState.Closed,
                $"[{tag}] began closing once the doorway had been clear for longer than closeDelay={delay}s");

            float closing = 0f;
            while (door.State != ProximityAirlock.DoorState.Closed && closing < 3f)
            {
                closing += Time.deltaTime;
                Snap(outP);
                yield return null;
            }

            Check(door.State == ProximityAirlock.DoorState.Closed,
                $"[{tag}] finished closing {closing:F2}s after the close began");
            int afterClose = _transitions;
            yield return Hold(outP, 1.0f);
            Check(_transitions == afterClose, $"[{tag}] no further transitions while the player stayed clear");
            yield return Observe(door, $"[{tag}] settled clear after the departure", "Closed");
        }

        // ------------------------------------------------------------------ evidence

        IEnumerator Capture(string tag, Vector2 center, float orthoSize, int w, int h)
        {
            yield return null;

            if (_camGo == null)
            {
                _camGo = new GameObject("__airlock_verify_cam");
                _cam = _camGo.AddComponent<Camera>();
                _cam.orthographic = true;
                _cam.clearFlags = CameraClearFlags.SolidColor;
                _cam.backgroundColor = new Color(0.035f, 0.04f, 0.07f, 1f);
                _cam.nearClipPlane = 0.01f;
                _cam.farClipPlane = 1000f;
                _cam.depth = 100f;
            }

            _cam.orthographicSize = orthoSize;
            _cam.transform.position = new Vector3(center.x, center.y, -100f);

            Directory.CreateDirectory(RenderDir);
            var path = $"{RenderDir}/airlock_{tag}.png";
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            rt.Create();
            _cam.targetTexture = rt;

            bool rendered = false;
            var sreq = new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest
            {
                destination = rt,
            };
            if (UnityEngine.Rendering.RenderPipeline.SupportsRenderRequest(_cam, sreq))
            {
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(_cam, sreq);
                rendered = true;
            }

            if (!rendered)
            {
                _cam.Render();
            }

            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            RenderTexture.active = null;
            _cam.targetTexture = null;
            rt.Release();
            DestroyImmediate(tex);
            _renders.Add(path);
            Line($"render {tag}: {(rendered ? "render-request" : "camera.render")} state(featured door) -> {path}");
            yield return null;
        }

        void Finish(int code)
        {
            Directory.CreateDirectory(ScratchDir);
            var sb = new StringBuilder();
            sb.AppendLine("Proximity airlock runtime verification (play mode)");
            sb.AppendLine("scene: " + ScenePath);
            sb.AppendLine("generated: " + DateTime.UtcNow.ToString("u", CultureInfo.InvariantCulture));
            sb.AppendLine("unity: " + Application.unityVersion);
            sb.AppendLine("result: " + (code == 0 ? "PASS" : "FAIL") + " (" + _failures.Count + " failures)");
            sb.AppendLine();
            sb.AppendLine("== log ==");
            foreach (var l in _log)
            {
                sb.AppendLine(l);
            }

            if (_failures.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("== failures ==");
                foreach (var f in _failures)
                {
                    sb.AppendLine(f);
                }
            }

            sb.AppendLine();
            sb.AppendLine("== renders ==");
            foreach (var r in _renders)
            {
                sb.AppendLine(r);
            }

            File.WriteAllText(ReportPath, sb.ToString());

            // Durable exit signal (see Poll): written only once the report is safely on disk, and
            // read by the editor loop, so a lost Finished delegate cannot strand the run.
            try
            {
                File.WriteAllText(CodePath, code.ToString(CultureInfo.InvariantCulture));
            }
            catch (Exception)
            {
                // the delegate below is the fallback
            }

            Debug.Log($"[AirlockTest] report written to {ReportPath} (result={code})");

            try
            {
                Finished?.Invoke(code);
            }
            catch (Exception e)
            {
                Debug.LogError("[AirlockTest] Finished handler threw: " + e);
            }
        }

        static string Sample(List<float> values, int index)
        {
            return index >= 0 && index < values.Count
                ? values[index].ToString("F2", CultureInfo.InvariantCulture)
                : "(out of range)";
        }

        static string Fmt(Vector3 v)
        {
            return $"({v.x.ToString("F2", CultureInfo.InvariantCulture)}," +
                   $"{v.y.ToString("F2", CultureInfo.InvariantCulture)})";
        }
    }
}
