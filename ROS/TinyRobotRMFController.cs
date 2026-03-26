using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.RmfFleetMsgs;
using RosMessageTypes.BuiltinInterfaces;
using System.Collections.Generic;

/// <summary>
/// Open-RMF 플릿 "tinyRobot"용 Unity 로봇 컨트롤러.
/// robot_state 발행, robot_path_requests 구독, 경로 이동 처리.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class TinyRobotRMFController : MonoBehaviour
{
    [Header("RMF Identity")]
    [SerializeField] private string robotName = "tinyRobot1";
    [SerializeField] private string levelName = "L1";

    [Header("RMF Topics (ROS-TCP-Endpoint와 동일 토픽)")]
    [SerializeField] private string robotStateTopic = "/robot_state";
    [SerializeField] private string pathRequestTopic = "/robot_path_requests";

    [Header("State Publish")]
    [SerializeField] private float robotStateUpdateFrequency = 10f;
    [Tooltip("Endpoint가 퍼블리셔 등록을 처리할 시간(초). 등록 전에 발행하면 'Not registered to publish' 에러가 날 수 있음.")]
    [SerializeField] private float publishStartDelay = 2f;

    [Header("Optional")]
    [Range(0f, 100f)]
    [SerializeField] private float batteryPercent = 100f;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 1.5f;
    [SerializeField] private float rotationSpeed = 90f;
    [SerializeField] private float waypointReachedThreshold = 0.25f;

    [Header("Coordinate Calibration")]
    [Tooltip("Unity 맵이 RMF 좌표계 원점과 어긋나 있을 때 보정용 X 오프셋 (Unity 좌표 기준)")]
    [SerializeField] private float mapOffsetX = 0f;
    [Tooltip("Unity 맵이 RMF 좌표계 원점과 어긋나 있을 때 보정용 Z 오프셋 (Unity 좌표 기준)")]
    [SerializeField] private float mapOffsetZ = 0f;
    [Tooltip("필요 시 회전 보정 (도 단위, 현재 미구현)")]
    [SerializeField] private float mapRotationOffset = 0f;
    [Tooltip("RMF와 Unity 간의 스케일 비율 (기본 1.0). Unity 1 unit이 RMF 1m가 아닐 경우 조정.")]
    [SerializeField] private float mapScale = 1.0f;

    [Tooltip("시작 후 일정 시간(초) 동안 들어오는 PathRequest를 무시 (오래된 메시지 처리 방지)")]
    [SerializeField] private float ignorePathRequestDuration = 5.0f;

    private Rigidbody rb;
    private ROSConnection ros;

    private string currentTaskId = "0";
    private readonly Queue<Vector3> waypointQueue = new Queue<Vector3>();
    private readonly Queue<float> waypointYaws = new Queue<float>();
    private float statePublishInterval;
    private float nextStatePublishTime;
    private ulong stateSeq;
    private bool isInitialized;
    private bool hasLoggedFirstPublish;
    private float startTime;
    private HashSet<string> ignoredTaskSet = new HashSet<string>();

    public string RobotName => robotName;
    public string LevelName => levelName;
    public float MoveSpeed => moveSpeed;
    public float RotationSpeed => rotationSpeed;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody>();
    }

    void Start()
    {
        SetupRigidbodyForRMF();
        ros = ROSConnection.GetOrCreateInstance();

        // RMF fleet_manager 호환: queue_size=10 (docs/From_ROS/Unity-robot_state-QoS-Fix.md)
        ros.RegisterPublisher<RobotStateMsg>(robotStateTopic, 10, false);
        ros.Subscribe<PathRequestMsg>(pathRequestTopic, OnPathRequest);

        statePublishInterval = 1f / robotStateUpdateFrequency;
        nextStatePublishTime = Time.time;
        startTime = Time.time;
        isInitialized = false;

        bool hasConnection = ros.HasConnectionThread;
        Debug.Log($"[RMF] {robotName}: publisher '{robotStateTopic}' 등록됨. 발행은 {publishStartDelay}초 후 시작. ROS 연결 스레드: {hasConnection}. (ROS 쪽에 토픽이 안 보이면 씬에 TinyRobot/TinyRobot2 오브젝트가 있는지, 연결이 된 뒤 발행이 시작되는지 확인)");
    }

    void SetupRigidbodyForRMF()
    {
        rb.useGravity = false;
        rb.isKinematic = false; 
        rb.linearDamping = 0f; // [수정] 저항 제거
        rb.angularDamping = 0f;
        rb.constraints = RigidbodyConstraints.FreezePositionY
                         | RigidbodyConstraints.FreezeRotationX
                         | RigidbodyConstraints.FreezeRotationZ;
        rb.interpolation = RigidbodyInterpolation.Interpolate; // 부드러운 이동
    }

    void OnPathRequest(PathRequestMsg msg)
    {
        if (msg.fleet_name != "tinyRobot" || msg.robot_name != robotName)
            return;

        string incomingTaskId = msg.task_id ?? "0";

        // 1. 블랙리스트(무시 목록)에 있는 태스크인지 확인
        if (ignoredTaskSet.Contains(incomingTaskId))
        {
            // 이미 무시하기로 한 태스크는 계속 무시 (로그 생략 가능)
            return;
        }

        // 2. 시작 후 일정 시간(초기화 기간) 동안 들어오는 태스크는 
        //    "이전에 남아있던 태스크"로 간주하여 블랙리스트에 등록하고 실행하지 않음 (죽임)
        if (Time.time - startTime < ignorePathRequestDuration)
        {
            if (!string.IsNullOrEmpty(incomingTaskId) && incomingTaskId != "0")
            {
                ignoredTaskSet.Add(incomingTaskId);
                Debug.LogWarning($"[RMF] {robotName}: 초기화 중 감지된 기존 태스크 '{incomingTaskId}'를 무시 목록에 등록하고 중단합니다.");
            }
            return;
        }

        currentTaskId = incomingTaskId;
        waypointQueue.Clear();
        waypointYaws.Clear();

        if (msg.path != null)
        {
            for (int i = 0; i < msg.path.Length; i++)
            {
                RMFToUnity(msg.path[i], out Vector3 pos, out float yawDeg);
                waypointQueue.Enqueue(pos);
                waypointYaws.Enqueue(yawDeg);
            }
        }

        if (waypointQueue.Count > 0)
            Debug.Log($"[RMF] {robotName} received path with {waypointQueue.Count} waypoints, task_id={currentTaskId}");
    }

    void Update()
    {
        if (!isInitialized)
        {
            if (Time.time - startTime >= publishStartDelay)
            {
                isInitialized = true;
                nextStatePublishTime = Time.time;
                Debug.Log($"[RMF] {robotName}: robot_state 발행 시작.");
            }
            return;
        }

        if (Time.time >= nextStatePublishTime)
        {
            PublishRobotState();
            nextStatePublishTime = Time.time + statePublishInterval;
        }
    }

    void FixedUpdate()
    {
        // 초기화 전이라도 물리적인 움직임을 막기 위해 정지 처리
        if (!isInitialized)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            return;
        }

        // 웨이포인트가 없으면 정지
        if (waypointQueue.Count == 0)
        {
            // 확실한 정지 보장
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            return;
        }

        Vector3 targetPos = waypointQueue.Peek();
        // float targetYaw = waypointYaws.Peek(); // 이동 중에는 직접 계산한 dir을 봄

        Vector3 flatPos = new Vector3(transform.position.x, 0f, transform.position.z);
        Vector3 flatTarget = new Vector3(targetPos.x, 0f, targetPos.z);
        float dist = Vector3.Distance(flatPos, flatTarget);

        Vector3 dir = (flatTarget - flatPos).normalized;
        
        // [수정] 목표 지점까지의 거리가 있으면 이동 로직 수행
        if (dist > waypointReachedThreshold)
        {
            // 1. 회전: 목표 방향 바라보기
            Quaternion targetRot = Quaternion.LookRotation(dir);
            float angleDiff = Vector3.Angle(transform.forward, dir);

            // 2. 제자리 회전 (Turn-in-place)
            // 각도 차이가 5도 이상이면 제자리에서 회전만 수행 (이동 X)
            if (angleDiff > 5.0f)
            {
                rb.MoveRotation(Quaternion.RotateTowards(transform.rotation, targetRot, rotationSpeed * Time.fixedDeltaTime));
                
                // 회전 중에는 물리적으로 밀리지 않도록 속도 0 고정
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            else
            {
                // 3. 각도가 맞으면 전진
                // 이동 중에도 미세한 회전 보정은 계속 수행
                rb.MoveRotation(Quaternion.RotateTowards(transform.rotation, targetRot, rotationSpeed * Time.fixedDeltaTime));

                float step = moveSpeed * Time.fixedDeltaTime;
                
                // 이번 프레임에 갈 거리가 남은 거리보다 크면 딱 거기까지만 이동 (Over-shooting 방지)
                if (step > dist) step = dist;

                Vector3 moveVec = dir * step;
                rb.MovePosition(rb.position + moveVec);
            }
        }
        else
        {
            // 웨이포인트 도달 처리
            waypointQueue.Dequeue();
            waypointYaws.Dequeue();
            
            // 도착했으면 확실하게 정지
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    void PublishRobotState()
    {
        uint mode = waypointQueue.Count > 0
            ? RobotModeMsg.MODE_MOVING
            : RobotModeMsg.MODE_IDLE;

        UnityToRMF(transform.position, transform.eulerAngles.y, out float x, out float y, out float yaw);

        // [수정] 타임스탬프를 0이 아닌 현재 시간으로 설정 (RMF 동기화 개선)
        int sec = (int)Time.time;
        uint nanosec = (uint)((Time.time % 1) * 1e9);
        TimeMsg t = new TimeMsg(sec, nanosec); 
        
        var location = new LocationMsg(t, x, y, yaw, false, 0f, levelName, 0);

        LocationMsg[] pathArray = new LocationMsg[0];
        if (waypointQueue.Count > 0)
        {
            var list = new List<LocationMsg>();
            foreach (Vector3 wp in waypointQueue)
            {
                UnityToRMF(wp, 0f, out float wx, out float wy, out float wyaw);
                list.Add(new LocationMsg(t, wx, wy, wyaw, false, 0f, levelName, 0));
            }
            pathArray = list.ToArray();
        }

        var state = new RobotStateMsg
        {
            name = robotName,
            model = "",
            task_id = currentTaskId,
            seq = stateSeq++,
            mode = new RobotModeMsg(mode, 0),
            battery_percent = batteryPercent,
            location = location,
            path = pathArray
        };

        try
        {
            ros.Publish(robotStateTopic, state);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[RMF] {robotName}: Publish 실패 - {e.Message}\n{e.StackTrace}");
            return;
        }

        if (!hasLoggedFirstPublish)
        {
            hasLoggedFirstPublish = true;
            Debug.Log($"[RMF] {robotName}: 첫 robot_state 발행 완료.");
        }
    }

    /// <summary>
    /// Unity 월드 좌표 → RMF L1 좌표 (미터, 라디안).
    /// Legacy Mapping: Unity Z -> RMF x, Unity X -> RMF y.
    /// (이전 AMRController와 동일한 매핑으로 복구)
    /// </summary>
    void UnityToRMF(Vector3 unityPos, float unityYawDeg, out float x, out float y, out float yaw)
    {
        // 1. 보정값 적용 (Offset)
        float correctedX = unityPos.x - mapOffsetX;
        float correctedZ = unityPos.z - mapOffsetZ;

        // 2. 스케일 적용 (Unity -> RMF: 나누기? 곱하기? 보통 Unity 1unit = 1m이므로 1.0)
        // 만약 mapScale이 1보다 크면(Unity가 더 크면) RMF로 갈 땐 나눠줘야 함.
        // 여기선 mapScale을 "Unity 1 unit이 RMF 몇 m인가"로 정의하지 않고, 단순히 비례 상수로 둠.
        // RMF = Unity * scale
        correctedX *= mapScale;
        correctedZ *= mapScale;

        // 3. 축 매핑 (Unity X -> RMF x, Unity Z -> RMF y)
        x = correctedX;
        y = correctedZ;
        
        // 4. 회전 매핑
        // RMF yaw = 90 - Unity yaw (in degrees) - rotationOffset
        float correctedYawDeg = unityYawDeg - mapRotationOffset;
        yaw = (90f - correctedYawDeg) * Mathf.Deg2Rad;
        
        // Normalize to -PI ~ PI
        if (yaw > Mathf.PI) yaw -= 2 * Mathf.PI;
        else if (yaw <= -Mathf.PI) yaw += 2 * Mathf.PI;
    }

    /// <summary>
    /// RMF L1 좌표 → Unity 월드 좌표.
    /// </summary>
    void RMFToUnity(LocationMsg loc, out Vector3 position, out float yawDeg)
    {
        // 1. 축 매핑 역변환 (RMF x -> Unity X, RMF y -> Unity Z)
        float unityX = loc.x;
        float unityZ = loc.y;

        // 2. 스케일 역변환 (Unity = RMF / scale)
        if (mapScale != 0)
        {
            unityX /= mapScale;
            unityZ /= mapScale;
        }

        // 3. 보정값 적용 (Offset 더하기)
        unityX += mapOffsetX;
        unityZ += mapOffsetZ;

        position = new Vector3(unityX, transform.position.y, unityZ);
        
        // 4. 회전 매핑 역변환
        // Unity yaw = 90 - RMF yaw + rotationOffset
        yawDeg = 90f - (loc.yaw * Mathf.Rad2Deg) + mapRotationOffset;
    }
}
