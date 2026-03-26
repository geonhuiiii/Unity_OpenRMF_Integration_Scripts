#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Linq;

/// <summary>
/// Unity 씬의 TinyRobotRMFController 및 태그된 웨이포인트 정보를 기반으로
/// RMF fleet adapter config.yaml을 자동 생성하는 에디터 도구.
///
/// BuildingYamlGenerator와 동일한 웨이포인트 명명 규칙(charger_N, pick_N, drop_N,
/// {robotName}_parking)을 사용하여 이름 불일치 문제를 방지합니다.
///
/// 메뉴: OpenRMF → Generate Fleet Adapter Config
/// </summary>
public class FleetAdapterConfigGenerator : EditorWindow
{
    [Header("Fleet")]
    string fleetName = "tinyRobot";
    string fleetManagerIp = "127.0.0.1";
    int fleetManagerPort = 7001;
    string fleetManagerUser = "some_user";
    string fleetManagerPassword = "some_password";

    [Header("Profile")]
    float footprintRadius = 0.3f;
    float vicinityRadius = 0.5f;
    bool reversible = true;

    [Header("Limits")]
    float linearVelocity = 0.5f;
    float linearAcceleration = 0.75f;
    float angularVelocity = 0.6f;
    float angularAcceleration = 2.0f;

    [Header("Battery")]
    float batteryVoltage = 12.0f;
    float batteryCapacity = 24.0f;
    float chargingCurrent = 5.0f;

    [Header("Mechanical")]
    float robotMass = 20.0f;
    float momentOfInertia = 10.0f;
    float frictionCoefficient = 0.22f;

    [Header("Power")]
    float ambientPower = 20.0f;
    float toolPower = 0.0f;

    [Header("Recharge")]
    float rechargeThreshold = 0.10f;
    float rechargeSoc = 1.0f;
    bool accountForBatteryDrain = true;

    [Header("Tasks")]
    bool taskLoop = true;
    bool taskDelivery = true;
    bool taskClean = false;
    int finishingRequestIdx = 0;
    static readonly string[] finishingRequestOptions = { "park", "charge", "nothing" };

    [Header("Robot Config")]
    float maxDelay = 15.0f;
    bool filterWaypoints = false;

    string outputPath = "";
    Vector2 scrollPos;
    bool limitsAutoDetected;

    [MenuItem("OpenRMF/Generate Fleet Adapter Config")]
    static void ShowWindow()
    {
        var w = GetWindow<FleetAdapterConfigGenerator>("Fleet Adapter Config");
        w.minSize = new Vector2(450, 600);
    }

    void OnEnable()
    {
        if (string.IsNullOrEmpty(outputPath))
            outputPath = Path.Combine(Application.dataPath, "..", "fleet_adapter_config.yaml");

        AutoDetectLimitsFromScene();
    }

    void AutoDetectLimitsFromScene()
    {
        var robots = Object.FindObjectsByType<TinyRobotRMFController>(FindObjectsSortMode.None);
        if (robots.Length > 0)
        {
            linearVelocity = robots[0].MoveSpeed;
            angularVelocity = robots[0].RotationSpeed * Mathf.Deg2Rad;
            limitsAutoDetected = true;
        }
    }

    // ─── GUI ───

    void OnGUI()
    {
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        EditorGUILayout.LabelField("Fleet Adapter Config Generator", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "유니티 씬의 TinyRobotRMFController와 태그된 웨이포인트를 기반으로\n" +
            "fleet adapter config.yaml을 자동 생성합니다.\n" +
            "웨이포인트 이름은 BuildingYamlGenerator와 동일한 명명 규칙을 사용합니다.",
            MessageType.Info);

        EditorGUILayout.Space();
        DrawFleetSettings();
        EditorGUILayout.Space();
        DrawProfileSettings();
        EditorGUILayout.Space();
        DrawLimitsSettings();
        EditorGUILayout.Space();
        DrawBatterySettings();
        EditorGUILayout.Space();
        DrawMechanicalSettings();
        EditorGUILayout.Space();
        DrawTaskSettings();
        EditorGUILayout.Space();
        DrawRobotConfigSettings();
        EditorGUILayout.Space();
        DrawSceneStatus();
        EditorGUILayout.Space();
        DrawOutputSettings();

        EditorGUILayout.Space();
        GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
        if (GUILayout.Button("Fleet Adapter Config 생성", GUILayout.Height(40)))
            Generate();
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndScrollView();
    }

    void DrawFleetSettings()
    {
        EditorGUILayout.LabelField("Fleet 설정", EditorStyles.boldLabel);
        fleetName = EditorGUILayout.TextField("Fleet 이름", fleetName);
        fleetManagerIp = EditorGUILayout.TextField("Fleet Manager IP", fleetManagerIp);
        fleetManagerPort = EditorGUILayout.IntField("Fleet Manager Port", fleetManagerPort);
        fleetManagerUser = EditorGUILayout.TextField("User", fleetManagerUser);
        fleetManagerPassword = EditorGUILayout.TextField("Password", fleetManagerPassword);
    }

    void DrawProfileSettings()
    {
        EditorGUILayout.LabelField("로봇 프로필", EditorStyles.boldLabel);
        footprintRadius = EditorGUILayout.FloatField("Footprint 반경 (m)", footprintRadius);
        vicinityRadius = EditorGUILayout.FloatField("Vicinity 반경 (m)", vicinityRadius);
        reversible = EditorGUILayout.Toggle("후진 가능", reversible);
    }

    void DrawLimitsSettings()
    {
        EditorGUILayout.LabelField("이동 제한", EditorStyles.boldLabel);
        if (limitsAutoDetected)
        {
            EditorGUILayout.HelpBox("씬의 TinyRobotRMFController에서 속도를 자동 감지했습니다.", MessageType.None);
        }
        linearVelocity = EditorGUILayout.FloatField("Linear Velocity (m/s)", linearVelocity);
        linearAcceleration = EditorGUILayout.FloatField("Linear Acceleration (m/s²)", linearAcceleration);
        angularVelocity = EditorGUILayout.FloatField("Angular Velocity (rad/s)", angularVelocity);
        angularAcceleration = EditorGUILayout.FloatField("Angular Acceleration (rad/s²)", angularAcceleration);
    }

    void DrawBatterySettings()
    {
        EditorGUILayout.LabelField("배터리 시스템", EditorStyles.boldLabel);
        batteryVoltage = EditorGUILayout.FloatField("Voltage (V)", batteryVoltage);
        batteryCapacity = EditorGUILayout.FloatField("Capacity (Ah)", batteryCapacity);
        chargingCurrent = EditorGUILayout.FloatField("Charging Current (A)", chargingCurrent);
        EditorGUILayout.Space(2);
        rechargeThreshold = EditorGUILayout.Slider("Recharge Threshold", rechargeThreshold, 0f, 1f);
        rechargeSoc = EditorGUILayout.Slider("Recharge SoC", rechargeSoc, 0f, 1f);
        accountForBatteryDrain = EditorGUILayout.Toggle("Account for Battery Drain", accountForBatteryDrain);
    }

    void DrawMechanicalSettings()
    {
        EditorGUILayout.LabelField("기계·전력 시스템", EditorStyles.boldLabel);
        robotMass = EditorGUILayout.FloatField("Mass (kg)", robotMass);
        momentOfInertia = EditorGUILayout.FloatField("Moment of Inertia (kg·m²)", momentOfInertia);
        frictionCoefficient = EditorGUILayout.FloatField("Friction Coefficient", frictionCoefficient);
        ambientPower = EditorGUILayout.FloatField("Ambient Power (W)", ambientPower);
        toolPower = EditorGUILayout.FloatField("Tool Power (W)", toolPower);
    }

    void DrawTaskSettings()
    {
        EditorGUILayout.LabelField("태스크 설정", EditorStyles.boldLabel);
        taskLoop = EditorGUILayout.Toggle("Loop", taskLoop);
        taskDelivery = EditorGUILayout.Toggle("Delivery", taskDelivery);
        taskClean = EditorGUILayout.Toggle("Clean", taskClean);
        finishingRequestIdx = EditorGUILayout.Popup("Finishing Request", finishingRequestIdx, finishingRequestOptions);
    }

    void DrawRobotConfigSettings()
    {
        EditorGUILayout.LabelField("로봇 개별 설정", EditorStyles.boldLabel);
        maxDelay = EditorGUILayout.FloatField("Max Delay (s)", maxDelay);
        filterWaypoints = EditorGUILayout.Toggle("Filter Waypoints", filterWaypoints);
    }

    void DrawSceneStatus()
    {
        EditorGUILayout.LabelField("씬 오브젝트 현황", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;

        var robots = Object.FindObjectsByType<TinyRobotRMFController>(FindObjectsSortMode.None);
        var style = new GUIStyle(EditorStyles.label) { richText = true };

        string robotColor = robots.Length > 0 ? "green" : "red";
        EditorGUILayout.LabelField("TinyRobotRMFController:",
            $"<color={robotColor}>{robots.Length}개</color>", style);

        foreach (var r in robots.OrderBy(r => r.RobotName))
            EditorGUILayout.LabelField($"  └ {r.RobotName} (Level: {r.LevelName}, Speed: {r.MoveSpeed} m/s)");

        DrawTagCount("Charger", style);
        DrawTagCount("Pickup", style);
        DrawTagCount("Dropoff", style);

        var assignments = ComputeRobotAssignments();
        if (assignments.Count > 0)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("자동 웨이포인트 할당 미리보기", EditorStyles.miniLabel);
            foreach (var a in assignments.OrderBy(kv => kv.Key))
            {
                EditorGUILayout.LabelField(
                    $"  {a.Key}  →  start: \"{a.Value.parkingName}\",  charger: \"{a.Value.chargerName}\"");
            }
        }

        EditorGUI.indentLevel--;
    }

    void DrawTagCount(string tag, GUIStyle style)
    {
        int count = 0;
        try { count = GameObject.FindGameObjectsWithTag(tag).Length; }
        catch { }
        string color = count > 0 ? "green" : "red";
        EditorGUILayout.LabelField($"{tag}:", $"<color={color}>{count}개</color>", style);
    }

    void DrawOutputSettings()
    {
        EditorGUILayout.LabelField("출력", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        outputPath = EditorGUILayout.TextField("저장 경로", outputPath);
        if (GUILayout.Button("...", GUILayout.Width(30)))
        {
            string dir = Path.GetDirectoryName(outputPath);
            string file = Path.GetFileName(outputPath);
            string path = EditorUtility.SaveFilePanel("Fleet Config YAML 저장", dir, file, "yaml");
            if (!string.IsNullOrEmpty(path)) outputPath = path;
        }
        EditorGUILayout.EndHorizontal();
    }

    // ─── Data structures ───

    struct RobotAssignment
    {
        public string robotName;
        public string levelName;
        public string parkingName;
        public string chargerName;
        public float orientation;
        public float stateUpdateFrequency;
    }

    // ─── Scene analysis ───

    /// <summary>
    /// BuildingYamlGenerator.CollectByTag("Charger", ...) 와 동일한 명명 규칙으로 충전기를 수집합니다.
    /// 정렬 기준: GameObject.name 오름차순, 인덱스 1부터 시작 → "charger_1", "charger_2", ...
    /// </summary>
    Dictionary<string, Vector2> CollectChargers()
    {
        var result = new Dictionary<string, Vector2>();
        GameObject[] objs;
        try { objs = GameObject.FindGameObjectsWithTag("Charger"); }
        catch { return result; }

        var sorted = objs.OrderBy(o => o.name).ToArray();
        for (int i = 0; i < sorted.Length; i++)
        {
            string waypointName = $"charger_{i + 1}";
            Vector3 wpos = sorted[i].transform.position;
            result[waypointName] = new Vector2(wpos.x, wpos.z);
        }
        return result;
    }

    /// <summary>
    /// 각 로봇에 대해 시작 웨이포인트(parking)와 가장 가까운 충전기를 계산합니다.
    /// </summary>
    Dictionary<string, RobotAssignment> ComputeRobotAssignments()
    {
        var result = new Dictionary<string, RobotAssignment>();
        var robots = Object.FindObjectsByType<TinyRobotRMFController>(FindObjectsSortMode.None);
        if (robots.Length == 0) return result;

        var chargers = CollectChargers();
        var usedChargers = new HashSet<string>();

        // 각 로봇을 가장 가까운 충전기에 1:1 매칭 (거리 순으로 greedy 할당)
        var robotChargerPairs = new List<(TinyRobotRMFController robot, string chargerName, float dist)>();
        foreach (var robot in robots)
        {
            Vector2 robotPos = new Vector2(robot.transform.position.x, robot.transform.position.z);
            foreach (var charger in chargers)
            {
                float dist = Vector2.Distance(robotPos, charger.Value);
                robotChargerPairs.Add((robot, charger.Key, dist));
            }
        }

        robotChargerPairs.Sort((a, b) => a.dist.CompareTo(b.dist));
        var assignedRobots = new HashSet<string>();

        foreach (var pair in robotChargerPairs)
        {
            if (assignedRobots.Contains(pair.robot.RobotName)) continue;
            if (usedChargers.Contains(pair.chargerName)) continue;

            assignedRobots.Add(pair.robot.RobotName);
            usedChargers.Add(pair.chargerName);

            float yawRad = ComputeRMFYaw(pair.robot.transform.eulerAngles.y);

            float stateFreq = ReadSerializedFloat(pair.robot, "robotStateUpdateFrequency", 10f);

            result[pair.robot.RobotName] = new RobotAssignment
            {
                robotName = pair.robot.RobotName,
                levelName = pair.robot.LevelName,
                parkingName = $"{pair.robot.RobotName}_parking",
                chargerName = pair.chargerName,
                orientation = yawRad,
                stateUpdateFrequency = stateFreq
            };
        }

        // 충전기보다 로봇이 많은 경우: 할당 안 된 로봇은 parking spot을 charger로도 사용
        foreach (var robot in robots)
        {
            if (assignedRobots.Contains(robot.RobotName)) continue;

            float yawRad = ComputeRMFYaw(robot.transform.eulerAngles.y);
            float stateFreq = ReadSerializedFloat(robot, "robotStateUpdateFrequency", 10f);
            string parkingName = $"{robot.RobotName}_parking";

            result[robot.RobotName] = new RobotAssignment
            {
                robotName = robot.RobotName,
                levelName = robot.LevelName,
                parkingName = parkingName,
                chargerName = parkingName,
                orientation = yawRad,
                stateUpdateFrequency = stateFreq
            };

            Debug.LogWarning($"[FleetAdapterConfig] '{robot.RobotName}'에 할당할 충전기가 부족합니다. " +
                             $"parking spot '{parkingName}'을 charger로 사용합니다.");
        }

        return result;
    }

    static float ComputeRMFYaw(float unityYawDeg)
    {
        float yaw = (90f - unityYawDeg) * Mathf.Deg2Rad;
        if (yaw > Mathf.PI) yaw -= 2f * Mathf.PI;
        else if (yaw <= -Mathf.PI) yaw += 2f * Mathf.PI;
        return yaw;
    }

    static float ReadSerializedFloat(Object target, string fieldName, float fallback)
    {
        var so = new SerializedObject(target);
        var prop = so.FindProperty(fieldName);
        return prop != null ? prop.floatValue : fallback;
    }

    // ─── Generation ───

    void Generate()
    {
        var robots = Object.FindObjectsByType<TinyRobotRMFController>(FindObjectsSortMode.None);
        if (robots.Length == 0)
        {
            EditorUtility.DisplayDialog("오류", "씬에 TinyRobotRMFController가 없습니다.", "확인");
            return;
        }

        var chargers = CollectChargers();
        if (chargers.Count == 0)
        {
            bool proceed = EditorUtility.DisplayDialog("경고",
                "씬에 'Charger' 태그된 오브젝트가 없습니다.\n" +
                "로봇의 parking spot을 charger 웨이포인트로 사용합니다.\n\n계속하시겠습니까?",
                "계속", "취소");
            if (!proceed) return;
        }

        var assignments = ComputeRobotAssignments();

        float publishFleetState = ReadSerializedFloat(robots[0], "robotStateUpdateFrequency", 10f);

        string yaml = BuildConfigYaml(assignments, publishFleetState);
        File.WriteAllText(outputPath, yaml, new UTF8Encoding(false));

        var assignmentSummary = string.Join(", ",
            assignments.OrderBy(kv => kv.Key)
                       .Select(kv => $"{kv.Key}→{kv.Value.chargerName}"));

        Debug.Log($"[FleetAdapterConfig] 생성 완료: {outputPath}\n" +
                  $"  로봇: {assignments.Count}개, 충전기: {chargers.Count}개\n" +
                  $"  할당: {assignmentSummary}");

        EditorUtility.RevealInFinder(outputPath);
    }

    // ─── YAML output ───

    string BuildConfigYaml(Dictionary<string, RobotAssignment> assignments, float publishFleetState)
    {
        var sb = new StringBuilder();
        string finishing = finishingRequestOptions[finishingRequestIdx];

        sb.AppendLine("# FLEET CONFIG =================================================================");
        sb.AppendLine("# RMF Fleet parameters (auto-generated from Unity scene)");
        sb.AppendLine();
        sb.AppendLine("rmf_fleet:");
        sb.AppendLine($"  name: \"{fleetName}\"");
        sb.AppendLine("  fleet_manager:");
        sb.AppendLine($"    ip: \"{fleetManagerIp}\"");
        sb.AppendLine($"    port: {fleetManagerPort}");
        sb.AppendLine($"    user: \"{fleetManagerUser}\"");
        sb.AppendLine($"    password: \"{fleetManagerPassword}\"");
        sb.AppendLine("  limits:");
        sb.AppendLine($"    linear: [{F(linearVelocity)}, {F(linearAcceleration)}] # velocity, acceleration");
        sb.AppendLine($"    angular: [{F(angularVelocity)}, {F(angularAcceleration)}] # velocity, acceleration");
        sb.AppendLine("  profile: # Robot profile is modelled as a circle");
        sb.AppendLine($"    footprint: {F(footprintRadius)} # radius in m");
        sb.AppendLine($"    vicinity: {F(vicinityRadius)} # radius in m");
        sb.AppendLine($"  reversible: {Bool(reversible)} # whether robots in this fleet can reverse");
        sb.AppendLine("  battery_system:");
        sb.AppendLine($"    voltage: {F(batteryVoltage)} # V");
        sb.AppendLine($"    capacity: {F(batteryCapacity)} # Ah");
        sb.AppendLine($"    charging_current: {F(chargingCurrent)} # A");
        sb.AppendLine("  mechanical_system:");
        sb.AppendLine($"    mass: {F(robotMass)} # kg");
        sb.AppendLine($"    moment_of_inertia: {F(momentOfInertia)} # kgm^2");
        sb.AppendLine($"    friction_coefficient: {F(frictionCoefficient)}");
        sb.AppendLine("  ambient_system:");
        sb.AppendLine($"    power: {F(ambientPower)} # W");
        sb.AppendLine("  tool_system:");
        sb.AppendLine($"    power: {F(toolPower)} # W");
        sb.AppendLine($"  recharge_threshold: {F(rechargeThreshold)} # Battery level below which robots in this fleet will not operate");
        sb.AppendLine($"  recharge_soc: {F(rechargeSoc)} # Battery level to which robots should be charged up to during recharging");
        sb.AppendLine($"  publish_fleet_state: {F(publishFleetState)} # Publish frequency for fleet state, ensure same as robot_state_update_frequency");
        sb.AppendLine($"  account_for_battery_drain: {Bool(accountForBatteryDrain)}");
        sb.AppendLine("  task_capabilities: # Specify the types of RMF Tasks that robots in this fleet are capable of performing");
        sb.AppendLine($"    loop: {Bool(taskLoop)}");
        sb.AppendLine($"    delivery: {Bool(taskDelivery)}");
        sb.AppendLine($"    clean: {Bool(taskClean)}");
        sb.AppendLine($"    finishing_request: \"{finishing}\" # [park, charge, nothing]");

        // ─── Robots ───
        sb.AppendLine();
        sb.AppendLine("# ROBOT CONFIG =================================================================");
        sb.AppendLine();
        sb.AppendLine("robots:");

        foreach (var kv in assignments.OrderBy(kv => kv.Key))
        {
            var a = kv.Value;
            sb.AppendLine($"  {a.robotName}:");
            sb.AppendLine("    robot_config:");
            sb.AppendLine($"      max_delay: {F(maxDelay)} # allowed seconds of delay before itinerary gets interrupted and replanned");
            sb.AppendLine($"      filter_waypoints: {Bool(filterWaypoints)}");
            sb.AppendLine("    rmf_config:");
            sb.AppendLine($"      robot_state_update_frequency: {F(a.stateUpdateFrequency)}");
            sb.AppendLine("      start:");
            sb.AppendLine($"        map_name: \"{a.levelName}\"");
            sb.AppendLine($"        waypoint: \"{a.parkingName}\"");
            sb.AppendLine($"        orientation: {F(a.orientation)} # radians");
            sb.AppendLine("      charger:");
            sb.AppendLine($"        waypoint: \"{a.chargerName}\"");
        }

        // ─── Reference Coordinates ───
        sb.AppendLine();
        sb.AppendLine("# TRANSFORM CONFIG =============================================================");
        sb.AppendLine("# For computing transforms between Robot and RMF coordinate systems");
        sb.AppendLine("# Unity coordinates are used directly (1 unit = 1 meter)");
        sb.AppendLine();
        sb.AppendLine("reference_coordinates:");
        sb.AppendLine("  rmf: [[0.0, 0.0],");
        sb.AppendLine("        [1.0, 1.0],");
        sb.AppendLine("        [2.0, 2.0],");
        sb.AppendLine("        [3.0, 3.0]]");
        sb.AppendLine("  robot: [[0.0, 0.0],");
        sb.AppendLine("        [1.0, 1.0],");
        sb.AppendLine("        [2.0, 2.0],");
        sb.AppendLine("        [3.0, 3.0]]");

        return sb.ToString();
    }

    static string F(float v)
    {
        if (v == (int)v) return v.ToString("F1");
        return v.ToString("G6");
    }

    static string Bool(bool v) => v ? "True" : "False";
}
#endif
