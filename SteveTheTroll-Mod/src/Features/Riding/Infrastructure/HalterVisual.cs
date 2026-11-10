using KydlCore.Common;
using SteveTheTroll.Configuration;
using SteveTheTroll.Features.Riding.Domain;
using SteveTheTroll.Integrations.Unity;
using UnityEngine;

namespace SteveTheTroll.Features.Riding.Infrastructure;

/// <summary>
///     T-213: what a ridden troll wears. The bundle prefab HalterParts has BodyParts (the leather X, seat straps, seat; exported in the Spine1 bone's local space) and
///     HeadParts (bit, side rings, anchors for the two leashes; Head bone space). They are re-parented onto those bones, so they move with the troll's
///     animation. The leash is a leather line from each ring to the rider's hand. All placement offsets are Tunables (KydlTools, F7) read every frame.
///     Local only: other players do not see the halter yet.
/// </summary>
[DefaultExecutionOrder(-1000)]
internal sealed class HalterVisual : MonoBehaviour
{
    private Character _troll;
    private Transform _rider, _body, _head, _seatAnchor, _seatPoint;
    private Transform[] _leashFrom = new Transform[2], _leashTo = new Transform[2];
    private float _handErrorCm;

    /// Lab: how far (cm) the hands ended from where the reins want them, last frame.
    internal float HandErrorCm => _handErrorCm;
    private LineRenderer[] _lines = new LineRenderer[2];
    private Vector3 _bodyBase, _headBase, _seatSmooth, _lastPos;
    private bool _hasLastPos;
    private float _exactUntil;   // D-214: the seat follows the bone exactly until this time (an attack or a stagger is playing)
    private float _anchorGap;    // D-214: metres between the seat point and the bone anchor it hangs on, after this frame's follow step

    /// Lab / log: how far (m) the rider's seat point was from the bone anchor after the last frame (0 = the seat is exactly on the bone).
    internal float AnchorGap => _anchorGap;

    /// Lab / log: an attack or stagger is playing (or ended less than SeatFollow.DeliberateHoldSeconds ago), so the seat follows the bone exactly.
    internal bool FollowsExactly => Time.time < _exactUntil;

    /// Where the rider's pelvis goes (offset by the SeatX/Y/Z tunables).
    internal Transform SeatPoint => _seatPoint;

    /// Expected outcome: the halter is on the troll and has its anchors (null with the reason logged when the bundle part is missing).
    internal static HalterVisual Attach(Character troll, Player rider)
    {
        GameObject prefab = BundleAssets.Load<GameObject>(BundleAssets.HalterParts);
        if (prefab == null)
        {
            return null;
        }

        Transform spine = FindDeep(troll.transform, "Spine1");
        Transform head = FindDeep(troll.transform, "Head");
        if (spine == null || head == null)
        {
            ModLog.Error($"HalterVisual: troll bones not found (Spine1 {spine != null}, Head {head != null})");
            return null;
        }

        GameObject shell = Instantiate(prefab);
        Transform body = shell.transform.Find("BodyParts");
        Transform headParts = shell.transform.Find("HeadParts");
        if (body == null || headParts == null)
        {
            ModLog.Error("HalterVisual: HalterParts has no BodyParts / HeadParts");
            Destroy(shell);
            return null;
        }

        FixShaders(body);
        FixShaders(headParts);
        body.SetParent(spine, false);
        headParts.SetParent(head, false);
        Destroy(shell);
        var v = troll.gameObject.AddComponent<HalterVisual>();
        v._troll = troll;
        v._rider = rider != null ? rider.transform : null;
        v._body = body;
        v._head = headParts;
        v._bodyBase = body.localPosition;
        v._headBase = headParts.localPosition;
        v._seatAnchor = body.Find("STT_RideSeat");
        if (v._seatAnchor == null)
        {
            v._seatAnchor = body;
            ModLog.Warn("HalterVisual: STT_RideSeat missing in BodyParts - the seat is at the bone");
        }

        // the seat hangs on the troll's ROOT, not on the animated bone: the game moves the attached rider in FixedUpdate, a bone that bobs with the walk animation made the rider shake
        v._seatPoint = new GameObject("STT_RideSeatPoint").transform;
        v._seatPoint.SetParent(troll.transform, false);
        v._seatSmooth = troll.transform.InverseTransformPoint(v._seatAnchor.position);
        string[] hand = { "LeftHand", "RightHand" };
        string[] anchor = { "STT_LeashL", "STT_LeashR" };
        for (int i = 0; i < 2; i++)
        {
            v._leashFrom[i] = headParts.Find(anchor[i]);
            v._leashTo[i] = rider != null ? FindDeep(rider.transform, hand[i]) : null;
            v._lines[i] = v._leashFrom[i] != null ? v._leashFrom[i].GetComponent<LineRenderer>() : null;
            if (v._lines[i] == null || v._leashTo[i] == null)
            {
                ModLog.Warn($"HalterVisual: leash {i} not complete (anchor {v._leashFrom[i] != null}, line {v._lines[i] != null}, hand {v._leashTo[i] != null})");
            }
            else
            {
                v._lines[i].useWorldSpace = true;
                v._lines[i].positionCount = 28;
            }
        }

        v.Apply();
        // 2026-11-06 (user: the reins are not in the rider's hands): which attach animations the player's animator has - the sitting pose decides where the hands are
        if (rider != null)
        {
            var an = rider.GetComponentInChildren<Animator>();
            if (an != null)
            {
                var names = new System.Text.StringBuilder();
                foreach (AnimatorControllerParameter ap in an.parameters)
                {
                    if (ap.name.IndexOf("attach", System.StringComparison.OrdinalIgnoreCase) >= 0 || ap.name.IndexOf("ride", System.StringComparison.OrdinalIgnoreCase) >= 0 || ap.name.IndexOf("lox", System.StringComparison.OrdinalIgnoreCase) >= 0 || ap.name.IndexOf("saddle", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        names.Append(ap.name).Append(" (").Append(ap.type).Append(") ");
                    }
                }

                ModLog.Info($"HalterVisual: rider animator parameters about attaching: {names}; IK pass on layers: {an.layerCount}");
            }
        }

        ModLog.Info($"HalterVisual: halter on {troll.name} (parts {body.childCount} + {headParts.childCount}, leash hands {v._leashTo[0] != null && v._leashTo[1] != null})");
        return v;
    }

    private void LateUpdate()
    {
        if (_troll == null)
        {
            return;
        }

        Apply();
        // the game moves the rider to the seat only in its physics step; here the rider is pinned to the seat's CURRENT pose every frame (before the camera runs), so a walking troll cannot slide away from or through its rider
        Player p = Player.m_localPlayer;
        if (p != null && p.IsAttached() && RideSession.IsRidden(_troll))
        {
            p.transform.SetPositionAndRotation(_seatPoint.position, _seatPoint.rotation);
            if (ModSettings.RideHandsOn.Value)
            {
                HandsOnReins();
            }
        }

        ModLog.Every("HalterVisual.status", 10d, $"HalterVisual: halter on {_troll.name}, seat offset {ModSettings.RideSeatX.Value:F2}/{ModSettings.RideSeatY.Value:F2}/{ModSettings.RideSeatZ.Value:F2} m, leash sag {ModSettings.RideLeashSag.Value:F2} of its length");
    }

    // the seat parts are made narrower / wider along the troll's left-right axis (their own X); the base scale is remembered once
    private readonly System.Collections.Generic.Dictionary<Transform, Vector3> _seatScale = new System.Collections.Generic.Dictionary<Transform, Vector3>();

    private void SeatWidth()
    {
        if (_seatScale.Count == 0)
        {
            foreach (string n in new[] { "Seat", "SeatPad", "Cantle", "Pommel" })
            {
                Transform s = _body.Find(n);
                if (s != null)
                {
                    _seatScale[s] = s.localScale;
                }
            }

            if (_seatScale.Count == 0)
            {
                _seatScale[_body] = _body.localScale; // keeps the loop from searching every frame; nothing is scaled when no seat part exists
            }
        }

        float w = ModSettings.RideSeatWidth.Value;
        foreach (var kv in _seatScale)
        {
            if (kv.Key != null && kv.Key != _body)
            {
                kv.Key.localScale = new Vector3(kv.Value.x * w, kv.Value.y, kv.Value.z);
            }
        }
    }

    // 2026-11-06: two-bone arm IK after the animation has run: the upper arm and forearm are turned so each hand reaches the rein position in front of the seat, elbows out and down.
    private void HandsOnReins()
    {
        float err = 0f;
        for (int i = 0; i < 2; i++)
        {
            Transform hand = _leashTo[i];
            Transform fore = hand != null ? hand.parent : null;
            Transform upper = fore != null ? fore.parent : null;
            if (upper == null)
            {
                continue;
            }

            float side = i == 0 ? -1f : 1f; // 0 = left hand
            Vector3 target = _seatPoint.TransformPoint(new Vector3(side * ModSettings.RideHandX.Value, ModSettings.RideHandY.Value, ModSettings.RideHandZ.Value));
            Vector3 a = upper.position, b = fore.position, c = hand.position;
            float l1 = Vector3.Distance(a, b), l2 = Vector3.Distance(b, c);
            Vector3 toT = target - a;
            float d = Mathf.Clamp(toT.magnitude, Mathf.Abs(l1 - l2) + 0.01f, l1 + l2 - 0.005f);
            Vector3 dir = toT.sqrMagnitude > 1e-6f ? toT.normalized : _seatPoint.forward;
            // the elbow: along the line to the target by `along`, pushed out and down (the pole) by `h`
            float along = (d * d + l1 * l1 - l2 * l2) / (2f * d);
            float h = Mathf.Sqrt(Mathf.Max(0f, l1 * l1 - along * along));
            Vector3 pole = _seatPoint.TransformDirection(new Vector3(side * 0.5f, -0.8f, -0.2f)).normalized;
            pole = Vector3.ProjectOnPlane(pole, dir).normalized;
            Vector3 elbow = a + dir * along + pole * h;
            upper.rotation = Quaternion.FromToRotation(b - a, elbow - a) * upper.rotation;
            fore.rotation = Quaternion.FromToRotation(hand.position - fore.position, target - fore.position) * fore.rotation;
            err = Mathf.Max(err, Vector3.Distance(hand.position, target));
        }

        _handErrorCm = err * 100f;
    }
    private Vector3 World(float x, float y, float z)
    {
        Transform t = _troll.transform;
        return t.right * x + t.up * y + t.forward * z;
    }

    private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        return 0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    private void Apply()
    {
        _body.localPosition = _bodyBase + _body.parent.InverseTransformVector(World(ModSettings.RideBodyX.Value, ModSettings.RideBodyY.Value, ModSettings.RideBodyZ.Value));
        _head.localPosition = _headBase + _head.parent.InverseTransformVector(World(ModSettings.RideHeadX.Value, ModSettings.RideHeadY.Value, ModSettings.RideHeadZ.Value));
        // the anchor's place in the troll's own space (the rider is pinned to the seat every frame in LateUpdate); the offsets are in the troll's axes. A walking troll is followed exactly,
        // a standing one eased (D-202): its random idle clips move the spine fast and far, the rider jumped up to 22 cm in a frame
        Vector3 target = _troll.transform.InverseTransformPoint(_seatAnchor.position);
        Vector3 trollPos = _troll.transform.position;
        float dt = Time.deltaTime;
        float speed = _hasLastPos && dt > 0f ? new Vector2(trollPos.x - _lastPos.x, trollPos.z - _lastPos.z).magnitude / dt : 0f;
        _lastPos = trollPos;
        _hasLastPos = true;
        // D-208: eased AND capped in speed, so a huge gap to the bone (the Candy idle) cannot throw the rider in one frame
        // D-214: ... but never while the troll swings or staggers: the game pins the rider to the saddle exactly, and the basket on the bone moves with the animation
        if (_troll.InAttack() || _troll.IsStaggering())
        {
            _exactUntil = Time.time + SeatFollow.DeliberateHoldSeconds;
        }

        bool deliberate = Time.time < _exactUntil;
        _seatSmooth = Vector3.MoveTowards(_seatSmooth, Vector3.Lerp(_seatSmooth, target, SeatFollow.Share(speed, dt, deliberate)), SeatFollow.MaxStep(speed, dt, deliberate));
        _anchorGap = Vector3.Distance(_troll.transform.TransformPoint(_seatSmooth), _seatAnchor.position);
        _seatPoint.localPosition = _seatSmooth + new Vector3(ModSettings.RideSeatX.Value, ModSettings.RideSeatY.Value, ModSettings.RideSeatZ.Value);
        _seatPoint.localRotation = Quaternion.Euler(ModSettings.RideRiderPitch.Value, ModSettings.RideRiderYaw.Value, -ModSettings.RideRiderRoll.Value);
        SeatWidth();
        for (int i = 0; i < 2; i++)
        {
            LineRenderer lr = _lines[i];
            if (lr == null || _leashFrom[i] == null || _leashTo[i] == null)
            {
                continue;
            }

            // 2026-11-06 (user: the reins must go around the neck, not into it, and hook on the outside of the rings): the line runs ring -> three waypoints outside the head and neck (in the halter's own space, the troll has no collision for a line) -> hand
            Vector3 a = _leashFrom[i].position, b = _leashTo[i].position;
            float side = i == 0 ? -1f : 1f, wide = ModSettings.RideReinWiden.Value;
            Vector3[] way =
            {
                a,
                _head.TransformPoint(new Vector3(side * 1.25f * wide, 6.25f, 1.25f)),
                _head.TransformPoint(new Vector3(side * 1.05f * wide, 6.6f, 0.55f)),
                _head.TransformPoint(new Vector3(side * 0.7f * wide, 6.85f, 0.28f)),
                b,
            };
            int lastWay = way.Length - 1;
            float sag = ModSettings.RideLeashSag.Value * (b - way[lastWay - 1]).magnitude;
            for (int k = 0; k < lr.positionCount; k++)
            {
                float u = k / (float)(lr.positionCount - 1) * lastWay;
                int sg = Mathf.Min(lastWay - 1, (int)u);
                float lt = u - sg;
                Vector3 pos = CatmullRom(way[Mathf.Max(sg - 1, 0)], way[sg], way[sg + 1], way[Mathf.Min(sg + 2, lastWay)], lt);
                if (sg == lastWay - 1)
                {
                    pos += Vector3.down * (sag * 4f * lt * (1f - lt));
                }

                lr.SetPosition(k, pos);
            }
            lr.widthMultiplier = ModSettings.RideLeashWidth.Value;
        }
    }

    /// Takes the halter off again (parts and component). Expected outcome: the troll looks as before.
    internal void Remove()
    {
        if (_body != null)
        {
            Destroy(_body.gameObject);
        }

        if (_head != null)
        {
            Destroy(_head.gameObject);
        }

        Destroy(this);
    }

    /// Checks every renderer of the halter and logs what could come out pink in game: a slot without a material (the bit and the chest rings had none once), or a shader the player cannot run.
    /// Expected outcome: nothing is changed; the log says "all materials ok" when the bundle is fine.
    private static void FixShaders(Transform root)
    {
        int bad = 0;
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
        {
            foreach (Material m in r.sharedMaterials)
            {
                if (m == null || m.shader == null || !m.shader.isSupported)
                {
                    bad++;
                    if (bad <= 3)
                    {
                        ModLog.Warn($"HalterVisual: renderer {r.name} has {(m == null ? "no material" : $"material {m.name} with an unsupported shader")} - it will show pink");
                    }
                }
            }
        }

        ModLog.Info($"HalterVisual: {(bad == 0 ? "all materials ok" : $"{bad} material slot(s) will show pink")} on {root.name}");
    }
    internal static Transform FindDeep(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == name)
            {
                return t;
            }
        }

        return null;
    }
}
