using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using Unity.Robotics.ROSTCPConnector.ROSGeometry; // ⭐ 좌표계 변환용
using RosMessageTypes.Sensor;
using RosMessageTypes.Geometry;
using RosMessageTypes.Std;
using System;

public class LiDARSensor : MonoBehaviour
{
    [Header("ROS Settings")]
    [SerializeField] private string scanTopicName = "/scan";
    [SerializeField] private string frameId = "laser_frame";
    [SerializeField] private float publishRate = 10f; // Hz
    [SerializeField] private bool publishSensorTF = true; // ⭐ URDF 있으면 false로 설정
    
    [Header("LiDAR Specifications")]
    [SerializeField] private float scanRate = 10f;
    [SerializeField] private float minAngle = -180f; // degrees
    [SerializeField] private float maxAngle = 180f;  // degrees
    [SerializeField] private float angleIncrement = 1f; // degrees
    [SerializeField] private float minRange = 0.12f; // meters
    [SerializeField] private float maxRange = 10f;   // meters
    
    [Header("Detection Settings")]
    [SerializeField] private LayerMask detectionLayers = ~0; // 모든 레이어
    [SerializeField] private float raycastOriginOffset = 0.05f; // ⭐ 자기 충돌 방지
    [SerializeField] private bool useInfinityForNoHit = false; // ⭐ Inf vs MaxRange
    [Tooltip("로봇 자신의 레이어를 detectionLayers에서 제외하세요")]
    
    [Header("Visualization")]
    [SerializeField] private bool drawDebugRays = true;
    [SerializeField] private Color hitColor = Color.red;
    [SerializeField] private Color missColor = Color.green;
    [SerializeField] private float debugRayDuration = 0.1f;
    
    private ROSConnection rosConnection;
    private float nextPublishTime;
    private float publishInterval;
    private float tfPublishInterval;
    private float nextTFPublishTime;
    
    private int rayCount;
    private float angleIncrementRad;
    
    void Start()
    {
        rosConnection = ROSConnection.GetOrCreateInstance();
        
        // Publisher 등록
        rosConnection.RegisterPublisher<LaserScanMsg>(scanTopicName);
        
        if (publishSensorTF)
        {
            rosConnection.RegisterPublisher<RosMessageTypes.Tf2.TFMessageMsg>("/tf");
            tfPublishInterval = 0.1f; // 10Hz
            nextTFPublishTime = Time.time + 0.2f;
        }

        // 발행 주기 계산
        publishInterval = 1f / publishRate;
        nextPublishTime = Time.time + publishInterval;
        
        // LiDAR 스펙 계산
        rayCount = Mathf.CeilToInt((maxAngle - minAngle) / angleIncrement) + 1;
        angleIncrementRad = angleIncrement * Mathf.Deg2Rad;
        
        Debug.Log($"[LiDAR] Initialized - Frame: {frameId}, Rays: {rayCount}, Range: {minRange}~{maxRange}m");
        
        // 레이어 마스크 경고
        if (detectionLayers == ~0)
        {
            Debug.LogWarning("[LiDAR] detectionLayers가 모든 레이어를 포함합니다. 로봇 자신의 레이어를 제외하세요!");
        }
    }
    
    void Update()
    {
        // LaserScan 발행
        if (Time.time >= nextPublishTime)
        {
            PublishLaserScan();
            nextPublishTime += publishInterval;
        }
        
        // TF 발행 (URDF 없을 때만)
        if (publishSensorTF && Time.time >= nextTFPublishTime)
        {
            PublishSensorTF();
            nextTFPublishTime += tfPublishInterval;
        }
    }

    /// <summary>
    /// base_link -> laser_frame TF 발행
    /// ⭐ URDF로 관리하는 경우 publishSensorTF를 false로 설정
    /// </summary>
    void PublishSensorTF()
    {
        if (AMRController.CurrentTimestamp == null)
        {
            Debug.LogWarning("[LiDAR] AMRController.CurrentTimestamp is null, skipping TF publish");
            return;
        }
        
        try
        {
            var tfMessage = new RosMessageTypes.Tf2.TFMessageMsg
            {
                transforms = new TransformStampedMsg[]
                {
                    new TransformStampedMsg
                    {
                        header = new HeaderMsg
                        {
                            stamp = AMRController.CurrentTimestamp,
                            frame_id = "base_link"
                        },
                        child_frame_id = frameId,
                        transform = new TransformMsg
                        {
                            // ⭐ ROSGeometry 라이브러리 사용
                            translation = transform.localPosition.To<FLU>(),
                            rotation = transform.localRotation.To<FLU>()
                        }
                    }
                }
            };
            
            rosConnection.Publish("/tf", tfMessage);
        }
        catch (Exception e)
        {
            Debug.LogError($"[LiDAR TF] Publish failed: {e.Message}");
        }
    }

    void PublishLaserScan()
    {
        if (AMRController.CurrentTimestamp == null)
        {
            return;
        }
        
        float[] ranges = new float[rayCount];
        float[] intensities = new float[rayCount];
        
        // ⭐ 레이캐스트 시작점을 약간 앞으로 이동 (자기 충돌 방지)
        Vector3 rayOrigin = transform.position + transform.forward * raycastOriginOffset;
        
        // ⭐ ROS 표준: angle_min부터 반시계방향(CCW)으로 스캔
        // Unity의 Y축 회전은 시계방향이 양수이므로 음수를 곱해 보정
        for (int i = 0; i < rayCount; i++)
        {
            // ROS 좌표계 기준 각도 계산
            float rosAngle = minAngle + (i * angleIncrement);
            
            // ⭐ Unity 좌표계로 변환 (Y축 기준 회전, 방향 반전)
            // ROS의 CCW(+) -> Unity의 CW(-)
            float unityAngle = -rosAngle; 
            
            Vector3 direction = Quaternion.Euler(0, unityAngle, 0) * transform.forward;
            
            RaycastHit hit;
            bool didHit = Physics.Raycast(
                rayOrigin, 
                direction, 
                out hit, 
                maxRange, 
                detectionLayers,
                QueryTriggerInteraction.Ignore // ⭐ 트리거 콜라이더 무시
            );
            
            if (didHit && hit.distance >= minRange)
            {
                ranges[i] = hit.distance;
                intensities[i] = CalculateIntensity(hit);
                
                if (drawDebugRays)
                {
                    Debug.DrawRay(rayOrigin, direction * hit.distance, hitColor, debugRayDuration);
                }
            }
            else
            {
                // ⭐ No hit 처리: Infinity vs MaxRange
                ranges[i] = useInfinityForNoHit ? float.PositiveInfinity : maxRange;
                intensities[i] = 0.0f;
                
                if (drawDebugRays)
                {
                    Debug.DrawRay(rayOrigin, direction * maxRange, missColor, debugRayDuration);
                }
            }
        }

        // LaserScan 메시지 생성
        LaserScanMsg scanMsg = new LaserScanMsg
        {
            header = new HeaderMsg
            {
                stamp = AMRController.CurrentTimestamp,
                frame_id = frameId
            },
            angle_min = minAngle * Mathf.Deg2Rad,
            angle_max = maxAngle * Mathf.Deg2Rad,
            angle_increment = angleIncrementRad,
            time_increment = (1f / scanRate) / rayCount,
            scan_time = 1f / scanRate,
            range_min = minRange,
            range_max = maxRange,
            ranges = ranges,
            intensities = intensities
        };
        
        rosConnection.Publish(scanTopicName, scanMsg);
    }
    
    /// <summary>
    /// 히트한 표면의 반사 강도 계산 (간단한 구현)
    /// </summary>
    float CalculateIntensity(RaycastHit hit)
    {
        // 거리 기반 감쇠
        float distanceFactor = 1.0f - Mathf.Clamp01(hit.distance / maxRange);
        
        // 표면 법선과 레이 방향의 각도 (수직일수록 강함)
        float angleFactor = Mathf.Abs(Vector3.Dot(hit.normal, -hit.transform.forward));
        
        return Mathf.Clamp01(distanceFactor * angleFactor);
    }
    
    void OnDrawGizmosSelected()
    {
        // Scene 뷰에서 LiDAR 범위 시각화
        Gizmos.color = Color.yellow;
        
        Vector3 origin = transform.position + transform.forward * raycastOriginOffset;
        
        // 최소/최대 각도 표시 (Unity 좌표계)
        Vector3 minDirection = Quaternion.Euler(0, -minAngle, 0) * transform.forward;
        Vector3 maxDirection = Quaternion.Euler(0, -maxAngle, 0) * transform.forward;
        
        Gizmos.DrawRay(origin, minDirection * maxRange);
        Gizmos.DrawRay(origin, maxDirection * maxRange);
        
        // 부채꼴 시각화
        int segments = 20;
        float angleStep = (maxAngle - minAngle) / segments;
        Vector3 prevPoint = origin + minDirection * maxRange;
        
        for (int i = 1; i <= segments; i++)
        {
            float angle = minAngle + (i * angleStep);
            Vector3 direction = Quaternion.Euler(0, -angle, 0) * transform.forward;
            Vector3 point = origin + direction * maxRange;
            
            Gizmos.DrawLine(prevPoint, point);
            Gizmos.DrawRay(origin, direction * maxRange * 0.3f);
            
            prevPoint = point;
        }
        
        // 원점 표시
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(origin, 0.05f);
    }
    
    void OnValidate()
    {
        // Inspector에서 값 변경 시 검증
        if (minRange < 0.01f) minRange = 0.01f;
        if (maxRange < minRange) maxRange = minRange + 0.1f;
        if (angleIncrement < 0.1f) angleIncrement = 0.1f;
        if (publishRate < 1f) publishRate = 1f;
        
        // 각도 범위 체크
        if (minAngle < -180f) minAngle = -180f;
        if (maxAngle > 180f) maxAngle = 180f;
        if (minAngle >= maxAngle)
        {
            Debug.LogWarning("[LiDAR] minAngle >= maxAngle! Swapping values.");
            float temp = minAngle;
            minAngle = maxAngle - 1f;
            maxAngle = temp;
        }
    }
}
