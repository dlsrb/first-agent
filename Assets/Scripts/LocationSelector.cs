using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class LocationSelector : MonoBehaviour
{
    private Dictionary<GameObject, Color> originalColors =
        new Dictionary<GameObject, Color>();

    private GameObject selectedLocation;

    [Header("장소 정보 UI")]
    public GameObject locationInfoPanel;
    public TMP_Text locationNameText;
    public TMP_Text locationTypeText;
    public TMP_Text locationDescriptionText;

    [Header("카메라 설정")]
    public float zoomedCameraSize = 2.5f;
    public float cameraMoveSpeed = 5f;

    [Header("패널 설정")]
    public float panelOffset = 50f;

    private Camera mainCamera;
    private Canvas parentCanvas;

    private RectTransform canvasRect;
    private RectTransform panelRect;

    private Vector3 targetCameraPosition;
    private float targetCameraSize;

    void Start()
    {
        // 게임 시작 시 지역 정보 패널 숨기기
        if (locationInfoPanel != null)
        {
            locationInfoPanel.SetActive(false);

            panelRect =
                locationInfoPanel.GetComponent<RectTransform>();

            parentCanvas =
                locationInfoPanel.GetComponentInParent<Canvas>();

            if (parentCanvas != null)
            {
                canvasRect =
                    parentCanvas.GetComponent<RectTransform>();
            }
        }

        // 카메라 초기 설정
        mainCamera = Camera.main;

        if (mainCamera != null)
        {
            targetCameraPosition = mainCamera.transform.position;
            targetCameraSize = mainCamera.orthographicSize;
        }

        // 기존 지역 색상 저장 로직 유지
        SpriteRenderer[] locations =
            GetComponentsInChildren<SpriteRenderer>();

        foreach (SpriteRenderer location in locations)
        {
            originalColors[location.gameObject] = location.color;
        }
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Vector3 mousePosition =
                Camera.main.ScreenToWorldPoint(Input.mousePosition);

            Vector2 clickPosition =
                new Vector2(mousePosition.x, mousePosition.y);

            Collider2D hit =
                Physics2D.OverlapPoint(clickPosition);

            if (hit != null &&
                originalColors.ContainsKey(hit.gameObject))
            {
                SelectLocation(hit.gameObject);
            }
        }
    }

    void LateUpdate()
    {
        // 카메라가 없으면 종료
        if (mainCamera == null)
        {
            return;
        }

        // 선택한 지역을 중심으로 부드럽게 이동
        mainCamera.transform.position = Vector3.Lerp(
            mainCamera.transform.position,
            targetCameraPosition,
            cameraMoveSpeed * Time.deltaTime
        );

        // 카메라 확대
        if (mainCamera.orthographic)
        {
            mainCamera.orthographicSize = Mathf.Lerp(
                mainCamera.orthographicSize,
                targetCameraSize,
                cameraMoveSpeed * Time.deltaTime
            );
        }

        // 선택된 지역이 있으면 패널 위치 갱신
        if (selectedLocation != null &&
            locationInfoPanel != null &&
            locationInfoPanel.activeSelf)
        {
            UpdatePanelPosition();
        }
    }

    void SelectLocation(GameObject location)
    {
        // 이전에 선택한 지역의 색상 복원
        if (selectedLocation != null)
        {
            SpriteRenderer previousRenderer =
                selectedLocation.GetComponent<SpriteRenderer>();

            if (previousRenderer != null)
            {
                previousRenderer.color =
                    originalColors[selectedLocation];
            }
        }

        // 새 지역 선택
        selectedLocation = location;

        SpriteRenderer currentRenderer =
            selectedLocation.GetComponent<SpriteRenderer>();

        if (currentRenderer != null)
        {
            currentRenderer.color = Color.blue;
        }

        // 선택한 지역의 위치로 카메라 이동
        if (mainCamera != null)
        {
            Vector3 locationPosition = location.transform.position;

            targetCameraPosition = new Vector3(
                locationPosition.x,
                locationPosition.y,
                mainCamera.transform.position.z
            );

            targetCameraSize = zoomedCameraSize;
        }

        // 선택한 지역의 데이터 가져오기
        LocationData data =
            location.GetComponent<LocationData>();

        if (data != null)
        {
            // 정보 패널 표시
            if (locationInfoPanel != null)
            {
                locationInfoPanel.SetActive(true);
            }

            // 지역 이름 표시
            if (locationNameText != null)
            {
                locationNameText.text = data.locationName;
            }

            // 지역 유형 표시
            if (locationTypeText != null)
            {
                locationTypeText.text =
                    data.locationType.ToString();
            }

            // 지역 설명 표시
            if (locationDescriptionText != null)
            {
                locationDescriptionText.text =
                    data.description;
            }

            Debug.Log("장소 이름: " + data.locationName);
            Debug.Log("장소 유형: " + data.locationType);
            Debug.Log("장소 설명: " + data.description);
        }
        else
        {
            Debug.LogWarning(
                location.name + "에 LocationData가 없습니다."
            );
        }
    }

    void UpdatePanelPosition()
    {
        // UI 위치 계산에 필요한 요소가 없으면 종료
        if (mainCamera == null ||
            canvasRect == null ||
            panelRect == null ||
            selectedLocation == null)
        {
            return;
        }

        // 선택한 지역의 화면 좌표 계산
        Vector3 screenPosition =
            mainCamera.WorldToScreenPoint(
                selectedLocation.transform.position
            );

        // 화면 좌표를 Canvas 좌표로 변환
        Vector2 localPoint;

        bool converted =
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                screenPosition,
                null,
                out localPoint
            );

        if (!converted)
        {
            return;
        }

        // 패널 크기와 Canvas 경계 확인
        float panelWidth = panelRect.rect.width;
        float panelHeight = panelRect.rect.height;

        float canvasLeft = canvasRect.rect.xMin;
        float canvasRight = canvasRect.rect.xMax;

        float canvasBottom = canvasRect.rect.yMin;
        float canvasTop = canvasRect.rect.yMax;

        // 기본적으로 지역 오른쪽에 패널 배치
        float availableRight = canvasRight - localPoint.x;
        float availableLeft = localPoint.x - canvasLeft;

        float direction;

        if (availableRight >= panelWidth + panelOffset)
        {
            direction = 1f;
        }
        else if (availableLeft >= panelWidth + panelOffset)
        {
            direction = -1f;
        }
        else
        {
            // 양쪽 모두 공간이 부족하면 더 넓은 쪽 선택
            direction =
                availableRight >= availableLeft ? 1f : -1f;
        }

        // 선택한 지역 옆에 패널 배치
        float panelX =
            localPoint.x +
            direction * (panelWidth / 2f + panelOffset);

        float panelY = localPoint.y;

        // 패널이 화면 밖으로 나가지 않도록 제한
        float minX = canvasLeft + panelWidth / 2f;
        float maxX = canvasRight - panelWidth / 2f;

        float minY = canvasBottom + panelHeight / 2f;
        float maxY = canvasTop - panelHeight / 2f;

        if (minX <= maxX)
        {
            panelX = Mathf.Clamp(panelX, minX, maxX);
        }

        if (minY <= maxY)
        {
            panelY = Mathf.Clamp(panelY, minY, maxY);
        }

        panelRect.anchoredPosition =
            new Vector2(panelX, panelY);
    }
    public void EnterSelectedLocation()
    {
        // 선택한 지역이 없으면 실행하지 않음
        if (selectedLocation == null)
        {
            Debug.LogWarning("먼저 진입할 지역을 선택하세요.");
            return;
        }

        // 선택한 지역의 데이터 가져오기
        LocationData data =
            selectedLocation.GetComponent<LocationData>();

        if (data == null)
        {
            Debug.LogWarning("선택한 지역에 LocationData가 없습니다.");
            return;
        }

        // 진입 결과 출력
        Debug.Log(data.locationName + "에 진입했습니다.");
    }

}