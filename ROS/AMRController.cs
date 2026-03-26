using UnityEngine;
using RosMessageTypes.Tf2;
using Unity.Robotics.ROSTCPConnector;
using Unity.Robotics.ROSTCPConnector.ROSGeometry;
using RosMessageTypes.Geometry;
using RosMessageTypes.Std;
using RosMessageTypes.Nav;
using RosMessageTypes.Rosgraph;
using RosMessageTypes.BuiltinInterfaces;

public class AMRController : MonoBehaviour
{
    private ROSConnection ros;
    private Rigidbody rb;
    
    [Header("ROS Settings")]
    [SerializeField] private string baseFrameId = "base_link";
    [SerializeField] private string odomFrameId = "odom";
    [SerializeField] private string cmdVelTopic = "/cmd_vel"; // Nav2가 보내는 속도 명령
    [SerializeField] private float publishRate = 30f; // Odom은 자주 보내야 함
    
    // ⭐ 자체 이동 설정(속도, 가속도 등)은 제거됨 -> Nav2가 제어함
    
    // 수신받은 속도 명령 저장용
    private Vector3 targetLinearVelocity;
    private float targetAngularVelocity;
    
    // Odometry 계산용
    private Vector3 lastPosition;
    private Quaternion lastRotation;
    private float lastUpdateTime;
    private float nextPublishTime;
    private float publishInterval;

    public static TimeMsg CurrentTimestamp { get; private set; }
    private bool isInitialized = false;

    void Awake()
    {
        CurrentTimestamp = new TimeMsg { sec = 0, nanosec = 0 };
        lastPosition = transform.position;
        lastRotation = transform.rotation;
        lastUpdateTime = Time.time;
    }

    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();
        rb = GetComponent<Rigidbody>();
        
        // Rigidbody 물리 설정 (미끄러짐 방지 및 관성 처리)
        rb.useGravity = false;
        rb.linearDamping = 5f; // 명령이 멈추면 금방 서도록 설정
        rb.angularDamping = 5f;
        rb.constraints = RigidbodyConstraints.FreezePositionY | 
                         RigidbodyConstraints.FreezeRotationX | 
                         RigidbodyConstraints.FreezeRotationZ;

        // Publisher 등록 (Unity -> ROS)
        ros.RegisterPublisher<TFMessageMsg>("/tf");
        ros.RegisterPublisher<ClockMsg>("/clock");
        ros.RegisterPublisher<OdometryMsg>("/odom");
        
        // ⭐ Subscriber 변경 (ROS Nav2 -> Unity)
        // Nav2가 계산한 속도 명령(Twist)을 받습니다.
        ros.Subscribe<TwistMsg>(cmdVelTopic, ReceiveVelocityCommand);

        publishInterval = 1f / publishRate;
        nextPublishTime = Time.time;
        isInitialized = true;

        // Clock 발행 (Sim time 사용 시 필수)
        InvokeRepeating("PublishClock", 0.05f, 0.01f); // 100Hz
    }

    void Update()
    {
        if (isInitialized && Time.time >= nextPublishTime)
        {
            UpdateTimestamp();
            PublishOdomAndTF(); // Odom과 TF는 세트입니다.
            nextPublishTime += publishInterval;
        }
    }

    void FixedUpdate()
    {
        // ⭐ Nav2의 명령대로 물리 이동 처리
        MoveRobotByCmdVel();
    }

    /// <summary>
    /// Nav2로부터 cmd_vel(선속도, 각속도) 수신
    /// </summary>
    void ReceiveVelocityCommand(TwistMsg twistMsg)
    {
        // ROS(FLU) -> Unity(RUF) 좌표계 변환
        // 선속도: ROS x(전진) -> Unity z(전진)
        // 각속도: ROS z(회전) -> Unity y(회전) (부호 반대 주의: Unity는 좌수좌표계)
        
        // <FLU> 확장 메서드를 사용하면 편리하지만, 원리를 위해 수동 변환 예시를 듭니다.
        // targetLinearVelocity = (float)twistMsg.linear.x * transform.forward; // 로컬 기준 전진
        
        // Unity Robotics 패키지의 변환 기능 사용 권장:
        Vector3 linear = twistMsg.linear.From<FLU>();
        Vector3 angular = twistMsg.angular.From<FLU>();

        // Unity에서 로봇의 전진 방향은 Z축이므로 linear.z 성분을 사용해야 함 (From<FLU>가 X->Z로 바꿔줌)
        // 하지만 cmd_vel은 보통 로봇 기준(Local) 속도이므로 아래와 같이 적용합니다.
        
        targetLinearVelocity = transform.forward * linear.z; 
        targetAngularVelocity = -angular.y; // ROS(CCW+) -> Unity(CW+) 회전 방향 보정 필요할 수 있음
    }

    /// <summary>
    /// 수신받은 속도 명령을 Rigidbody에 적용
    /// </summary>
    void MoveRobotByCmdVel()
    {
        // 1. 선속도 적용 (직접 속도 제어)
        // Nav2가 멈추라고 하면(0) 즉시 멈춰야 하므로 보간 없이 적용하거나 아주 짧게 보간
        Vector3 currentVelocity = rb.linearVelocity;
        Vector3 newVelocity = new Vector3(targetLinearVelocity.x, currentVelocity.y, targetLinearVelocity.z);
        rb.linearVelocity = Vector3.Lerp(currentVelocity, newVelocity, Time.fixedDeltaTime * 10f);

        // 2. 각속도 적용
        // Unity Rigidbody angularVelocity는 Radian/s 단위
        Vector3 newAngularVel = new Vector3(0, Mathf.Deg2Rad * targetAngularVelocity, 0); 
        // *참고: 만약 targetAngularVelocity가 이미 Radian이면 변환 불필요 (TwistMsg는 보통 Radian)
        
        // TwistMsg는 Radian/s 이므로 바로 적용 (Y축 회전)
        // From<FLU> 변환 시 y축 부호가 바뀔 수 있으므로 테스트 필요. 보통은 -angular.y
        rb.angularVelocity = new Vector3(0, -targetAngularVelocity * Mathf.Rad2Deg, 0); // Unity는 Degree 기반 연산이 많지만 angularVelocity 속성은 Radian을 씁니다.
        
        // 수정: rb.angularVelocity는 Vector3(x, y, z) 라디안 단위입니다.
        // ROS Twist.angular.z (rad/s) -> Unity y (rad/s)
        // ROS는 왼손법칙(엄지 위) vs Unity 오른손법칙 등 좌표계 차이로 부호 확인 필수.
        // 일반적으로:
        rb.angularVelocity = new Vector3(0, (float)-targetAngularVelocity, 0);
    }

    void UpdateTimestamp()
    {
        // Clock 메시지용 시간 업데이트
        CurrentTimestamp = new TimeMsg
        {
            sec = (int)Time.time,
            nanosec = (uint)((Time.time % 1) * 1e9)
        };
    }

    void PublishClock()
    {
        if (!isInitialized) return;
        UpdateTimestamp();
        ros.Publish("/clock", new ClockMsg { clock = CurrentTimestamp });
    }

    void PublishOdomAndTF()
    {
        if (!isInitialized) return;

        // --- 1. Odometry 계산 ---
        float dt = Time.time - lastUpdateTime;
        if (dt <= 0) return;

        Vector3 deltaPos = transform.position - lastPosition;
        Quaternion deltaRot = transform.rotation * Quaternion.Inverse(lastRotation);

        // 속도 계산
        Vector3 linearVel = deltaPos / dt;
        Vector3 angularVel = deltaRot.eulerAngles / dt * Mathf.Deg2Rad; // Radian으로 변환

        // --- 2. TF (odom -> base_link) 발행 ---
        // Nav2는 정확한 TF 트리가 필수입니다.
        var tfMsg = new TFMessageMsg
        {
            transforms = new TransformStampedMsg[]
            {
                new TransformStampedMsg
                {
                    header = new HeaderMsg { stamp = CurrentTimestamp, frame_id = odomFrameId },
                    child_frame_id = baseFrameId,
                    transform = new TransformMsg
                    {
                        translation = transform.position.To<FLU>(),
                        rotation = transform.rotation.To<FLU>()
                    }
                }
            }
        };
        ros.Publish("/tf", tfMsg);

        // --- 3. Odometry 메시지 발행 ---
        var odomMsg = new OdometryMsg
        {
            header = new HeaderMsg { stamp = CurrentTimestamp, frame_id = odomFrameId },
            child_frame_id = baseFrameId,
            pose = new PoseWithCovarianceMsg
            {
                pose = new PoseMsg
                {
                    position = transform.position.To<FLU>(),
                    orientation = transform.rotation.To<FLU>()
                }
            },
            twist = new TwistWithCovarianceMsg
            {
                twist = new TwistMsg
                {
                    linear = linearVel.To<FLU>(),
                    angular = angularVel.To<FLU>()
                }
            }
        };
        ros.Publish("/odom", odomMsg);

        // 상태 업데이트
        lastPosition = transform.position;
        lastRotation = transform.rotation;
        lastUpdateTime = Time.time;
    }
}
