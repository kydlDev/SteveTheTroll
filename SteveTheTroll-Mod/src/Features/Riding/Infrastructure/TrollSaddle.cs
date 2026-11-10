using KydlCore.Common;
using SteveTheTroll.Features.Riding.Domain;
using SteveTheTroll.Features.Trolls.Combat.Infrastructure;
using UnityEngine;

namespace SteveTheTroll.Features.Riding.Infrastructure;

/// <summary>
///     T-213: put on a ridden troll (modelled on the game's Sadle). The player's controls reach the troll through IDoodadController: the troll's own AI is
///     skipped while this component is there (RidingPatches), so it only does what the rider says. The mouse buttons are the troll's attacks (left = a swing,
///     secondary = the overhead / chopping attack).
/// </summary>
internal sealed class TrollSaddle : MonoBehaviour, IDoodadController
{
    private Character _troll;
    private Humanoid _human;
    private Transform _seat;
    private Vector3 _lastMove;
    private bool _autoOn, _prevAuto;

    /// Lab: the autorun toggle as the saddle sees it.
    internal bool AutoRunOn => _autoOn;

    /// True while the saddle itself sets the troll's move direction (the trace patch ignores that).
    internal static bool Setting;

    /// True while the saddle itself starts an attack (the block patch lets that one through).
    internal static bool Attacking;

    internal Transform Seat => _seat;

    // Lab only: how well the rider keeps to the seat: the biggest gap between the player and the seat point, and the biggest jump of the rider inside the troll's frame from one frame to the next.
    private bool _sampling;
    private int _samples;
    private float _gapMax, _stepMax;
    private Vector3 _lastLocal;
    private float _lastYaw;

    /// Lab only: what the troll's animator plays (clip, normalised time, transition), whether it attacks and how fast it moves - for the big-step log.
    private string AnimState()
    {
        Animator an = _troll != null ? _troll.GetComponentInChildren<Animator>() : null;
        if (an == null)
        {
            return "no animator";
        }

        AnimatorClipInfo[] clips = an.GetCurrentAnimatorClipInfo(0);
        string clip = clips.Length > 0 && clips[0].clip != null ? clips[0].clip.name : "-";
        return $"{clip} {an.GetCurrentAnimatorStateInfo(0).normalizedTime:F2}{(an.IsInTransition(0) ? " (in transition)" : "")}{(_human != null && _human.InAttack() ? ", attacking" : "")}, speed {_troll.GetVelocity().magnitude:F2} m/s";
    }
    private readonly System.Text.StringBuilder _big = new System.Text.StringBuilder();
    private int _bigCount;
    private float _sampleStart;
    private float _anchorGapMax, _anchorGapAttackMax;   // D-214: seat point vs the bone anchor (m), overall and while the troll attacks
    private int _attackFrames;
    private float _speedMax;   // D-214: the fastest ground speed of the ridden troll since the last Sample() (m/s); the rider asked for a run (Shift / autorun) in _lastRun
    private bool _lastRun;
    private CameraProbe _probe;
    private int _camFrames, _camStutter;
    private float _camSum, _camSumSq, _camMax;
    private Vector3 _camLast;
    private readonly System.Collections.Generic.List<float> _camSteps = new System.Collections.Generic.List<float>();

    internal float GapCm => _gapMax * 100f;

    internal float StepCm => _stepMax * 100f;

    /// D-214: the biggest distance (cm) between the rider's seat point and the bone anchor it follows since the sampling started (the old rideGap compares the rider with the seat point, which is
    /// pinned to it by construction - it could not see a seat that stays behind the bone).
    internal float AnchorGapCm => _anchorGapMax * 100f;

    /// D-214: the same, only for frames in which the troll was in an attack.
    internal float AnchorGapAttackCm => _anchorGapAttackMax * 100f;

    /// D-214: frames sampled while the troll was in an attack (0 = the lab never saw an attack, so the attack gap proves nothing).
    internal int AttackFrames => _attackFrames;

    /// D-214 (user 2026-11-10: "are the trolls walking slower than 125 % / 150 % when I ride them? it feels so slow"): the fastest speed seen while ridden, in cm/s.
    internal float SpeedMaxCm => _speedMax * 100f;

    /// Lab only: the saddle presses the primary attack button by itself while on.
    internal bool TestAttack { get; set; }

    /// Lab only: start / stop collecting (LateUpdate, after the rider was pinned to the seat).
    internal void Sample(bool on)
    {
        _sampling = on;
        _samples = 0;
        _camFrames = 0;
        _camSteps.Clear();
        _camStutter = 0;
        _camSum = _camSumSq = _camMax = 0f;
        if (on && _probe == null)
        {
            _probe = gameObject.AddComponent<CameraProbe>();
            _probe.Owner = this;
        }
        else if (!on && _probe != null)
        {
            Destroy(_probe);
            _probe = null;
        }

        _gapMax = 0f;
        _stepMax = 0f;
        _anchorGapMax = 0f;
        _anchorGapAttackMax = 0f;
        _attackFrames = 0;
        _speedMax = 0f;
        _big.Length = 0;
        _bigCount = 0;
        _sampleStart = Time.time;
    }

    internal string SampleReport()
    {
        _camFrames = _camSteps.Count;
        var sorted = new System.Collections.Generic.List<float>(_camSteps);
        sorted.Sort();
        float mean = sorted.Count > 0 ? sorted[sorted.Count / 2] : 0f; // the MEDIAN step (one camera jump at the start must not spoil it)
        float sd = 0f;
        _camStutter = 0;
        _camMax = sorted.Count > 0 ? sorted[sorted.Count - 1] : 0f;
        for (int i = 30; i < _camSteps.Count; i++)
        {
            float d = _camSteps[i];
            if (mean > 0.004f && (d < mean * 0.25f || d > mean * 2.5f))
            {
                _camStutter++;
            }
        }
        Rigidbody rb = _troll != null ? _troll.GetComponent<Rigidbody>() : null;
        Rigidbody pb = Player.m_localPlayer != null ? Player.m_localPlayer.GetComponent<Rigidbody>() : null;
        string cam = $" | camera moved {(mean * 100f):F2} cm per frame (median; spread {(sd * 100f):F2}, biggest {(_camMax * 100f):F2} cm), stutter frames (under a quarter or over 2.5x the mean) {_camStutter} of {_camFrames}; troll body interpolation {(rb != null ? rb.interpolation.ToString() : "none")}, player body interpolation {(pb != null ? pb.interpolation.ToString() : "none")}, fixed step {(Time.fixedDeltaTime * 1000f):F0} ms, vsync {QualitySettings.vSyncCount}";
        return $"rider off the seat by at most {GapCm:F1} cm, seat off the bone by at most {AnchorGapCm:F1} cm (while attacking {AnchorGapAttackCm:F1} cm in {_attackFrames} frames), biggest jump between two frames {StepCm:F1} cm (in the troll frame), {_samples} frames; jumps over 4 cm: {_bigCount}{_big}{cam}";
    }

    private void LateUpdate()
    {
        Player p = Player.m_localPlayer;
        if (!_sampling || p == null || _troll == null || _seat == null)
        {
            return;
        }

        Vector3 local = _troll.transform.InverseTransformPoint(p.transform.position);
        if (_samples++ > 0)
        {
            float step = Vector3.Distance(local, _lastLocal);
            _stepMax = Mathf.Max(_stepMax, step);
            // D-201 (g16b-2 ride-shake-still: 14 cm, the log did not say what the troll's body was doing): every big step names the troll's animation, how far its body turned since the last
            // frame and its speed, so a bone animation can be told from a turning body or a late rider
            float turn = Mathf.Abs(Mathf.DeltaAngle(_lastYaw, _troll.transform.eulerAngles.y));
            if (step > 0.04f && _bigCount++ < 6)
            {
                _big.Append($" [t {(Time.time - _sampleStart):F2} step {(step * 100f):F1} cm, dt {(Time.deltaTime * 1000f):F0} ms, delta {(local - _lastLocal):F2}, body turned {turn:F2} deg, anim {AnimState()}]");
            }
        }

        _lastYaw = _troll.transform.eulerAngles.y;
        _lastLocal = local;
        _gapMax = Mathf.Max(_gapMax, Vector3.Distance(p.transform.position, _seat.position));
        HalterVisual halter = _troll.GetComponent<HalterVisual>();
        if (halter != null)
        {
            _anchorGapMax = Mathf.Max(_anchorGapMax, halter.AnchorGap);
            _speedMax = Mathf.Max(_speedMax, new Vector2(_troll.GetVelocity().x, _troll.GetVelocity().z).magnitude);
            if (_human != null && _human.InAttack())
            {
                _attackFrames++;
                _anchorGapAttackMax = Mathf.Max(_anchorGapAttackMax, halter.AnchorGap);
            }
        }
    }
    // Lab only (user 2026-11-06 "there is some jitter still"): how smoothly the CAMERA moves while riding - sampled after the game camera has been placed for the frame.
    private void CameraFrame()
    {
        if (!_sampling || GameCamera.instance == null)
        {
            return;
        }

        Vector3 c = GameCamera.instance.transform.position;
        if (_camFrames > 0 || _camLast != Vector3.zero)
        {
            _camSteps.Add(Vector3.Distance(c, _camLast));
            _camFrames++;
        }

        _camLast = c;
    }

    [DefaultExecutionOrder(32000)]
    private sealed class CameraProbe : MonoBehaviour
    {
        internal TrollSaddle Owner;

        private void LateUpdate()
        {
            if (Owner != null)
            {
                Owner.CameraFrame();
            }
        }
    }

    /// The last movement the rider asked for, as a world direction (the lab reads it).
    internal Vector3 LastMove => _lastMove;

    /// Lab only: walk forward as if W were held.
    internal bool TestDrive { get; set; }

    /// Lab: the rider holds the run key while the saddle drives the troll (D-214: a ridden troll must reach its run speed, 125 % / 150 % of vanilla).
    internal bool TestRun { get; set; }

    /// Expected outcome: the troll and the seat transform are remembered.
    internal bool Setup(Character troll, Transform seat)
    {
        _troll = troll;
        _human = troll.GetComponent<Humanoid>();
        _seat = seat;
        // 2026-11-06 (lab ride-cam: troll body interpolation None, player body Interpolate, fixed step 20 ms, frames 6 - 12 ms): the troll root moved in 50 Hz steps, so the seat, the rider and the camera stepped
        // with it = the jitter the user sees. A ridden troll is interpolated like the player; the old setting comes back when the saddle goes.
        _body = troll.GetComponent<Rigidbody>();
        if (_body != null)
        {
            _oldInterpolation = _body.interpolation;
            _body.interpolation = RigidbodyInterpolation.Interpolate;
        }

        ModLog.Verbose($"TrollSaddle: seat on {troll.name} at {(seat != null ? seat.name : "-")}");
        return _seat != null;
    }

    private static bool InputFree()
    {
        return !(Chat.instance != null && Chat.instance.HasFocus()) && !Console.IsVisible() && !Menu.IsVisible() &&
               !(InventoryGui.instance != null && InventoryGui.IsVisible()) && !TextInput.IsVisible();
    }

    private readonly System.Collections.Generic.List<MonoBehaviour> _suspended = new System.Collections.Generic.List<MonoBehaviour>();
    private float _nextSuspend;

    /// The mod's own troll behaviours (farming, recovery, return home, rage, slam, chase, arrival) would drive the troll under its rider; they are switched off while it is ridden
    /// and switched on again when the ride ends. New ones that start meanwhile (an order, a hit) are caught within half a second.
    private void SuspendBehaviours()
    {
        if (Time.time < _nextSuspend || _troll == null)
        {
            return;
        }

        _nextSuspend = Time.time + 0.5f;
        foreach (MonoBehaviour mb in _troll.GetComponents<MonoBehaviour>())
        {
            string ns = mb.GetType().Namespace;
            if (mb.enabled && ns != null && ns.StartsWith("SteveTheTroll.Features.Trolls.") && !mb.GetType().Name.StartsWith("Backpack"))
            {
                mb.enabled = false;
                _suspended.Add(mb);
                ModLog.Info($"TrollSaddle: {mb.GetType().Name} paused on the ridden {_troll.name}");
            }
        }
    }

    private Rigidbody _body;
    private RigidbodyInterpolation _oldInterpolation;

    private void OnDestroy()
    {
        if (_body != null)
        {
            _body.interpolation = _oldInterpolation;
        }

        foreach (MonoBehaviour mb in _suspended)
        {
            if (mb != null)
            {
                mb.enabled = true;
            }
        }

        _suspended.Clear();
    }
    private void Update()
    {
        SuspendBehaviours();
        if (_human == null || _troll == null || !InputFree())
        {
            return;
        }

        var view = _troll.GetComponent<ZNetView>();
        ModLog.Every("TrollSaddle.detail", 2d, $"TrollSaddle: detail - owner {view != null && view.IsValid() && view.IsOwner()}, move dir in effect {_troll.GetMoveDir():F2}, last asked {_lastMove:F2}, run asked {_lastRun}, running {_troll.IsRunning()}, run speed in use {_troll.m_runSpeed:F1} jog {_troll.m_speed:F1} m/s, can move {_troll.CanMove()}, staggering {_troll.IsStaggering()}, in attack {_human.InAttack()}, on ground {_troll.IsOnGround()}, speed {_troll.GetVelocity().magnitude:F2} m/s, swimming {_troll.IsSwimming()}, tamed {_troll.IsTamed()}");
        ModLog.Every("TrollSaddle.status", 10d, $"TrollSaddle: {_troll.name} ridden, last move {_lastMove:F2}, attacking {_human.InAttack()}");

        // the mouse buttons attack with the troll's own weapon; held = keeps attacking
        bool primary = ZInput.GetButton("Attack") || ZInput.GetButton("JoyAttack") || TestAttack;
        bool secondary = ZInput.GetButton("SecondaryAttack") || ZInput.GetButton("JoyAltKeys") && ZInput.GetButton("JoyAttack");
        if ((primary || secondary) && !_human.InAttack())
        {
            // the troll's attacks are its items, not the weapon's secondary mode: left = a horizontal swing (punch / log swing), secondary = the overhead / chopping one when it has one
            ItemDrop.ItemData weapon = secondary
                ? TrollWeapons.Pick(_human, Attack.AttackType.Vertical, true) ?? TrollWeapons.Pick(_human, Attack.AttackType.Vertical, false)
                : TrollWeapons.Pick(_human, Attack.AttackType.Horizontal, false);
            if (weapon != null && !_human.IsItemEquiped(weapon))
            {
                _human.EquipItem(weapon, false);
            }

            Attacking = true;
            bool started = weapon != null && _human.StartAttack(null, false);
            Attacking = false;
            ModLog.Every("TrollSaddle.attack", 1d, $"TrollSaddle: {(secondary ? "secondary" : "primary")} attack asked - started {started}, weapon {(weapon != null ? weapon.m_shared.m_name : "none")}");
        }
    }

    /// The rider's controls: walk, look, run. The troll's attacks come from the mouse buttons (Update), not from here.
    public void ApplyControlls(Vector3 moveDir, Vector3 lookDir, bool run, bool autoRun, bool block)
    {
        if (_troll == null)
        {
            return;
        }

        if (TestDrive)
        {
            moveDir = _troll.transform.forward;
            lookDir = _troll.transform.forward;
        }

        // the game hands over the input in the rider's own frame (x right, z forward, like the Lox saddle reads it): turn it into a world direction along the camera
        Vector3 flat = lookDir;
        flat.y = 0f;
        flat = flat.sqrMagnitude > 0.0001f ? flat.normalized : _troll.transform.forward;
        Vector3 world = Vector3.zero;
        // 2026-11-06 (user: "the troll does not follow autorun"): the game clears the autorun flag for a doodad controller, so the toggle lives here: the key switches it on / off, S or block switches it off,
        // and while it is on the troll walks the way the camera looks when no key is held
        if (autoRun && !_prevAuto)
        {
            _autoOn = !_autoOn;
        }

        _prevAuto = autoRun;
        if (block || moveDir.z < -0.5f)
        {
            _autoOn = false;
        }

        if (TestDrive)
        {
            world = _troll.transform.forward;
        }
        else if (moveDir.sqrMagnitude > 0.01f && !(moveDir.z < -0.5f && Mathf.Abs(moveDir.x) < 0.5f))
        {
            // S alone stops (a troll does not walk backwards); anything else walks the way the camera and keys say
            world = (Quaternion.LookRotation(flat) * moveDir).normalized;
        }

        if (_autoOn && world == Vector3.zero && !TestDrive)
        {
            world = flat;
        }

        _lastMove = world;
        Setting = true;
        _troll.SetMoveDir(world);
        Setting = false;
        _lastRun = (run || _autoOn || TestRun) && world != Vector3.zero;
        _troll.SetRun(_lastRun);
        _troll.SetLookDir(world != Vector3.zero && !(_human != null && _human.InAttack()) ? world : flat);
    }

    /// The game calls this when the rider lets go of the control (the Use key): the ride ends like a normal dismount.
    public void OnUseStop(Player player)
    {
        ModLog.Info($"TrollSaddle: the game released the rider's control (Use key or too far from the seat: {(player != null && _seat != null ? Vector3.Distance(player.transform.position, _seat.position).ToString("F1") : "?")} m) - the ride ends");
        RideSession.End(player, EndReason.PlayerDismounted);
    }

    public Component GetControlledComponent() => _troll;

    public Vector3 GetPosition()
    {
        // the game stops doodad control when the rider is further than the interact distance from this point: the seat is where the rider is, the troll's feet are 6 m below
        return _seat != null ? _seat.position : _troll != null ? _troll.transform.position : transform.position;
    }

    public bool IsValid() => _troll != null && !_troll.IsDead();
}
