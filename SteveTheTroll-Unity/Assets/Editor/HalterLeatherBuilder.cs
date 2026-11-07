using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Builds smooth, stitched leather straps (X harness, seat straps, preview reins) and procedural leather textures.
public static class HalterLeatherBuilder
{
    const string Dir = "Assets/SteveTheTroll_Ride/";
    const string Photos = @"E:\Projects\Valheim\Photos\SteveTheTroll\";
    const float W = 0.22f, T = 0.06f, R = 0.025f;
    const int TS = 512;

    static readonly List<Vector2> _pp = new List<Vector2>(), _pn = new List<Vector2>();
    static readonly List<float> _pu = new List<float>();
    static float _P;

    static void Add(Vector2 c, float deg)
    {
        float a = deg * Mathf.Deg2Rad; var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        _pp.Add(c + d * R); _pn.Add(d);
    }
    static void Arc(Vector2 c, float a0, float a1)
    {
        for (int k = 0; k < 4; k++) Add(c, Mathf.Lerp(a0, a1, k / 3f));
    }
    static void Profile()
    {
        _pp.Clear(); _pn.Clear(); _pu.Clear();
        float fh = W / 2 - R, th = T / 2 - R;
        var cTL = new Vector2(-fh, th); var cTR = new Vector2(fh, th); var cBR = new Vector2(fh, -th); var cBL = new Vector2(-fh, -th);
        Add(cTL, 90);
        Arc(cTR, 90, 0); Arc(cBR, 0, -90); Arc(cBL, -90, -180); Arc(cTL, 180, 90);
        float acc = 0; _pu.Add(0);
        for (int i = 1; i < _pp.Count; i++) { acc += Vector2.Distance(_pp[i], _pp[i - 1]); _pu.Add(acc); }
        _P = acc;
        for (int i = 0; i < _pu.Count; i++) _pu[i] /= _P;
    }

    // ---------- path helpers
    static Vector3 CR(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        return 0.5f * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (-p0 + 3 * p1 - 3 * p2 + p3) * t3);
    }
    static List<Vector3> Resample(List<Vector3> p, bool closed, float step)
    {
        var path = new List<Vector3>(p); if (closed) path.Add(p[0]);
        var cum = new List<float> { 0 };
        for (int i = 1; i < path.Count; i++) cum.Add(cum[i - 1] + Vector3.Distance(path[i], path[i - 1]));
        float total = cum[cum.Count - 1];
        int cnt = Mathf.Max(closed ? 8 : 2, Mathf.RoundToInt(total / step));
        var res = new List<Vector3>(); int seg = 0;
        int last = closed ? cnt - 1 : cnt;
        for (int j = 0; j <= last; j++)
        {
            float tg = total * j / cnt;
            while (seg < path.Count - 2 && cum[seg + 1] < tg) seg++;
            float L = Mathf.Max(1e-5f, cum[seg + 1] - cum[seg]);
            res.Add(Vector3.Lerp(path[seg], path[seg + 1], Mathf.Clamp01((tg - cum[seg]) / L)));
        }
        return res;
    }
    static Mesh Ribbon(string name, List<Vector3> ctrl, bool closed, System.Func<Vector3, Vector3> hint, float scale, float step, int smoothPasses, float push)
    {
        var pts = new List<Vector3>(ctrl);
        for (int i = pts.Count - 1; i > 0; i--) if ((pts[i] - pts[i - 1]).sqrMagnitude < 1e-4f) pts.RemoveAt(i);
        if (closed && pts.Count > 1 && (pts[0] - pts[pts.Count - 1]).sqrMagnitude < 1e-4f) pts.RemoveAt(pts.Count - 1);
        pts = Resample(pts, closed, step);
        for (int pass = 0; pass < smoothPasses; pass++)
        {
            var np = new List<Vector3>(pts);
            for (int i = 0; i < pts.Count; i++)
            {
                if (!closed && (i == 0 || i == pts.Count - 1)) continue;
                var a = pts[(i - 1 + pts.Count) % pts.Count]; var b = pts[(i + 1) % pts.Count];
                np[i] = Vector3.Lerp(pts[i], (a + b) * 0.5f, 0.5f);
            }
            pts = np;
        }
        if (push != 0) for (int i = 0; i < pts.Count; i++) pts[i] += hint(pts[i]).normalized * push;
        int n = pts.Count; int segs = closed ? n : n - 1;
        var dense = new List<Vector3>();
        for (int s = 0; s < segs; s++)
        {
            var p0 = pts[closed ? (s - 1 + n) % n : Mathf.Max(s - 1, 0)]; var p1 = pts[s]; var p2 = pts[(s + 1) % n];
            var p3 = pts[closed ? (s + 2) % n : Mathf.Min(s + 2, n - 1)];
            for (int k = 0; k < 3; k++) dense.Add(CR(p0, p1, p2, p3, k / 3f));
        }
        if (!closed) dense.Add(pts[n - 1]);
        int m = dense.Count; int rings = closed ? m + 1 : m; int K = _pp.Count;
        var vcum = new float[rings];
        for (int i = 1; i < rings; i++) vcum[i] = vcum[i - 1] + Vector3.Distance(dense[i % m], dense[i - 1]);
        var verts = new Vector3[rings * K]; var nors = new Vector3[rings * K]; var uvs = new Vector2[rings * K];
        for (int i = 0; i < rings; i++)
        {
            var c = dense[i % m];
            var prev = closed ? dense[(i - 1 + m) % m] : dense[Mathf.Max(i - 1, 0)];
            var next = closed ? dense[(i + 1) % m] : dense[Mathf.Min(i + 1, m - 1)];
            var tan = (next - prev).normalized;
            var h = hint(c); var B = Vector3.Cross(h, tan);
            if (B.sqrMagnitude < 1e-3f) B = Vector3.Cross(Vector3.up, tan);
            B.Normalize(); var N = Vector3.Cross(tan, B).normalized;
            for (int k = 0; k < K; k++)
            {
                int vi = i * K + k;
                verts[vi] = c + B * (_pp[k].x * scale) + N * (_pp[k].y * scale);
                nors[vi] = (B * _pn[k].x + N * _pn[k].y).normalized;
                uvs[vi] = new Vector2(_pu[k], vcum[i] / (_P * scale));
            }
        }
        var tris = new List<int>();
        for (int i = 0; i < rings - 1; i++)
            for (int k = 0; k < K - 1; k++)
            {
                int a = i * K + k, b = i * K + k + 1, c2 = (i + 1) * K + k, d = (i + 1) * K + k + 1;
                tris.Add(a); tris.Add(c2); tris.Add(b); tris.Add(b); tris.Add(c2); tris.Add(d);
            }
        var mesh = new Mesh { name = name, indexFormat = verts.Length > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
        mesh.vertices = verts; mesh.normals = nors; mesh.uv = uvs; mesh.triangles = tris.ToArray();
        mesh.RecalculateBounds(); mesh.RecalculateTangents();
        return mesh;
    }

    // ---------- procedural leather textures
    static uint Hash(int x, int y, int seed)
    {
        uint h = (uint)(x * 374761393 + y * 668265263 + seed * 2147483647);
        h = (h ^ (h >> 13)) * 1274126177u; return h ^ (h >> 16);
    }
    static void Cell(float x, float y, int n, int seed, out float f1, out float f2)
    {
        int cx = Mathf.FloorToInt(x), cy = Mathf.FloorToInt(y); f1 = 9; f2 = 9;
        for (int j = -1; j <= 1; j++)
            for (int i = -1; i <= 1; i++)
            {
                int gx = cx + i, gy = cy + j;
                int wx = ((gx % n) + n) % n, wy = ((gy % n) + n) % n;
                uint h = Hash(wx, wy, seed);
                float px = gx + (h & 0xFFFF) / 65535f, py = gy + ((h >> 16) & 0xFFFF) / 65535f;
                float d = Mathf.Sqrt((px - x) * (px - x) + (py - y) * (py - y));
                if (d < f1) { f2 = f1; f1 = d; } else if (d < f2) f2 = d;
            }
    }
    static float Sm(float a, float b, float x) { x = Mathf.Clamp01((x - a) / (b - a)); return x * x * (3 - 2 * x); }

    static void MakeSet(string name, bool stitched, bool transposed, Color baseCol)
    {
        float u1 = _pu[1];
        var alb = new Color[TS * TS]; var hgt = new float[TS * TS];
        float[] stitchU = { 0.012f / _P, u1 - 0.012f / _P };
        for (int y = 0; y < TS; y++)
            for (int x = 0; x < TS; x++)
            {
                float tx = (x + 0.5f) / TS, ty = (y + 0.5f) / TS;
                float u = transposed ? ty : tx, v = transposed ? tx : ty;
                float f1, f2, g1, g2, m1, m2;
                Cell(u * 40, v * 40, 40, 1, out f1, out f2);
                Cell(u * 150, v * 150, 150, 2, out g1, out g2);
                Cell(u * 6, v * 6, 6, 3, out m1, out m2);
                float crease = Sm(0.0f, 0.2f, f2 - f1);
                float pore = Sm(0.0f, 0.22f, g1);
                float mottle = 0.88f + 0.24f * Mathf.Clamp01(m1);
                float shade = (0.62f + 0.38f * crease) * (0.82f + 0.18f * pore) * mottle;
                float h = 0.55f * crease + 0.12f * pore;
                float edge = 0;
                if (stitched)
                {
                    if (u <= u1) { float dd = Mathf.Min(u, u1 - u); edge = 0.18f * (1 - Sm(0, 0.03f, dd)); }
                    else { float dd = Mathf.Min(u - u1, 1 - u); edge = Mathf.Lerp(0.18f, 0.45f, Sm(0, 0.05f, dd)); }
                }
                var col = baseCol * shade * (1 - edge); col.a = 1;
                if (stitched)
                {
                    float phase = (v * 16f) % 1f;
                    foreach (float s in stitchU)
                    {
                        float along = Mathf.Max(0, Mathf.Abs(phase - 0.5f) * (_P / 16f) - 0.007f);
                        float across = (u - s) * _P;
                        float dist = Mathf.Sqrt(along * along + across * across);
                        if (dist < 0.0016f) { col = Color.Lerp(col, new Color(0.74f, 0.62f, 0.44f, 1) * (0.85f + 0.15f * pore), 0.95f); h += 0.25f; }
                        else if (dist < 0.0034f) { col *= 0.55f; h -= 0.9f * (1 - Sm(0.0016f, 0.0034f, dist)); }
                    }
                }
                alb[y * TS + x] = col; hgt[y * TS + x] = h;
            }
        var nrm = new Color[TS * TS];
        for (int y = 0; y < TS; y++)
            for (int x = 0; x < TS; x++)
            {
                float hl = hgt[y * TS + (x + TS - 1) % TS], hr = hgt[y * TS + (x + 1) % TS];
                float hd = hgt[((y + TS - 1) % TS) * TS + x], hu = hgt[((y + 1) % TS) * TS + x];
                var n = new Vector3((hl - hr) * 2.2f, (hd - hu) * 2.2f, 1f).normalized;
                nrm[y * TS + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1);
            }
        Save($"{name}_A.png", alb, false); Save($"{name}_N.png", nrm, true);
    }
    static void Save(string file, Color[] px, bool normal)
    {
        var t = new Texture2D(TS, TS, TextureFormat.RGBA32, false); t.SetPixels(px); t.Apply();
        string p = Dir + file; File.WriteAllBytes(p, t.EncodeToPNG()); Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate);
        var ti = (TextureImporter)AssetImporter.GetAtPath(p);
        ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        ti.wrapMode = TextureWrapMode.Repeat; ti.mipmapEnabled = true; ti.SaveAndReimport();
    }
    static void SetupMat(string matName, string tex, Color tint, float smooth)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(Dir + matName);
        m.shader = Shader.Find("Standard"); m.color = tint;
        m.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + tex + "_A.png");
        m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + tex + "_N.png"));
        m.EnableKeyword("_NORMALMAP"); m.SetFloat("_BumpScale", 1f);
        m.SetFloat("_Glossiness", smooth); m.SetFloat("_Metallic", 0f);
        m.mainTextureScale = Vector2.one; EditorUtility.SetDirty(m);
    }

    // ---------- stored control paths
    class PathData { public List<Vector3> pts = new List<Vector3>(); public List<Vector3> nrm = new List<Vector3>(); public bool closed; }
    static string PathFile { get { return $"{Dir}halter_paths.txt"; } }
    static Dictionary<string, PathData> LoadOrExtract(Transform root)
    {
        var d = new Dictionary<string, PathData>();
        if (File.Exists(PathFile))
        {
            foreach (var line in File.ReadAllLines(PathFile))
            {
                var parts = line.Split('|'); if (parts.Length < 3) continue;
                var pd = new PathData { closed = parts[1] == "closed" };
                if (parts.Length > 3) foreach (var s in parts[3].Split(';')) { var c = s.Split(','); if (c.Length == 3) pd.nrm.Add(new Vector3(float.Parse(c[0], CultureInfo.InvariantCulture), float.Parse(c[1], CultureInfo.InvariantCulture), float.Parse(c[2], CultureInfo.InvariantCulture))); }
                foreach (var s in parts[2].Split(';')) { var c = s.Split(','); if (c.Length == 3) pd.pts.Add(new Vector3(float.Parse(c[0], CultureInfo.InvariantCulture), float.Parse(c[1], CultureInfo.InvariantCulture), float.Parse(c[2], CultureInfo.InvariantCulture))); }
                d[parts[0]] = pd;
            }
            return d;
        }
        d["XA"] = Extract(root, "Strap0_", true, false); d["XB"] = Extract(root, "Strap1_", true, false);
        d["SeatL"] = Extract(root, "SeatStrap-1_", false, true); d["SeatR"] = Extract(root, "SeatStrap1_", false, true);
        var sb = new StringBuilder();
        foreach (var kv in d)
        {
            sb.Append(kv.Key).Append('|').Append(kv.Value.closed ? "closed" : "open").Append('|');
            for (int i = 0; i < kv.Value.pts.Count; i++) { var p = kv.Value.pts[i]; sb.Append(p.x.ToString("F4", CultureInfo.InvariantCulture)).Append(',').Append(p.y.ToString("F4", CultureInfo.InvariantCulture)).Append(',').Append(p.z.ToString("F4", CultureInfo.InvariantCulture)); if (i < kv.Value.pts.Count - 1) sb.Append(';'); }
            sb.AppendLine();
        }
        File.WriteAllText(PathFile, sb.ToString());
        return d;
    }
    static PathData Extract(Transform root, string prefix, bool closed, bool includeEnd)
    {
        var pd = new PathData { closed = closed }; var seen = new HashSet<string>(); Vector3 last = Vector3.zero; float maxGap = 0;
        foreach (Transform c in root)
        {
            if (!c.name.StartsWith(prefix) || !seen.Add(c.name)) continue;
            float half = c.lossyScale.z * 0.5f; var st = c.position - c.forward * half;
            if (pd.pts.Count > 0) maxGap = Mathf.Max(maxGap, Vector3.Distance(st, last));
            pd.pts.Add(st); last = c.position + c.forward * half;
        }
        if (includeEnd) pd.pts.Add(last);
        Debug.Log($"[Leather] {prefix} pts={pd.pts.Count} maxGap={maxGap:F3}");
        return pd;
    }

    static GameObject MakeObj(string name, Mesh mesh, Material mat, Transform parent)
    {
        var g = new GameObject(name); g.transform.SetParent(parent, false);
        g.AddComponent<MeshFilter>().sharedMesh = mesh; g.AddComponent<MeshRenderer>().sharedMaterial = mat; return g;
    }
    static Mesh SaveMesh(Mesh m)
    {
        string p = $"{Dir}Meshes/{m.name}.asset";
        if (!AssetDatabase.IsValidFolder($"{Dir}Meshes")) AssetDatabase.CreateFolder(Dir.TrimEnd('/'), "Meshes");
        AssetDatabase.DeleteAsset(p); AssetDatabase.CreateAsset(m, p); return AssetDatabase.LoadAssetAtPath<Mesh>(p);
    }
    // 2026-11-06 (user: the seat is too wide): absolute widths (X scale), so running the builder again never narrows it twice
    static void NarrowSeat(Transform c)
    {
        float x = c.name == "Seat" ? 0.62f : c.name == "SeatPad" ? 0.52f : c.name == "Cantle" ? 0.56f : c.localScale.x;
        var s = c.localScale; s.x = x; c.localScale = s;
    }
    static bool IsOld(string n) { return n.StartsWith("Strap") || n.StartsWith("SeatStrap") || n.StartsWith("Rein") || n.StartsWith("XStrap"); }
    static void Shot(Camera cam, string file, Vector3 pos, Vector3 look, float fov)
    {
        cam.transform.position = pos; cam.transform.LookAt(look); cam.fieldOfView = fov;
        var rt = new RenderTexture(1280, 960, 24); cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
        var tex = new Texture2D(1280, 960); tex.ReadPixels(new Rect(0, 0, 1280, 960), 0, 0); tex.Apply();
        File.WriteAllBytes(Photos + file + ".png", tex.EncodeToPNG());
        cam.targetTexture = null; RenderTexture.active = null; Object.DestroyImmediate(rt); Object.DestroyImmediate(tex);
    }

    // 2026-11-06 (user): "T_YFMeM_23 for metal, T_Leather_PatternM_01_MS for leather" - the pack textures replace the procedural ones on the halter materials
    static void Cap(string path)
    {
        var ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti != null && ti.maxTextureSize > 1024) { ti.maxTextureSize = 1024; ti.SaveAndReimport(); }
    }
    // 2026-11-06: where the reins pass on their way from the rings to the hands - outside the troll's head and neck (head half-width ~0.8 at y 6.4-7.0, jaw ~1.0 at y 6.2). Keep in step with HalterVisual.ReinWay.
    static Vector3[] ReinWay(float s) => new[] { new Vector3(s * 1.25f, 6.25f, 1.25f), new Vector3(s * 1.05f, 6.6f, 0.55f), new Vector3(s * 0.7f, 6.85f, 0.28f) };

    // 2026-11-06 (user): the seat frame (Seat, Cantle, Pommel) is T_YFFlM_02, only the cushion (SeatPad) is the leather
    static Material FrameMat()
    {
        const string Y = "Assets/SteveTheTroll_Ride/PackTextures/T_YFFlM_02_";
        Cap($"{Y}d.tga"); Cap($"{Y}n.tga");
        string p = $"{Dir}HalterSeatFrame.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m == null) { m = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(m, p); }
        m.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>($"{Y}d.tga"));
        m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{Y}n.tga")); m.EnableKeyword("_NORMALMAP");
        m.SetColor("_Color", Color.white); m.SetFloat("_Metallic", 0f); m.SetFloat("_Glossiness", 0.3f);
        EditorUtility.SetDirty(m); AssetDatabase.SaveAssets();
        return m;
    }
    static void PackLook()
    {
        const string L = "Assets/SteveTheTroll_Ride/PackTextures/T_Leather_PatternM_01_";
        const string Y = "Assets/SteveTheTroll_Ride/PackTextures/T_YFMeM_23_";
        foreach (var s in new[] { "BC", "N", "MS", "AO" }) Cap(L + s + ".png");
        Cap($"{Y}d.tga"); Cap($"{Y}n.tga");
        var bc = AssetDatabase.LoadAssetAtPath<Texture2D>($"{L}BC.png"); var nm = AssetDatabase.LoadAssetAtPath<Texture2D>($"{L}N.png");
        var ms = AssetDatabase.LoadAssetAtPath<Texture2D>($"{L}MS.png"); var ao = AssetDatabase.LoadAssetAtPath<Texture2D>($"{L}AO.png");
        foreach (var mn in new[] { "HalterLeather.mat", "HalterRein.mat", "HalterSeat.mat" })
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(Dir + mn);
            m.SetTexture("_MainTex", bc); m.SetTexture("_BumpMap", nm); m.EnableKeyword("_NORMALMAP"); m.SetFloat("_BumpScale", 1f);
            m.SetTexture("_MetallicGlossMap", ms); m.EnableKeyword("_METALLICGLOSSMAP"); m.SetTexture("_OcclusionMap", ao); m.EnableKeyword("_OCCLUSIONMAP");
            m.SetColor("_Color", Color.white); m.SetFloat("_Metallic", 0f); m.SetFloat("_Glossiness", 0.5f); m.SetFloat("_GlossMapScale", 1f); m.SetFloat("_OcclusionStrength", 1f);
            EditorUtility.SetDirty(m);
        }        var metal = AssetDatabase.LoadAssetAtPath<Material>($"{Dir}HalterMetal.mat");
        metal.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>($"{Y}d.tga"));
        metal.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{Y}n.tga")); metal.EnableKeyword("_NORMALMAP");
        metal.SetColor("_Color", Color.white); metal.SetFloat("_Metallic", 0f); metal.SetFloat("_Glossiness", 0.62f);
        EditorUtility.SetDirty(metal);
        AssetDatabase.SaveAssets();
    }
    [MenuItem("Valheim/Halter/Rebuild Leather")]
    public static void Run()
    {
        Profile();
        var troll = GameObject.Find("Troll"); var root = troll.transform.Find("STT_TrollHalter");
        var paths = LoadOrExtract(root);
        var seatMat = AssetDatabase.LoadAssetAtPath<Material>($"{Dir}HalterSeat.mat");
        var seatCol = new Color(0.33f, 0.18f, 0.09f, 1);
        MakeSet("LeatherStrap", true, false, new Color(0.40f, 0.215f, 0.105f, 1));
        MakeSet("LeatherRein", true, true, new Color(0.40f, 0.215f, 0.105f, 1));
        MakeSet("LeatherGrain", false, false, seatCol);
        AssetDatabase.Refresh();
        SetupMat("HalterLeather.mat", "LeatherStrap", Color.white, 0.32f);
        SetupMat("HalterRein.mat", "LeatherRein", Color.white, 0.32f);
        SetupMat("HalterSeat.mat", "LeatherGrain", Color.white, 0.28f);
        AssetDatabase.SaveAssets();
        var leather = AssetDatabase.LoadAssetAtPath<Material>($"{Dir}HalterLeather.mat");
        var metal = AssetDatabase.LoadAssetAtPath<Material>($"{Dir}HalterMetal.mat"); // 2026-11-06 (user): the two seat straps are metal, not leather
        seatMat = AssetDatabase.LoadAssetAtPath<Material>($"{Dir}HalterSeat.mat");
        PackLook(); // 2026-11-06 (user): metal = T_YFMeM_23, leather = T_Leather_PatternM_01 (Leather Materials Pack)

        System.Func<Vector3, Vector3> radial = p => new Vector3(p.x, 0, p.z - 0.1f);
        // 2026-11-06 (user: the shoulder straps must lie flat on top of the shoulders, underside on the shoulder, not on edge): the strap's thickness axis follows the shoulder surface normal stored in halter_paths.txt
        System.Func<string, System.Func<Vector3, Vector3>> surface = key => p =>
        {
            var pd = paths[key]; if (pd.nrm.Count != pd.pts.Count) return radial(p);
            int bi = 0; float bd = float.MaxValue; for (int i = 0; i < pd.pts.Count; i++) { float d = (pd.pts[i] - p).sqrMagnitude; if (d < bd) { bd = d; bi = i; } }
            return pd.nrm[bi];
        };
        var meshes = new Dictionary<string, Mesh>();
        meshes["XStrapA"] = SaveMesh(Ribbon("XStrapA", paths["XA"].pts, true, surface("XA"), 1f, 0.12f, 3, 0f));
        meshes["XStrapB"] = SaveMesh(Ribbon("XStrapB", paths["XB"].pts, true, surface("XB"), 1f, 0.12f, 3, 0f));
        meshes["SeatStrapL"] = SaveMesh(Ribbon("SeatStrapL", paths["SeatL"].pts, false, radial, 0.77f, 0.12f, 3, 0.01f));
        meshes["SeatStrapR"] = SaveMesh(Ribbon("SeatStrapR", paths["SeatR"].pts, false, radial, 0.77f, 0.12f, 3, 0.01f));

        // scene: replace old parts
        var kill = new List<GameObject>(); foreach (Transform c in root) if (IsOld(c.name)) kill.Add(c.gameObject);
        foreach (var g in kill) Object.DestroyImmediate(g);
        foreach (var kv in meshes) MakeObj(kv.Key, kv.Value, kv.Key.StartsWith("SeatStrap") ? metal : leather, root);
        foreach (Transform c in root) if (c.name == "Seat" || c.name == "SeatPad" || c.name == "Cantle" || c.name == "Pommel") { c.GetComponent<Renderer>().sharedMaterial = c.name == "SeatPad" ? seatMat : FrameMat(); NarrowSeat(c); }

        // preview reins from ring knots to the rider's hands
        var player = GameObject.Find("RefPlayer"); var bit = root.Find("Bit");
        // 2026-11-06 (user: the reins go to the circles at the mouth, not to the top sphere): the preview reins start on the rim of the side rings, the point nearest to the seat (the same rule the leash anchors in the prefab use)
        var knots = new List<Vector3>();
        foreach (string rn in new[] { "SideRing-1", "SideRing1" })
        {
            var pieces = new List<Vector3>(); foreach (Transform c in bit) if (c.name == rn) { var r = c.GetComponent<Renderer>(); if (r != null) pieces.Add(r.bounds.center); }
            if (pieces.Count == 0) { knots.Add(Vector3.zero); continue; }
            Vector3 centre = Vector3.zero; foreach (var p in pieces) centre += p; centre /= pieces.Count;
            Vector3 dir = (ReinWay(rn == "SideRing-1" ? -1f : 1f)[0] - centre).normalized;
            float bd = -float.MaxValue; Vector3 best = centre; foreach (var p in pieces) { float d = Vector3.Dot(p - centre, dir); if (d > bd) { bd = d; best = p; } }
            knots.Add(best + dir * 0.03f);
        }
        string[] hn = { "LeftHand", "RightHand" };
        for (int h = 0; h < 2 && h < knots.Count; h++)
        {
            float s = h == 0 ? -1f : 1f; Transform hand = null;
            foreach (var t in player.GetComponentsInChildren<Transform>()) if (t.name == hn[h]) hand = t;
            var a0 = knots[h];
            var way = ReinWay(s); var pl = new List<Vector3> { a0, way[0], way[1], way[2], hand.position }; // 2026-11-06 (user: the reins must go around the neck, not through it): the same waypoints HalterVisual uses in game // 2026-11-06: straight from the ring to the hand with a little sag (as in game)
            float ss = s; var m = Ribbon($"Rein{h}", pl, false, p => new Vector3(ss, 0.2f, 0), 0.42f, 0.1f, 2, 0);
            MakeObj($"Rein{h}", m, leather, root);
        }

        // prefab
        var pc = PrefabUtility.LoadPrefabContents($"{Dir}HalterParts.prefab");
        var body = pc.transform.Find("BodyParts");
        var pk = new List<GameObject>(); foreach (Transform c in body) if (c.name.StartsWith("Strap") || c.name.StartsWith("SeatStrap") || c.name.StartsWith("XStrap")) pk.Add(c.gameObject);
        foreach (var g in pk) Object.DestroyImmediate(g);
        foreach (var kv in meshes) MakeObj(kv.Key, kv.Value, kv.Key.StartsWith("SeatStrap") ? metal : leather, body);
        foreach (Transform c in body) if (c.name == "Seat" || c.name == "SeatPad" || c.name == "Cantle" || c.name == "Pommel") { c.GetComponent<Renderer>().sharedMaterial = c.name == "SeatPad" ? seatMat : FrameMat(); NarrowSeat(c); }
        PrefabUtility.SaveAsPrefabAsset(pc, $"{Dir}HalterParts.prefab"); PrefabUtility.UnloadPrefabContents(pc);

        EditorSceneManager.SaveOpenScenes();
        var cam = GameObject.Find("Cam").GetComponent<Camera>();
        string ts = $"{System.DateTime.Now:yyyyMMdd-HHmm}_ride-leather-";
        Shot(cam, $"{ts}side", new Vector3(14, 7.5f, 1.5f), new Vector3(0, 6.5f, 0.5f), 34);
        Shot(cam, $"{ts}front34", new Vector3(7, 8f, 8f), new Vector3(0, 6.3f, 0.6f), 34);
        Shot(cam, $"{ts}chest", new Vector3(0, 5.2f, 9f), new Vector3(0, 4.6f, 1f), 30);
        Shot(cam, $"{ts}back", new Vector3(5, 7f, -9f), new Vector3(0, 5.2f, -0.5f), 34);
        Debug.Log("[Leather] done");
    }
}

