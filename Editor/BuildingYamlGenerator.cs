#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Linq;

/// <summary>
/// Unity 씬에서 OpenRMF building.yaml을 자동 생성하는 에디터 도구.
///
/// 필요한 태그 설정:
///   - "Wall"    : 벽 오브젝트 (BoxCollider 필수)
///   - "Pickup"  : 픽업 지점
///   - "Dropoff" : 드롭오프 지점
///   - "Charger" : 충전기 지점
///   - "Door"    : 문 오브젝트 (BoxCollider 필수)
///
/// 메뉴: OpenRMF → Generate Building YAML
/// </summary>
public class BuildingYamlGenerator : EditorWindow
{
    string buildingName = "building";
    string levelName = "L1";
    float gridResolution = 1.5f;
    float wallPadding = 0.5f;
    bool bidirectionalLanes = true;
    float namedPointConnectionRadius = 3.0f;
    string outputPath = "";
    Vector2 scrollPos;

    [MenuItem("OpenRMF/Generate Building YAML")]
    static void ShowWindow()
    {
        var w = GetWindow<BuildingYamlGenerator>("Building YAML Generator");
        w.minSize = new Vector2(400, 500);
    }

    void OnEnable()
    {
        if (string.IsNullOrEmpty(outputPath))
            outputPath = Path.Combine(Application.dataPath, "..", "building.yaml");
    }

    void OnGUI()
    {
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        EditorGUILayout.LabelField("OpenRMF Building YAML Generator", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUILayout.LabelField("기본 설정", EditorStyles.boldLabel);
        buildingName = EditorGUILayout.TextField("Building 이름", buildingName);
        levelName = EditorGUILayout.TextField("Level 이름", levelName);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("내비게이션 그리드", EditorStyles.boldLabel);
        gridResolution = EditorGUILayout.Slider("그리드 간격 (m)", gridResolution, 0.5f, 5.0f);
        wallPadding = EditorGUILayout.Slider("벽 패딩 (m)", wallPadding, 0.1f, 2.0f);
        bidirectionalLanes = EditorGUILayout.Toggle("양방향 레인", bidirectionalLanes);
        namedPointConnectionRadius = EditorGUILayout.Slider("포인트 연결 반경 (m)", namedPointConnectionRadius, 1.0f, 10.0f);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("출력", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        outputPath = EditorGUILayout.TextField("저장 경로", outputPath);
        if (GUILayout.Button("...", GUILayout.Width(30)))
        {
            string dir = Path.GetDirectoryName(outputPath);
            string file = Path.GetFileName(outputPath);
            string path = EditorUtility.SaveFilePanel("Building YAML 저장", dir, file, "yaml");
            if (!string.IsNullOrEmpty(path)) outputPath = path;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        DrawSceneStatus();

        EditorGUILayout.Space();
        GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
        if (GUILayout.Button("Building YAML 생성", GUILayout.Height(40)))
        {
            Generate();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndScrollView();
    }

    void DrawSceneStatus()
    {
        EditorGUILayout.LabelField("씬 오브젝트 현황", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        DrawTagCount("Wall");
        DrawTagCount("Pickup");
        DrawTagCount("Dropoff");
        DrawTagCount("Charger");
        DrawTagCount("Door");

        var robots = Object.FindObjectsByType<TinyRobotRMFController>(FindObjectsSortMode.None);
        EditorGUILayout.LabelField($"TinyRobotRMFController: {robots.Length}개");
        EditorGUI.indentLevel--;
    }

    void DrawTagCount(string tag)
    {
        int count = 0;
        try { count = GameObject.FindGameObjectsWithTag(tag).Length; }
        catch { }
        string color = count > 0 ? "green" : "red";
        EditorGUILayout.LabelField($"{tag}: ", $"<color={color}>{count}개</color>",
            new GUIStyle(EditorStyles.label) { richText = true });
    }

    // ─── Data structures ───

    struct WallRect
    {
        public Vector2 min, max;
        public Vector2[] corners;
    }

    struct NamedPoint
    {
        public Vector2 pos;
        public string name;
        public enum PointType { Pickup, Dropoff, Charger, Parking }
        public PointType type;
    }

    struct DoorInfo
    {
        public Vector2 p1, p2;
        public string name;
    }

    // ─── Main generation ───

    void Generate()
    {
        var walls = CollectWalls();
        var namedPoints = CollectNamedPoints();
        var doors = CollectDoors();
        var floorBounds = ComputeFloorBounds(walls);

        var vertices = new List<float[]>();
        var vertexNames = new List<string>();
        var vertexParams = new List<Dictionary<string, string>>();

        var wallSegments = new List<int[]>();
        var laneSegments = new List<int[]>();
        var floorVertexIndices = new List<int>();

        // 1) 벽 코너 vertices + wall segments
        int floorStartIdx = vertices.Count;
        foreach (var w in walls)
        {
            int baseIdx = vertices.Count;
            for (int i = 0; i < 4; i++)
            {
                vertices.Add(new float[] { w.corners[i].x, w.corners[i].y, 0 });
                vertexNames.Add("");
                vertexParams.Add(null);
            }
            wallSegments.Add(new[] { baseIdx, baseIdx + 1 });
            wallSegments.Add(new[] { baseIdx + 1, baseIdx + 2 });
            wallSegments.Add(new[] { baseIdx + 2, baseIdx + 3 });
            wallSegments.Add(new[] { baseIdx + 3, baseIdx });
        }

        // 2) 바닥 폴리곤 (외곽 벽 경계)
        var outerCorners = ComputeOuterFloorPolygon(floorBounds);
        foreach (var c in outerCorners)
        {
            floorVertexIndices.Add(vertices.Count);
            vertices.Add(new float[] { c.x, c.y, 0 });
            vertexNames.Add("");
            vertexParams.Add(null);
        }

        // 3) Door vertices
        var doorEntries = new List<int[]>();
        foreach (var d in doors)
        {
            int i1 = vertices.Count;
            vertices.Add(new float[] { d.p1.x, d.p1.y, 0 });
            vertexNames.Add("");
            vertexParams.Add(null);

            int i2 = vertices.Count;
            vertices.Add(new float[] { d.p2.x, d.p2.y, 0 });
            vertexNames.Add("");
            vertexParams.Add(null);

            doorEntries.Add(new[] { i1, i2 });
        }

        // 4) Named points (pickup, dropoff, charger)
        var namedVertexIndices = new Dictionary<int, NamedPoint>();
        foreach (var np in namedPoints)
        {
            int idx = vertices.Count;
            vertices.Add(new float[] { np.pos.x, np.pos.y, 0 });
            vertexNames.Add(np.name);

            var p = new Dictionary<string, string>();
            switch (np.type)
            {
                case NamedPoint.PointType.Pickup:
                    p["pickup_dispenser"] = $"[1, {np.name}]";
                    break;
                case NamedPoint.PointType.Dropoff:
                    p["dropoff_ingestor"] = $"[1, {np.name}]";
                    break;
                case NamedPoint.PointType.Charger:
                    p["is_charger"] = "[4, true]";
                    p["is_parking_spot"] = "[4, true]";
                    break;
                case NamedPoint.PointType.Parking:
                    p["is_parking_spot"] = "[4, true]";
                    break;
            }
            vertexParams.Add(p.Count > 0 ? p : null);
            namedVertexIndices[idx] = np;
        }

        // 5) 내비게이션 그리드
        int gridStartIdx = vertices.Count;
        var gridMap = new Dictionary<(int, int), int>();
        float xMin = floorBounds.x + wallPadding;
        float xMax = floorBounds.x + floorBounds.width - wallPadding;
        float yMin = floorBounds.y + wallPadding;
        float yMax = floorBounds.y + floorBounds.height - wallPadding;

        int cols = Mathf.FloorToInt((xMax - xMin) / gridResolution) + 1;
        int rows = Mathf.FloorToInt((yMax - yMin) / gridResolution) + 1;

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                float gx = xMin + c * gridResolution;
                float gy = yMin + r * gridResolution;
                if (!IsBlockedByWall(new Vector2(gx, gy), walls, wallPadding))
                {
                    int idx = vertices.Count;
                    vertices.Add(new float[] { gx, gy, 0 });
                    vertexNames.Add("");
                    vertexParams.Add(null);
                    gridMap[(c, r)] = idx;
                }
            }
        }

        // 6) 그리드 레인 연결 (4방향 + 대각선, 벽 관통 검사 포함)
        int[][] dirs = new[] {
            new[] { 1, 0 }, new[] { 0, 1 },
            new[] { 1, 1 }, new[] { 1, -1 }
        };

        int blockedLaneCount = 0;
        foreach (var kv in gridMap)
        {
            int cx = kv.Key.Item1, cy = kv.Key.Item2;
            int fromIdx = kv.Value;
            Vector2 fromPos = new Vector2(vertices[fromIdx][0], vertices[fromIdx][1]);

            foreach (var d in dirs)
            {
                var key = (cx + d[0], cy + d[1]);
                if (gridMap.TryGetValue(key, out int toIdx))
                {
                    Vector2 toPos = new Vector2(vertices[toIdx][0], vertices[toIdx][1]);
                    if (SegmentIntersectsWalls(fromPos, toPos, walls, wallPadding))
                    {
                        blockedLaneCount++;
                    }
                    else
                    {
                        laneSegments.Add(new[] { fromIdx, toIdx });
                    }
                }
            }
        }
        Debug.Log($"[BuildingYAML] 벽 관통 검사: {blockedLaneCount}개 레인 차단됨");

        // 7) Named points → 가장 가까운 그리드 포인트에 연결 (벽 관통 검사 포함)
        foreach (var kv in namedVertexIndices)
        {
            int npIdx = kv.Key;
            Vector2 npPos = kv.Value.pos;
            float bestDist = float.MaxValue;
            int bestGridIdx = -1;

            foreach (var gkv in gridMap)
            {
                int gIdx = gkv.Value;
                float gx = vertices[gIdx][0];
                float gy = vertices[gIdx][1];
                Vector2 gPos = new Vector2(gx, gy);
                float dist = Vector2.Distance(npPos, gPos);
                if (dist < bestDist && dist <= namedPointConnectionRadius
                    && !SegmentIntersectsWalls(npPos, gPos, walls, wallPadding))
                {
                    bestDist = dist;
                    bestGridIdx = gIdx;
                }
            }

            if (bestGridIdx >= 0)
            {
                laneSegments.Add(new[] { npIdx, bestGridIdx });
            }
            else
            {
                Debug.LogWarning($"[BuildingYAML] '{kv.Value.name}'에서 반경 {namedPointConnectionRadius}m 내 벽을 관통하지 않는 그리드 포인트를 찾지 못했습니다.");
            }
        }

        // 8) YAML 출력
        float xMeters = floorBounds.width;
        float yMeters = floorBounds.height;

        string yaml = BuildYaml(vertices, vertexNames, vertexParams,
            wallSegments, laneSegments, doorEntries, floorVertexIndices,
            doors, xMeters, yMeters);

        File.WriteAllText(outputPath, yaml, new UTF8Encoding(false));
        Debug.Log($"[BuildingYAML] 생성 완료: {outputPath}\n" +
                  $"  vertices: {vertices.Count}, walls: {wallSegments.Count}, " +
                  $"lanes: {laneSegments.Count}, named: {namedPoints.Count}");
        EditorUtility.RevealInFinder(outputPath);
    }

    // ─── Scene data collection ───

    List<WallRect> CollectWalls()
    {
        var result = new List<WallRect>();
        GameObject[] wallObjs;
        try { wallObjs = GameObject.FindGameObjectsWithTag("Wall"); }
        catch { return result; }

        var processed = new HashSet<int>();

        foreach (var go in wallObjs)
        {
            var col = go.GetComponent<BoxCollider>();
            if (col == null) continue;

            int id = col.GetInstanceID();
            if (processed.Contains(id)) continue;
            processed.Add(id);

            var t = col.transform;
            Vector3 center = t.TransformPoint(col.center);
            Vector3 size = Vector3.Scale(col.size, t.lossyScale);

            float halfX = Mathf.Abs(size.x) * 0.5f;
            float halfZ = Mathf.Abs(size.z) * 0.5f;

            if (halfX < 0.01f && halfZ < 0.01f) continue;

            float rot = t.eulerAngles.y * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rot);
            float sin = Mathf.Sin(rot);

            Vector2 cx = new Vector2(center.x, center.z);
            Vector2 dx = new Vector2(cos * halfX, sin * halfX);
            Vector2 dz = new Vector2(-sin * halfZ, cos * halfZ);

            var corners = new Vector2[4];
            corners[0] = cx - dx - dz;
            corners[1] = cx + dx - dz;
            corners[2] = cx + dx + dz;
            corners[3] = cx - dx + dz;

            float mnx = corners.Min(p => p.x);
            float mxx = corners.Max(p => p.x);
            float mny = corners.Min(p => p.y);
            float mxy = corners.Max(p => p.y);

            result.Add(new WallRect
            {
                min = new Vector2(mnx, mny),
                max = new Vector2(mxx, mxy),
                corners = corners
            });
        }

        return result;
    }

    List<NamedPoint> CollectNamedPoints()
    {
        var result = new List<NamedPoint>();
        int pickupIdx = 0, dropoffIdx = 0, chargerIdx = 0;

        CollectByTag("Pickup", NamedPoint.PointType.Pickup, ref pickupIdx, "pick", result);
        CollectByTag("Dropoff", NamedPoint.PointType.Dropoff, ref dropoffIdx, "drop", result);
        CollectByTag("Charger", NamedPoint.PointType.Charger, ref chargerIdx, "charger", result);

        // TinyRobotRMFController의 로봇 위치를 parking spot으로 추가
        var robots = Object.FindObjectsByType<TinyRobotRMFController>(FindObjectsSortMode.None);
        foreach (var robot in robots)
        {
            Vector3 pos = robot.transform.position;
            result.Add(new NamedPoint
            {
                pos = new Vector2(pos.x, pos.z),
                name = $"{robot.RobotName}_parking",
                type = NamedPoint.PointType.Parking
            });
        }

        return result;
    }

    void CollectByTag(string tag, NamedPoint.PointType type, ref int idx, string prefix, List<NamedPoint> result)
    {
        GameObject[] objs;
        try { objs = GameObject.FindGameObjectsWithTag(tag); }
        catch { return; }

        var sorted = objs.OrderBy(o => o.name).ToArray();
        foreach (var go in sorted)
        {
            Vector3 wpos = go.transform.position;
            idx++;
            result.Add(new NamedPoint
            {
                pos = new Vector2(wpos.x, wpos.z),
                name = $"{prefix}_{idx}",
                type = type
            });
        }
    }

    List<DoorInfo> CollectDoors()
    {
        var result = new List<DoorInfo>();
        GameObject[] doorObjs;
        try { doorObjs = GameObject.FindGameObjectsWithTag("Door"); }
        catch { return result; }

        int di = 0;
        foreach (var go in doorObjs)
        {
            var col = go.GetComponent<BoxCollider>();
            if (col == null) continue;

            var t = col.transform;
            Vector3 center = t.TransformPoint(col.center);
            Vector3 size = Vector3.Scale(col.size, t.lossyScale);
            float halfX = size.x * 0.5f;
            float halfZ = size.z * 0.5f;

            Vector2 p1, p2;
            if (Mathf.Abs(size.x) > Mathf.Abs(size.z))
            {
                p1 = new Vector2(center.x - halfX, center.z);
                p2 = new Vector2(center.x + halfX, center.z);
            }
            else
            {
                p1 = new Vector2(center.x, center.z - halfZ);
                p2 = new Vector2(center.x, center.z + halfZ);
            }

            di++;
            result.Add(new DoorInfo { p1 = p1, p2 = p2, name = go.name.Length > 0 ? go.name : $"door_{di}" });
        }

        return result;
    }

    Rect ComputeFloorBounds(List<WallRect> walls)
    {
        if (walls.Count == 0)
        {
            Debug.LogWarning("[BuildingYAML] 벽이 없습니다. 기본 바운드를 사용합니다.");
            return new Rect(-35, -18, 70, 36);
        }

        float xMin = walls.Min(w => w.min.x);
        float xMax = walls.Max(w => w.max.x);
        float yMin = walls.Min(w => w.min.y);
        float yMax = walls.Max(w => w.max.y);
        return new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
    }

    Vector2[] ComputeOuterFloorPolygon(Rect bounds)
    {
        return new[]
        {
            new Vector2(bounds.xMin, bounds.yMin),
            new Vector2(bounds.xMax, bounds.yMin),
            new Vector2(bounds.xMax, bounds.yMax),
            new Vector2(bounds.xMin, bounds.yMax),
        };
    }

    bool IsBlockedByWall(Vector2 point, List<WallRect> walls, float pad)
    {
        foreach (var w in walls)
        {
            if (point.x >= w.min.x - pad && point.x <= w.max.x + pad &&
                point.y >= w.min.y - pad && point.y <= w.max.y + pad)
                return true;
        }
        return false;
    }

    bool SegmentIntersectsWalls(Vector2 p1, Vector2 p2, List<WallRect> walls, float pad)
    {
        foreach (var w in walls)
        {
            // 1) 실제 벽 모서리(회전 포함)와 선분 교차 검사
            for (int i = 0; i < 4; i++)
            {
                if (LineSegmentsIntersect(p1, p2, w.corners[i], w.corners[(i + 1) % 4]))
                    return true;
            }

            // 2) 패딩 적용한 AABB 교차 검사 (벽 조각 사이 갭 처리)
            float halfPad = pad * 0.5f;
            if (SegmentIntersectsAABB(p1, p2,
                new Vector2(w.min.x - halfPad, w.min.y - halfPad),
                new Vector2(w.max.x + halfPad, w.max.y + halfPad)))
                return true;
        }

        // 3) 선분 위 중간 지점들을 샘플링하여 벽 내부인지 검사
        float segLen = Vector2.Distance(p1, p2);
        int sampleCount = Mathf.Max(Mathf.CeilToInt(segLen / (pad * 0.4f)), 4);
        for (int s = 1; s < sampleCount; s++)
        {
            float t = (float)s / sampleCount;
            Vector2 pt = Vector2.Lerp(p1, p2, t);
            if (IsBlockedByWall(pt, walls, 0f))
                return true;
        }

        return false;
    }

    static bool SegmentIntersectsAABB(Vector2 p1, Vector2 p2, Vector2 bmin, Vector2 bmax)
    {
        Vector2 d = p2 - p1;
        float tmin = 0f;
        float tmax = 1f;

        for (int axis = 0; axis < 2; axis++)
        {
            float origin = axis == 0 ? p1.x : p1.y;
            float dir = axis == 0 ? d.x : d.y;
            float lo = axis == 0 ? bmin.x : bmin.y;
            float hi = axis == 0 ? bmax.x : bmax.y;

            if (Mathf.Abs(dir) < 1e-8f)
            {
                if (origin < lo || origin > hi) return false;
            }
            else
            {
                float invD = 1f / dir;
                float t1 = (lo - origin) * invD;
                float t2 = (hi - origin) * invD;
                if (t1 > t2) { float tmp = t1; t1 = t2; t2 = tmp; }
                tmin = Mathf.Max(tmin, t1);
                tmax = Mathf.Min(tmax, t2);
                if (tmin > tmax) return false;
            }
        }

        return true;
    }

    static bool LineSegmentsIntersect(Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2)
    {
        float d1 = Cross2D(b2 - b1, a1 - b1);
        float d2 = Cross2D(b2 - b1, a2 - b1);
        float d3 = Cross2D(a2 - a1, b1 - a1);
        float d4 = Cross2D(a2 - a1, b2 - a1);

        if (((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) &&
            ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0)))
            return true;

        if (Mathf.Abs(d1) < 1e-6f && PointOnSegment(b1, b2, a1)) return true;
        if (Mathf.Abs(d2) < 1e-6f && PointOnSegment(b1, b2, a2)) return true;
        if (Mathf.Abs(d3) < 1e-6f && PointOnSegment(a1, a2, b1)) return true;
        if (Mathf.Abs(d4) < 1e-6f && PointOnSegment(a1, a2, b2)) return true;

        return false;
    }

    static float Cross2D(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

    static bool PointOnSegment(Vector2 segA, Vector2 segB, Vector2 pt)
    {
        return pt.x >= Mathf.Min(segA.x, segB.x) - 1e-5f &&
               pt.x <= Mathf.Max(segA.x, segB.x) + 1e-5f &&
               pt.y >= Mathf.Min(segA.y, segB.y) - 1e-5f &&
               pt.y <= Mathf.Max(segA.y, segB.y) + 1e-5f;
    }

    // ─── YAML output ───

    string BuildYaml(
        List<float[]> vertices, List<string> vNames, List<Dictionary<string, string>> vParams,
        List<int[]> wallSegs, List<int[]> laneSegs, List<int[]> doorSegs,
        List<int> floorVerts, List<DoorInfo> doorInfos,
        float xMeters, float yMeters)
    {
        var sb = new StringBuilder();

        // coordinate_system
        sb.AppendLine("coordinate_system: cartesian_meters");

        // crowd_sim (minimal)
        sb.AppendLine("crowd_sim:");
        sb.AppendLine("  agent_groups:");
        sb.AppendLine("    - {agents_name: [], agents_number: 0, group_id: 0, profile_selector: external_agent, state_selector: external_static, x: 0, y: 0}");
        sb.AppendLine("  agent_profiles:");
        sb.AppendLine("    - {ORCA_tau: 1, ORCA_tauObst: 0.4, class: 1, max_accel: 0, max_angle_vel: 0, max_neighbors: 10, max_speed: 0, name: external_agent, neighbor_dist: 5, obstacle_set: 1, pref_speed: 0, r: 0.25}");
        sb.AppendLine("  enable: 0");
        sb.AppendLine("  goal_sets: []");
        sb.AppendLine("  model_types: []");
        sb.AppendLine($"  obstacle_set: {{class: 1, file_name: {levelName}_navmesh.nav, type: nav_mesh}}");
        sb.AppendLine("  states:");
        sb.AppendLine("    - {final: 1, goal_set: -1, name: external_static, navmesh_file_name: \"\"}");
        sb.AppendLine("  transitions: []");
        sb.AppendLine("  update_time_step: 0.1");

        // graphs
        sb.AppendLine("graphs:");
        sb.AppendLine("  {}");

        // levels
        sb.AppendLine("levels:");
        sb.AppendLine($"  {levelName}:");

        // doors
        sb.AppendLine("    doors:");
        if (doorSegs.Count == 0)
        {
            sb.AppendLine("      []");
        }
        else
        {
            for (int i = 0; i < doorSegs.Count; i++)
            {
                string dname = i < doorInfos.Count ? doorInfos[i].name : $"door_{i}";
                sb.AppendLine($"      - [{doorSegs[i][0]}, {doorSegs[i][1]}, {{motion_axis: [1, start], motion_degrees: [3, 90], motion_direction: [2, 1], name: [1, \"{dname}\"], plugin: [1, normal], right_left_ratio: [3, 1], type: [1, hinged]}}]");
            }
        }

        // elevation
        sb.AppendLine("    elevation: 0");

        // floors
        sb.AppendLine("    floors:");
        if (floorVerts.Count > 0)
        {
            sb.AppendLine("      - parameters: {ceiling_scale: [3, 1], ceiling_texture: [1, blue_linoleum], indoor: [2, 0], texture_name: [1, blue_linoleum], texture_rotation: [3, 0], texture_scale: [3, 1]}");
            sb.Append("        vertices: [");
            sb.Append(string.Join(", ", floorVerts));
            sb.AppendLine("]");
        }
        else
        {
            sb.AppendLine("      []");
        }

        // lanes
        sb.AppendLine("    lanes:");
        if (laneSegs.Count == 0)
        {
            sb.AppendLine("      []");
        }
        else
        {
            foreach (var l in laneSegs)
            {
                string bidir = bidirectionalLanes ? "true" : "false";
                sb.AppendLine($"      - [{l[0]}, {l[1]}, {{bidirectional: [4, {bidir}], demo_mock_floor_name: [1, \"\"], demo_mock_lift_name: [1, \"\"], graph_idx: [2, 0], orientation: [1, \"\"], speed_limit: [3, 0]}}]");
            }
        }

        // layers
        sb.AppendLine("    layers:");
        sb.AppendLine("      {}");

        // measurements
        sb.AppendLine("    measurements:");
        sb.AppendLine("      []");

        // vertices
        sb.AppendLine("    vertices:");
        for (int i = 0; i < vertices.Count; i++)
        {
            float[] v = vertices[i];
            string name = vNames[i];
            var prm = vParams[i];

            string nameStr = string.IsNullOrEmpty(name) ? "\"\"" : name;

            if (prm != null && prm.Count > 0)
            {
                string paramStr = string.Join(", ", prm.Select(kv => $"{kv.Key}: {kv.Value}"));
                sb.AppendLine($"      - [{F(v[0])}, {F(v[1])}, {F(v[2])}, {nameStr}, {{{paramStr}}}]");
            }
            else
            {
                sb.AppendLine($"      - [{F(v[0])}, {F(v[1])}, {F(v[2])}, {nameStr}]");
            }
        }

        // walls
        sb.AppendLine("    walls:");
        if (wallSegs.Count == 0)
        {
            sb.AppendLine("      []");
        }
        else
        {
            foreach (var w in wallSegs)
            {
                sb.AppendLine($"      - [{w[0]}, {w[1]}, {{alpha: [3, 1], texture_height: [3, 2.5], texture_name: [1, default], texture_scale: [3, 1], texture_width: [3, 1]}}]");
            }
        }

        // x_meters, y_meters
        sb.AppendLine($"    x_meters: {F(xMeters)}");
        sb.AppendLine($"    y_meters: {F(yMeters)}");

        // lifts
        sb.AppendLine("lifts: {}");

        // name
        sb.AppendLine($"name: {buildingName}");

        return sb.ToString();
    }

    static string F(float v)
    {
        if (v == (int)v) return v.ToString("F1");
        return v.ToString("G8");
    }
}
#endif
