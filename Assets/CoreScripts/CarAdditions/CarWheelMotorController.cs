using UnityEngine;

/// <summary>
/// Управление колёсами через WheelCollider (только скорость, без поворота руля).
/// Активно только в режиме API_Motors (сложность Hard).
/// </summary>
public class CarWheelMotorController : MonoBehaviour
{
    private static readonly string[] WheelObjectNames =
    {
        "BackLeftWheel",
        "BackRightWheel",
        "FrontRightWheel",
        "FrontLeftWheel"
    };

    [Header("Моторы")]
    [SerializeField] private float maxMotorTorque = 800f;
    [SerializeField] private float maxLinearSpeed = 2.5f;
    [SerializeField] private float maxTurnSpeedDeg = 120f;
    [SerializeField] private float speedSmoothing = 8f;

    private WheelCollider frontLeftWheel;
    private WheelCollider frontRightWheel;
    private WheelCollider backLeftWheel;
    private WheelCollider backRightWheel;
    private Rigidbody carRigidbody;

    private bool isInitialized;
    private bool motorsEnabled;

    private float targetFrontLeft;
    private float targetFrontRight;
    private float targetBackLeft;
    private float targetBackRight;

    private float currentFrontLeft;
    private float currentFrontRight;
    private float currentBackLeft;
    private float currentBackRight;

    public bool IsInitialized => isInitialized;
    public bool MotorsEnabled => motorsEnabled;

    public void InitializeWheels(Transform carRoot)
    {
        frontLeftWheel = null;
        frontRightWheel = null;
        backLeftWheel = null;
        backRightWheel = null;

        if (carRoot == null)
        {
            Debug.LogError("CarWheelMotorController: carRoot is null");
            isInitialized = false;
            return;
        }

        carRigidbody = carRoot.GetComponent<Rigidbody>();

        foreach (string wheelName in WheelObjectNames)
        {
            Transform wheelTransform = FindChildRecursive(carRoot, wheelName);
            if (wheelTransform == null)
            {
                Debug.LogWarning($"CarWheelMotorController: не найден объект колеса '{wheelName}'");
                continue;
            }

            WheelCollider collider = wheelTransform.GetComponent<WheelCollider>();
            if (collider == null)
            {
                Debug.LogWarning($"CarWheelMotorController: на '{wheelName}' нет WheelCollider");
                continue;
            }

            collider.steerAngle = 0f;

            switch (wheelName)
            {
                case "FrontLeftWheel": frontLeftWheel = collider; break;
                case "FrontRightWheel": frontRightWheel = collider; break;
                case "BackLeftWheel": backLeftWheel = collider; break;
                case "BackRightWheel": backRightWheel = collider; break;
            }
        }

        isInitialized = frontLeftWheel != null && frontRightWheel != null
                        && backLeftWheel != null && backRightWheel != null;

        if (isInitialized)
        {
            Debug.Log("CarWheelMotorController: все 4 колеса найдены");
        }
        else
        {
            Debug.LogError("CarWheelMotorController: не все колёса инициализированы");
        }
    }

    public void SetMotorsEnabled(bool enabled)
    {
        motorsEnabled = enabled && isInitialized;

        if (!motorsEnabled)
        {
            StopMotorsImmediate();
        }
    }

    /// <summary>
    /// Задать скорости моторов (-1..1): отрицательные — назад, положительные — вперёд.
    /// </summary>
    public void SetWheelSpeeds(float frontLeft, float frontRight, float backLeft, float backRight)
    {
        if (!motorsEnabled) return;

        targetFrontLeft = Mathf.Clamp(frontLeft, -1f, 1f);
        targetFrontRight = Mathf.Clamp(frontRight, -1f, 1f);
        targetBackLeft = Mathf.Clamp(backLeft, -1f, 1f);
        targetBackRight = Mathf.Clamp(backRight, -1f, 1f);
    }

    public void StopMotors()
    {
        targetFrontLeft = 0f;
        targetFrontRight = 0f;
        targetBackLeft = 0f;
        targetBackRight = 0f;
    }

    public void StopMotorsImmediate()
    {
        StopMotors();
        currentFrontLeft = 0f;
        currentFrontRight = 0f;
        currentBackLeft = 0f;
        currentBackRight = 0f;
        ApplyTorqueToAllWheels(0f, 0f, 0f, 0f);
        if (frontLeftWheel != null) frontLeftWheel.motorTorque = 0f;
        if (frontRightWheel != null) frontRightWheel.motorTorque = 0f;
        if (backLeftWheel != null) backLeftWheel.motorTorque = 0f;
        if (backRightWheel != null) backRightWheel.motorTorque = 0f;
    }

    public float GetFrontLeftSpeed() => currentFrontLeft;
    public float GetFrontRightSpeed() => currentFrontRight;
    public float GetBackLeftSpeed() => currentBackLeft;
    public float GetBackRightSpeed() => currentBackRight;

    void FixedUpdate()
    {
        if (!motorsEnabled || !isInitialized) return;

        float dt = Time.fixedDeltaTime;
        currentFrontLeft = Mathf.MoveTowards(currentFrontLeft, targetFrontLeft, speedSmoothing * dt);
        currentFrontRight = Mathf.MoveTowards(currentFrontRight, targetFrontRight, speedSmoothing * dt);
        currentBackLeft = Mathf.MoveTowards(currentBackLeft, targetBackLeft, speedSmoothing * dt);
        currentBackRight = Mathf.MoveTowards(currentBackRight, targetBackRight, speedSmoothing * dt);

        ApplyTorqueToAllWheels(currentFrontLeft, currentFrontRight, currentBackLeft, currentBackRight);

        if (carRigidbody != null && carRigidbody.isKinematic)
        {
            ApplyKinematicDrive(dt);
        }
    }

    private void ApplyTorqueToAllWheels(float fl, float fr, float bl, float br)
    {
        SetWheelMotorTorque(frontLeftWheel, fl);
        SetWheelMotorTorque(frontRightWheel, fr);
        SetWheelMotorTorque(backLeftWheel, bl);
        SetWheelMotorTorque(backRightWheel, br);
    }

    private void SetWheelMotorTorque(WheelCollider wheel, float speed01)
    {
        if (wheel == null) return;
        wheel.steerAngle = 0f;
        wheel.brakeTorque = Mathf.Abs(speed01) < 0.01f ? 50f : 0f;
        wheel.motorTorque = speed01 * maxMotorTorque;
    }

    private void ApplyKinematicDrive(float dt)
    {
        float left = (currentFrontLeft + currentBackLeft) * 0.5f;
        float right = (currentFrontRight + currentBackRight) * 0.5f;

        float forward = (left + right) * 0.5f * maxLinearSpeed;
        float turnSpeedDeg = (right - left) * 0.5f * maxTurnSpeedDeg;

        Vector3 position = carRigidbody.position;
        Quaternion rotation = carRigidbody.rotation;

        if (Mathf.Abs(turnSpeedDeg) > 0.0001f)
        {
            rotation *= Quaternion.Euler(0f, turnSpeedDeg * dt, 0f);
        }

        Vector3 forwardDir = rotation * Vector3.forward;
        position += forwardDir * (forward * dt);

        carRigidbody.MovePosition(position);
        carRigidbody.MoveRotation(rotation);
    }

    private static Transform FindChildRecursive(Transform parent, string childName)
    {
        if (parent.name == childName)
            return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindChildRecursive(parent.GetChild(i), childName);
            if (found != null)
                return found;
        }

        return null;
    }
}
