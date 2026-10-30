using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;

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

    [Header("장소 내부 UI")]
    public GameObject placeInteriorPanel;
    public TMP_Text placeNameText;
    public TMP_Text placeDescriptionText;

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

    // 처음 카메라 위치와 확대값 저장
    private Vector3 initialCameraPosition;
    private float initialCameraSize;

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

        // 게임 시작 시 장소 내부 화면 숨기기
        if (placeInteriorPanel != null)
        {
            placeInteriorPanel.SetActive(false);
        }

        // 카메라 초기 설정
        mainCamera = Camera.main;

        if (mainCamera != null)
        {
            // 최초 카메라 위치와 확대값 저장
            initialCameraPosition = mainCamera.transform.position;
            initialCameraSize = mainCamera.orthographicSize;

            targetCameraPosition = initialCameraPosition;
            targetCameraSize = initialCameraSize;
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
        // 장소 내부 화면이 열려 있으면 지도 클릭 방지
        if (placeInteriorPanel != null &&
            placeInteriorPanel.activeSelf)
        {
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            // UI 버튼이나 패널 클릭 시 지도 선택 방지
            if (EventSystem.current != null &&
                EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            if (mainCamera == null)
            {
                return;
            }

            Vector3 mousePosition =
                mainCamera.ScreenToWorldPoint(Input.mousePosition);

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
        if (mainCamera == null)
        {
            return;
        }

        // 카메라 부드럽게 이동
        mainCamera.transform.position = Vector3.Lerp(
            mainCamera.transform.position,
            targetCameraPosition,
            cameraMoveSpeed * Time.deltaTime
        );

        // 카메라 확대 및 축소
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
                initialCameraPosition.z
            );

            targetCameraSize = zoomedCameraSize;
        }

        // 선택한 지역의 데이터 가져오기
        LocationData data =
            location.GetComponent<LocationData>();

        if (data != null)
        {
            if (locationInfoPanel != null)
            {
                locationInfoPanel.SetActive(true);
            }

            if (locationNameText != null)
            {
                locationNameText.text = data.locationName;
            }

            if (locationTypeText != null)
            {
                locationTypeText.text =
                    data.locationType.ToString();
            }

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

        float panelWidth = panelRect.rect.width;
        float panelHeight = panelRect.rect.height;

        float canvasLeft = canvasRect.rect.xMin;
        float canvasRight = canvasRect.rect.xMax;

        float canvasBottom = canvasRect.rect.yMin;
        float canvasTop = canvasRect.rect.yMax;

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
            direction =
                availableRight >= availableLeft ? 1f : -1f;
        }

        float panelX =
            localPoint.x +
            direction * (panelWidth / 2f + panelOffset);

        float panelY = localPoint.y;

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
        if (selectedLocation == null)
        {
            Debug.LogWarning("먼저 진입할 지역을 선택하세요.");
            return;
        }

        if (placeInteriorPanel == null)
        {
            Debug.LogWarning(
                "PlaceInteriorPanel이 연결되지 않았습니다."
            );
            return;
        }

        LocationData data =
            selectedLocation.GetComponent<LocationData>();

        if (data == null)
        {
            Debug.LogWarning(
                "선택한 지역에 LocationData가 없습니다."
            );
            return;
        }

        // 장소 이름 표시
        if (placeNameText != null)
        {
            placeNameText.text = data.locationName;
        }

        // 장소 설명 표시
        if (placeDescriptionText != null)
        {
            placeDescriptionText.text = data.description;
        }

        // 지도 정보 패널 숨기기
        if (locationInfoPanel != null)
        {
            locationInfoPanel.SetActive(false);
        }

        // 장소 내부 화면 표시
        placeInteriorPanel.SetActive(true);

        Debug.Log(data.locationName + "에 진입했습니다.");
    }

    public void ExitPlace()
    {
        // 장소 내부 화면 숨기기
        if (placeInteriorPanel != null)
        {
            placeInteriorPanel.SetActive(false);
        }

        // 지도 정보 패널 다시 표시
        if (locationInfoPanel != null &&
            selectedLocation != null)
        {
            locationInfoPanel.SetActive(true);
        }

        Debug.Log("지도 화면으로 돌아왔습니다.");
    }

    // 지도 정보 패널 닫기 및 카메라 초기화
    public void CloseLocationInfo()
    {
        // 선택한 장소의 색상 복원
        if (selectedLocation != null)
        {
            SpriteRenderer renderer =
                selectedLocation.GetComponent<SpriteRenderer>();

            if (renderer != null &&
                originalColors.ContainsKey(selectedLocation))
            {
                renderer.color =
                    originalColors[selectedLocation];
            }
        }

        // 선택 상태 해제
        selectedLocation = null;

        // 정보 패널 닫기
        if (locationInfoPanel != null)
        {
            locationInfoPanel.SetActive(false);
        }

        // 카메라 초기 위치와 확대값으로 복귀
        targetCameraPosition = initialCameraPosition;
        targetCameraSize = initialCameraSize;

        Debug.Log("전체 지도 화면으로 돌아갑니다.");
    }
}