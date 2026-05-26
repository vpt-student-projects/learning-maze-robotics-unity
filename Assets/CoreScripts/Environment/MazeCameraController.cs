using UnityEngine;

public class MazeCameraController : MonoBehaviour
{
    [Header("����� ������")]
    public CameraViewMode viewMode = CameraViewMode.OrthographicTop;

    [Header("��������� ��������������� ������")]
    public float orthographicPadding = 5f;
    public float minOrthographicSize = 10f;

    [Header("��������� ������������� ������")]
    public float perspectiveHeight = 30f;

    [Header("������")]
    public Camera mazeCamera;
    public MazeGenerator mazeGenerator;

    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private float originalOrthographicSize;
    private bool originalOrthographic;
    private CarController carController;
    private Transform carTransform;

    [Header("Настройки слежения за машинкой")]
    public float followCarHeight = 15f;
    public float followCarZoom = 10f;
    public float followCarMinZoom = 5f;
    public float followCarMaxZoom = 30f;
    public float followCarSmoothSpeed = 5f;

    [Header("Настройки вида от первого лица")]
    public float firstPersonHeight = 1.5f;
    public float firstPersonOffset = 0.5f;

    public enum CameraViewMode
    {
        FullMazeView,      // Обзор на весь лабиринт
        FollowCar,         // Слежение за машинкой
        FirstPerson,       // Вид от первого лица
        OrthographicTop,   // Старый режим (для совместимости)
        PerspectiveTop,    // Старый режим (для совместимости)
        Original           // Старый режим (для совместимости)
    }

    void Start()
    {
        if (mazeCamera == null)
            mazeCamera = Camera.main;

        if (mazeGenerator == null)
            mazeGenerator = FindObjectOfType<MazeGenerator>();

        SaveOriginalCameraState();
        SetupCameraView();
    }

    void Update()
    {
        // Обновляем камеру в режиме слежения за машинкой
        if (viewMode == CameraViewMode.FollowCar)
        {
            UpdateFollowCarCamera();
        }
        else if (viewMode == CameraViewMode.FirstPerson)
        {
            UpdateFirstPersonCamera();
        }
    }

    [ContextMenu("��������� ������ �� ��������")]
    public void SetupCameraView()
    {
        if (mazeGenerator == null || mazeCamera == null)
        {
            Debug.LogWarning("MazeGenerator ��� Camera �� ���������!");
            return;
        }

        switch (viewMode)
        {
            case CameraViewMode.FullMazeView:
                SetupFullMazeView();
                break;
            case CameraViewMode.FollowCar:
                SetupFollowCarView();
                break;
            case CameraViewMode.FirstPerson:
                SetupFirstPersonView();
                break;
            case CameraViewMode.OrthographicTop:
                SetupOrthographicTopView();
                break;
            case CameraViewMode.PerspectiveTop:
                SetupPerspectiveTopView();
                break;
            case CameraViewMode.Original:
                RestoreOriginalCamera();
                break;
        }

        //Debug.Log($"������ ��������� � ������: {viewMode}");
    }

    private void SetupOrthographicTopView()
    {
        // ������������ ������� ���������
        float mazeWidth = mazeGenerator.GetTotalWidth();
        float mazeDepth = mazeGenerator.GetTotalDepth();

        // ������� ����� ���������
        Vector3 mazeCenter = new Vector3(
            mazeWidth * 0.5f + mazeGenerator.wallOffset.x,
            0f,
            mazeDepth * 0.5f + mazeGenerator.wallOffset.z
        );

        // ����������� ������ ������ ������
        mazeCamera.transform.position = new Vector3(
            mazeCenter.x,
            Mathf.Max(mazeWidth, mazeDepth) * 0.5f + 10f, // ������ ������� �� ������� ���������
            mazeCenter.z
        );
        mazeCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        // ����������� ��������������� ������
        mazeCamera.orthographic = true;

        // ������������ ��������������� ������ ����� �������� ���� ��������
        float aspectRatio = (float)Screen.width / Screen.height;
        float requiredSizeX = (mazeWidth * 0.5f + orthographicPadding) / aspectRatio;
        float requiredSizeZ = (mazeDepth * 0.5f + orthographicPadding);

        mazeCamera.orthographicSize = Mathf.Max(requiredSizeX, requiredSizeZ, minOrthographicSize);

        Debug.Log($"��������������� ���: ������={mazeCamera.orthographicSize:F1}, ��������={mazeWidth:F1}x{mazeDepth:F1}");
    }

    private void SetupPerspectiveTopView()
    {
        // ������������ ������� ���������
        float mazeWidth = mazeGenerator.GetTotalWidth();
        float mazeDepth = mazeGenerator.GetTotalDepth();

        // ������� ����� ���������
        Vector3 mazeCenter = new Vector3(
            mazeWidth * 0.5f + mazeGenerator.wallOffset.x,
            0f,
            mazeDepth * 0.5f + mazeGenerator.wallOffset.z
        );

        // ����������� ������ ������ � ��������� �������� ��� ������� ������
        mazeCamera.transform.position = new Vector3(
            mazeCenter.x,
            perspectiveHeight,
            mazeCenter.z - mazeDepth * 0.2f // ������� ������� ����� ��� ������� ������
        );

        // ������� �� ����� ��������� � ��������� ��������
        mazeCamera.transform.LookAt(mazeCenter);
        mazeCamera.orthographic = false;

        // ����������� ���� ������ ����� �������� ���� ��������
        float distanceToCenter = Vector3.Distance(mazeCamera.transform.position, mazeCenter);
        float requiredFOV = Mathf.Atan(Mathf.Max(mazeWidth, mazeDepth) * 0.6f / distanceToCenter) * Mathf.Rad2Deg * 2f;
        mazeCamera.fieldOfView = Mathf.Clamp(requiredFOV, 40f, 80f);

        Debug.Log($"������������� ���: FOV={mazeCamera.fieldOfView:F1}, ������={perspectiveHeight:F1}");
    }

    private void RestoreOriginalCamera()
    {
        mazeCamera.transform.position = originalPosition;
        mazeCamera.transform.rotation = originalRotation;
        mazeCamera.orthographic = originalOrthographic;
        if (mazeCamera.orthographic)
            mazeCamera.orthographicSize = originalOrthographicSize;
    }

    private void SaveOriginalCameraState()
    {
        if (mazeCamera != null)
        {
            originalPosition = mazeCamera.transform.position;
            originalRotation = mazeCamera.transform.rotation;
            originalOrthographic = mazeCamera.orthographic;
            originalOrthographicSize = mazeCamera.orthographicSize;
        }
    }

    // ������ ��� ����� ������ ����� ���
    public void SetOrthographicTopMode()
    {
        SetFullMazeViewMode();
    }

    public void SetPerspectiveTopMode()
    {
        viewMode = CameraViewMode.PerspectiveTop;
        SetupCameraView();
    }

    public void SetOriginalMode()
    {
        viewMode = CameraViewMode.Original;
        SetupCameraView();
    }

    private void SetupFullMazeView()
    {
        // Вычисляем размеры лабиринта
        float mazeWidth = mazeGenerator.GetTotalWidth();
        float mazeDepth = mazeGenerator.GetTotalDepth();

        // Центр лабиринта
        Vector3 mazeCenter = new Vector3(
            mazeWidth * 0.5f + mazeGenerator.wallOffset.x,
            0f,
            mazeDepth * 0.5f + mazeGenerator.wallOffset.z
        );

        // Позиционируем камеру сверху по центру
        float maxDimension = Mathf.Max(mazeWidth, mazeDepth);
        float cameraHeight = maxDimension * 0.6f + 20f; // Высота зависит от размера лабиринта
        
        mazeCamera.transform.position = new Vector3(
            mazeCenter.x,
            cameraHeight,
            mazeCenter.z
        );
        mazeCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        // Устанавливаем ортографическую проекцию
        mazeCamera.orthographic = true;

        // Рассчитываем ортографический размер чтобы охватить весь лабиринт
        float aspectRatio = (float)Screen.width / Screen.height;
        float requiredSizeX = (mazeWidth * 0.5f + orthographicPadding) / aspectRatio;
        float requiredSizeZ = (mazeDepth * 0.5f + orthographicPadding);

        mazeCamera.orthographicSize = Mathf.Max(requiredSizeX, requiredSizeZ, minOrthographicSize);

        Debug.Log($"📷 Обзор на весь лабиринт: размер={mazeCamera.orthographicSize:F1}, лабиринт={mazeWidth:F1}x{mazeDepth:F1}");
    }

    private void SetupFollowCarView()
    {
        // Находим машинку
        if (carController == null)
        {
            carController = mazeGenerator?.GetCarController();
        }

        if (carController == null || !carController.IsCarReady())
        {
            Debug.LogWarning("⚠️ Машинка не найдена для режима слежения. Переключаюсь на обзор лабиринта.");
            viewMode = CameraViewMode.FullMazeView;
            SetupFullMazeView();
            return;
        }

        // Получаем трансформ машинки
        if (carTransform == null)
        {
            var carInstance = carController.transform.Find("PlayerCar");
            if (carInstance == null)
            {
                // Пытаемся найти дочерний объект с машинкой
                foreach (Transform child in carController.transform)
                {
                    if (child.name.Contains("Car") || child.name.Contains("Player"))
                    {
                        carTransform = child;
                        break;
                    }
                }
            }
            else
            {
                carTransform = carInstance;
            }
        }

        if (carTransform == null)
        {
            Debug.LogWarning("⚠️ Трансформ машинки не найден. Переключаюсь на обзор лабиринта.");
            viewMode = CameraViewMode.FullMazeView;
            SetupFullMazeView();
            return;
        }

        // Устанавливаем начальную позицию камеры
        UpdateFollowCarCamera();
        mazeCamera.orthographic = false;
        mazeCamera.fieldOfView = 60f;

        Debug.Log("📷 Режим слежения за машинкой активирован");
    }

    private void UpdateFollowCarCamera()
    {
        if (carTransform == null)
        {
            if (carController == null)
            {
                carController = mazeGenerator?.GetCarController();
            }

            if (carController != null && carController.IsCarReady())
            {
                var carInstance = carController.transform.Find("PlayerCar");
                if (carInstance == null)
                {
                    foreach (Transform child in carController.transform)
                    {
                        if (child.name.Contains("Car") || child.name.Contains("Player"))
                        {
                            carTransform = child;
                            break;
                        }
                    }
                }
                else
                {
                    carTransform = carInstance;
                }
            }

            if (carTransform == null)
                return;
        }

        Vector3 carPosition = carTransform.position;
        Vector3 targetPosition = carPosition + Vector3.up * followCarHeight;
        targetPosition += carTransform.forward * (-followCarZoom * 0.5f); // Отступ назад

        // Плавное перемещение камеры
        mazeCamera.transform.position = Vector3.Lerp(
            mazeCamera.transform.position,
            targetPosition,
            Time.deltaTime * followCarSmoothSpeed
        );

        // Камера смотрит на машинку
        Vector3 lookAtPosition = carPosition + Vector3.up * 1f;
        mazeCamera.transform.LookAt(lookAtPosition);
    }

    private void SetupFirstPersonView()
    {
        // Находим машинку
        if (carController == null)
        {
            carController = mazeGenerator?.GetCarController();
        }

        if (carController == null || !carController.IsCarReady())
        {
            Debug.LogWarning("⚠️ Машинка не найдена для режима от первого лица. Переключаюсь на обзор лабиринта.");
            viewMode = CameraViewMode.FullMazeView;
            SetupFullMazeView();
            return;
        }

        // Получаем трансформ машинки
        if (carTransform == null)
        {
            var carInstance = carController.transform.Find("PlayerCar");
            if (carInstance == null)
            {
                foreach (Transform child in carController.transform)
                {
                    if (child.name.Contains("Car") || child.name.Contains("Player"))
                    {
                        carTransform = child;
                        break;
                    }
                }
            }
            else
            {
                carTransform = carInstance;
            }
        }

        if (carTransform == null)
        {
            Debug.LogWarning("⚠️ Трансформ машинки не найден. Переключаюсь на обзор лабиринта.");
            viewMode = CameraViewMode.FullMazeView;
            SetupFullMazeView();
            return;
        }

        // Устанавливаем начальную позицию камеры
        UpdateFirstPersonCamera();
        mazeCamera.orthographic = false;
        mazeCamera.fieldOfView = 75f;

        Debug.Log("📷 Режим от первого лица активирован");
    }

    private void UpdateFirstPersonCamera()
    {
        if (carTransform == null)
        {
            if (carController == null)
            {
                carController = mazeGenerator?.GetCarController();
            }

            if (carController != null && carController.IsCarReady())
            {
                var carInstance = carController.transform.Find("PlayerCar");
                if (carInstance == null)
                {
                    foreach (Transform child in carController.transform)
                    {
                        if (child.name.Contains("Car") || child.name.Contains("Player"))
                        {
                            carTransform = child;
                            break;
                        }
                    }
                }
                else
                {
                    carTransform = carInstance;
                }
            }

            if (carTransform == null)
                return;
        }

        // Камера прикреплена к машинке
        Vector3 carPosition = carTransform.position;
        Vector3 forward = carTransform.forward;
        Vector3 up = carTransform.up;

        Vector3 cameraPosition = carPosition + up * firstPersonHeight + forward * firstPersonOffset;
        mazeCamera.transform.position = cameraPosition;
        mazeCamera.transform.rotation = carTransform.rotation;
    }

    public void SetFullMazeViewMode()
    {
        viewMode = CameraViewMode.FullMazeView;
        SetupCameraView();
    }

    public void SetFollowCarMode()
    {
        viewMode = CameraViewMode.FollowCar;
        SetupCameraView();
    }

    public void SetFirstPersonMode()
    {
        viewMode = CameraViewMode.FirstPerson;
        SetupCameraView();
    }

    public void SetFollowCarZoom(float zoom)
    {
        followCarZoom = Mathf.Clamp(zoom, followCarMinZoom, followCarMaxZoom);
    }

    public float GetFollowCarZoom()
    {
        return followCarZoom;
    }

    //      ���������� ������ ��� ��������� ������� ���������
    public void UpdateCameraForNewMaze()
    {
        SetupCameraView();
    }

    void OnValidate()
    {
        // �������������� ���������� � ��������� ��� ��������� ��������
        if (Application.isPlaying && mazeCamera != null)
        {
            SetupCameraView();
        }
    }

    // ����� ��� ������� - ���������� ������� ���������
    void OnDrawGizmosSelected()
    {
        if (mazeGenerator != null)
        {
            float mazeWidth = mazeGenerator.GetTotalWidth();
            float mazeDepth = mazeGenerator.GetTotalDepth();
            Vector3 mazeCenter = new Vector3(
                mazeWidth * 0.5f + mazeGenerator.wallOffset.x,
                0f,
                mazeDepth * 0.5f + mazeGenerator.wallOffset.z
            );

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(mazeCenter, new Vector3(mazeWidth, 0.1f, mazeDepth));
        }
    }
}