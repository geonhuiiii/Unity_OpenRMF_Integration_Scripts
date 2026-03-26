using UnityEngine;
using RosMessageTypes.Tf2;
using Unity.Robotics.ROSTCPConnector;
using Unity.Robotics.ROSTCPConnector.ROSGeometry; // ⭐ 좌표계 변환용
using RosMessageTypes.Geometry;
using RosMessageTypes.Std;
using RosMessageTypes.Nav;
using RosMessageTypes.Rosgraph;

public class AMRController_ : MonoBehaviour
{
    private ROSConnection ros;
    private Rigidbody rb;
    
    [Header("ROS Settings")]
    [SerializeField] private string baseFrameId = "base_link";
    [SerializeField] private string odomFrameId = "odom";
    [SerializeField] private string mapFrameId = "map";
    [SerializeField] private float publishRate = 10f; // Hz
    
    [Header("Movement Settings")]
    public float moveSpeed = 2.0f;
    public float rotationSpeed = 60.0f;
    public float acceleration = 5.0f;
    public float deceleration = 5.0f;
    
    [Header("Rotation Settings")]
    public float rotationThreshold = 5.0f;
    public float minTurnRadius = 0.5f;
    public bool allowPivotTurn = false;
    
    [Header("Self-Collision Prevention")]
    [SerializeField] private LayerMask robotLayerMask;
    [Tooltip("로봇 자신의 레이어를 여기서 제외하세요")]

    private Vector3 targetPosition;
    private bool hasTarget = false;
    private float currentSpeed = 0f;
    private float publishInterval;
    private float nextPublishTime;
    
    // Odometry 계산용
    private Vector3 lastPosition;
    private Quaternion lastRotation;
    private float lastUpdateTime;
    
    // ⭐ 타임스탬프를 static property로 제공
    public static RosMessageTypes.BuiltinInterfaces.TimeMsg CurrentTimestamp { get; private set; }
    
    private bool isInitialized = false;

    void Awake()
    {
        // 최초 타임스탬프 초기화 (null 방지)
        CurrentTimestamp = new RosMessageTypes.BuiltinInterfaces.TimeMsg 
        { 
            sec = 0, 
            nanosec = 0 
        };
        
        lastPosition = transform.position;
        lastRotation = transform.rotation;
        lastUpdateTime = Time.time;
    }

    void Start()
    {
        // ROS 연결 초기화
        ros = ROSConnection.GetOrCreateInstance();
        
        // Rigidbody 설정
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezePositionY | 
                        RigidbodyConstraints.FreezeRotationX | 
                        RigidbodyConstraints.FreezeRotationZ;

        // ROS Publisher 등록
        ros.RegisterPublisher<TFMessageMsg>("/tf");
        ros.RegisterPublisher<ClockMsg>("/clock");
        ros.RegisterPublisher<OdometryMsg>("/odom"); // ⭐ Odometry 추가
        ros.RegisterPublisher<PoseStampedMsg>("unity_amr/current_pose");
        
        // ROS Subscriber 등록
        ros.Subscribe<PoseStampedMsg>("unity_amr/goal", ReceiveGoal);
        
        // 발행 주기 계산
        publishInterval = 1f / publishRate;
        nextPublishTime = Time.time + 0.1f; // 초기 딜레이
        
        // 초기화 완료
        isInitialized = true;
        
        // Clock은 더 빠른 주기로 발행 (200Hz)
        InvokeRepeating("PublishClock", 0.05f, 0.005f);
        
        Debug.Log($"AMR Controller initialized - Frame IDs: {odomFrameId} -> {baseFrameId}");
    }

    void Update()
    {
        // 주기적으로 메시지 발행
        if (isInitialized && Time.time >= nextPublishTime)
        {
            UpdateTimestamp();
            PublishAll();
            nextPublishTime += publishInterval;
        }
    }

    void UpdateTimestamp()
    {
        double futureTime = Time.time + 0.1f; 
        CurrentTimestamp = new RosMessageTypes.BuiltinInterfaces.TimeMsg
        {
            sec = (int)futureTime, 
            nanosec = (uint)((futureTime % 1) * 1e9)
        };
    }
    
    void PublishClock()
    {
        if (!isInitialized) return;
        
        UpdateTimestamp();
        ClockMsg clockMsg = new ClockMsg { clock = CurrentTimestamp };
        ros.Publish("/clock", clockMsg);
    }

    void PublishAll()
    {
        if (!isInitialized || CurrentTimestamp == null) return;
        
        PublishOdomTF();
        PublishOdometry();
        PublishCurrentPose();
    }

    void ReceiveGoal(PoseStampedMsg goalMsg)
    {
        // ⭐ ROS 좌표계 -> Unity 좌표계 변환
        // ROS의 (x, y) -> Unity의 (y, z) 매핑 (FLU 기준)
        targetPosition = new Vector3(
            (float)goalMsg.pose.position.y,  // ROS Y -> Unity X
            transform.position.y,             // Y 고정 (2D 평면)
            (float)goalMsg.pose.position.x   // ROS X -> Unity Z
        );

        hasTarget = true;
        currentSpeed = 0f;
        Debug.Log($"[Goal Received] ROS: ({goalMsg.pose.position.x:F2}, {goalMsg.pose.position.y:F2}) -> Unity: {targetPosition}");
    }

    void FixedUpdate()
    {
        if (hasTarget)
        {
            MoveToTargetRealistic();
        }
    }

    void MoveToTargetRealistic()
    {
        Vector3 direction = (targetPosition - transform.position).normalized;
        direction.y = 0; // 2D 평면 이동
        
        float distance = Vector3.Distance(
            new Vector3(transform.position.x, 0, transform.position.z),
            new Vector3(targetPosition.x, 0, targetPosition.z)
        );
        
        if (distance > 0.1f)
        {
            if (direction != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                float angleDiff = Quaternion.Angle(rb.rotation, targetRotation);
                
                if (angleDiff > rotationThreshold)
                {
                    if (allowPivotTurn)
                    {
                        // 제자리 회전 모드
                        currentSpeed = 0f;
                        Quaternion newRotation = Quaternion.RotateTowards(
                            rb.rotation,
                            targetRotation,
                            rotationSpeed * Time.fixedDeltaTime
                        );
                        rb.MoveRotation(newRotation);
                    }
                    else
                    {
                        // 부드러운 회전 + 감속
                        float turnSpeedMultiplier = 1f - (angleDiff / 180f);
                        turnSpeedMultiplier = Mathf.Clamp(turnSpeedMultiplier, 0.2f, 1f);
                        
                        Quaternion newRotation = Quaternion.RotateTowards(
                            rb.rotation,
                            targetRotation,
                            rotationSpeed * 0.5f * Time.fixedDeltaTime
                        );
                        rb.MoveRotation(newRotation);
                        
                        float targetSpeed = moveSpeed * turnSpeedMultiplier * 0.5f;
                        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, 
                            (currentSpeed < targetSpeed ? acceleration : deceleration) * Time.fixedDeltaTime);
                        
                        Vector3 newPosition = rb.position + transform.forward * currentSpeed * Time.fixedDeltaTime;
                        rb.MovePosition(newPosition);
                    }
                }
                else
                {
                    // 정상 주행
                    Quaternion newRotation = Quaternion.RotateTowards(
                        rb.rotation,
                        targetRotation,
                        rotationSpeed * Time.fixedDeltaTime
                    );
                    rb.MoveRotation(newRotation);
                    
                    float targetSpeed = moveSpeed;
                    if (distance < 2f)
                    {
                        targetSpeed *= Mathf.Clamp01(distance / 2f);
                    }
                    
                    currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, 
                        (currentSpeed < targetSpeed ? acceleration : deceleration) * Time.fixedDeltaTime);
                    
                    Vector3 newPosition = rb.position + direction * currentSpeed * Time.fixedDeltaTime;
                    rb.MovePosition(newPosition);
                }
            }
        }
        else
        {
            hasTarget = false;
            currentSpeed = 0f;
            rb.linearVelocity = Vector3.zero;
            Debug.Log("✓ Goal reached!");
        }
    }

    /// <summary>
    /// odom -> base_link TF 발행
    /// ⭐ map -> base_link을 직접 발행하지 않음 (SLAM 노드가 map -> odom 발행)
    /// </summary>
    void PublishOdomTF()
    {
        if (!isInitialized || CurrentTimestamp == null) return;
        
        try
        {
            TFMessageMsg tfMessage = new TFMessageMsg
            {
                transforms = new TransformStampedMsg[]
                {
                    new TransformStampedMsg
                    {
                        header = new HeaderMsg
                        {
                            stamp = CurrentTimestamp,
                            frame_id = odomFrameId // ⭐ "odom"으로 변경
                        },
                        child_frame_id = baseFrameId,
                        transform = new TransformMsg
                        {
                            // ⭐ ROSGeometry 라이브러리 사용
                            translation = transform.position.To<FLU>(),
                            rotation = transform.rotation.To<FLU>()
                        }
                    }
                }
            };
            ros.Publish("/tf", tfMessage);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[TF Publish Failed] {e.Message}");
        }
    }

    /// <summary>
    /// Odometry 메시지 발행 (ROS Navigation Stack 필수)
    /// </summary>
    void PublishOdometry()
    {
        if (!isInitialized || CurrentTimestamp == null) return;
        
        try
        {
            // 속도 계산 (delta position / delta time)
            float dt = Time.time - lastUpdateTime;
            if (dt < 0.001f) dt = 0.001f; // 0으로 나누기 방지
            
            Vector3 deltaPos = transform.position - lastPosition;
            Quaternion deltaRot = transform.rotation * Quaternion.Inverse(lastRotation);
            
            // Unity 좌표계에서 속도 계산
            Vector3 linearVel = deltaPos / dt;
            Vector3 angularVel = (deltaRot.eulerAngles / dt) * Mathf.Deg2Rad;
            
            // ROS 좌표계로 변환
            var rosLinearVel = linearVel.To<FLU>();
            var rosAngularVel = angularVel.To<FLU>();
            
            OdometryMsg odomMsg = new OdometryMsg
            {
                header = new HeaderMsg
                {
                    stamp = CurrentTimestamp,
                    frame_id = odomFrameId
                },
                child_frame_id = baseFrameId,
                
                // 위치 (pose)
                pose = new PoseWithCovarianceMsg
                {
                    pose = new PoseMsg
                    {
                        position = transform.position.To<FLU>(),
                        orientation = transform.rotation.To<FLU>()
                    },
                    // 공분산 행렬 (36개 요소, 6x6)
                    covariance = new double[36]
                },
                
                // 속도 (twist)
                twist = new TwistWithCovarianceMsg
                {
                    twist = new TwistMsg
                    {
                        linear = rosLinearVel,
                        angular = rosAngularVel
                    },
                    covariance = new double[36]
                }
            };
            
            ros.Publish("/odom", odomMsg);
            
            // 상태 업데이트
            lastPosition = transform.position;
            lastRotation = transform.rotation;
            lastUpdateTime = Time.time;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Odometry Publish Failed] {e.Message}");
        }
    }

    /// <summary>
    /// 현재 위치 발행 (모니터링용)
    /// </summary>
    void PublishCurrentPose()
    {
        if (!isInitialized || CurrentTimestamp == null) return;
        
        try
        {
            PoseStampedMsg poseMsg = new PoseStampedMsg
            {
                header = new HeaderMsg
                {
                    stamp = CurrentTimestamp,
                    frame_id = mapFrameId // 또는 odomFrameId
                },
                pose = new PoseMsg
                {
                    position = transform.position.To<FLU>(),
                    orientation = transform.rotation.To<FLU>()
                }
            };
            ros.Publish("unity_amr/current_pose", poseMsg);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Pose Publish Failed] {e.Message}");
        }
    }
    
    void OnDrawGizmos()
    {
        // 목표 지점 시각화
        if (hasTarget)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(targetPosition, 0.3f);
            Gizmos.DrawLine(transform.position, targetPosition);
        }
        
        // 로봇 방향 표시
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(transform.position, transform.forward * 1f);
    }
}
