using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;

// 지도 조작 (Locations 오브젝트에 부착)
// 지역 맵·구역 맵 공통: 마커 클릭 → 카메라 확대 + 정보창 → 진입
// [A-4] 구역 마커면 구역 맵으로, 장소 마커면 장소 내부로 들어간다. 마커는 MapView가 데이터대로 만든다.
public class LocationSelector : MonoBehaviour
{
    private MapMarker selectedMarker;

    // [C-6] 시작 흐름 동안 잠긴 곳의 정보창 문구
    public const string LockedText = "[아직 갈 수 없다]";

    [Header("장소 정보 UI")]
    public GameObject locationInfoPanel;
    public TMP_Text locationNameText;
    public TMP_Text locationTypeText;
    public TMP_Text locationDescriptionText;

    [Header("장소 내부 UI")]
    public GameObject placeInteriorPanel;
    public TMP_Text placeNameText;
    public TMP_Text placeDescriptionText;

    [Header("상호작용 시스템")]
    public InteractionController interactionController;

    // [A-4 추가]
    [Header("지도")]
    public MapView mapView;

    [Tooltip("구역 맵에서만 보이는 '지역 지도로' 버튼")]
    public GameObject backToRegionButton;

    // [B-2 추가]
    [Header("전투")]
    public BattleController battleController;

    [Header("카메라 설정")]
    public float zoomedCameraSize = 2.5f;
    public float cameraMoveSpeed = 5f;

    [Tooltip("확대 시 카메라를 장소 기준 오른쪽(+)으로 옮길 거리(월드 단위). 양수면 장소가 화면 왼쪽에 보여 오른쪽 정보창 자리가 생김. 0이면 장소가 화면 중앙")]
    public float zoomedCameraOffsetX = 0f;

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

        // 시작은 지역 맵이므로 뒤로 버튼 숨기기
        if (backToRegionButton != null)
        {
            backToRegionButton.SetActive(false);
        }

        if (mapView == null)
        {
            Debug.LogWarning("LocationSelector에 MapView가 연결되지 않았습니다.");
        }

        if (battleController != null)
        {
            battleController.gameObject.SetActive(false);
            battleController.BattleFinished += HandleBattleFinished;
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
    }

    void Update()
    {
        // 장소 내부 화면·전투 화면이 열려 있으면 지도 클릭 방지
        if (placeInteriorPanel != null &&
            placeInteriorPanel.activeSelf)
        {
            return;
        }

        if (battleController != null && battleController.IsActive)
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

            if (hit != null)
            {
                MapMarker marker = hit.GetComponent<MapMarker>();

                if (marker != null)
                {
                    SelectMarker(marker);
                }
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

        // 선택된 마커가 있으면 패널 위치 갱신
        if (selectedMarker != null &&
            locationInfoPanel != null &&
            locationInfoPanel.activeSelf)
        {
            UpdatePanelPosition();
        }
    }

    void SelectMarker(MapMarker marker)
    {
        // 이전 선택 표시 해제 (색은 건드리지 않음)
        if (selectedMarker != null)
        {
            selectedMarker.SetSelected(false);
        }

        selectedMarker = marker;
        selectedMarker.SetSelected(true);

        // 선택한 마커 위치로 카메라 이동
        if (mainCamera != null)
        {
            Vector3 markerPosition = marker.transform.position;

            targetCameraPosition = new Vector3(
                markerPosition.x + zoomedCameraOffsetX,
                markerPosition.y,
                initialCameraPosition.z
            );

            targetCameraSize = zoomedCameraSize;
        }

        if (locationInfoPanel != null)
        {
            locationInfoPanel.SetActive(true);
        }

        if (locationNameText != null)
        {
            locationNameText.text = marker.DisplayName;
        }

        if (locationTypeText != null)
        {
            locationTypeText.text = marker.TypeText;
        }

        if (locationDescriptionText != null)
        {
            locationDescriptionText.text = marker.Locked ? LockedText : marker.Description;
        }

        // [C-6] 시작 흐름 동안 잠긴 곳은 진입 버튼 꺼짐
        var enter = locationInfoPanel != null ? locationInfoPanel.transform.Find("EnterButton") : null;
        if (enter != null) enter.GetComponent<UnityEngine.UI.Button>().interactable = !marker.Locked;

        Debug.Log("선택: " + marker.DisplayName + " (" + marker.TypeText + ")");
    }

    void UpdatePanelPosition()
    {
        if (mainCamera == null ||
            canvasRect == null ||
            panelRect == null ||
            selectedMarker == null)
        {
            return;
        }

        // 선택한 마커의 화면 좌표 계산
        Vector3 screenPosition =
            mainCamera.WorldToScreenPoint(
                selectedMarker.transform.position
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

    // 진입하기 버튼 (EnterButton OnClick). 구역 마커면 구역 맵, 장소 마커면 장소 내부
    public void EnterSelectedLocation()
    {
        if (selectedMarker == null)
        {
            Debug.LogWarning("먼저 진입할 곳을 선택하세요.");
            return;
        }

        if (selectedMarker.Locked)
        {
            Debug.Log("아직 갈 수 없는 곳입니다 (시작 흐름).");
            return;
        }

        if (selectedMarker.IsArea)
        {
            EnterArea(selectedMarker.Area);
            return;
        }

        if (placeInteriorPanel == null)
        {
            Debug.LogWarning(
                "PlaceInteriorPanel이 연결되지 않았습니다."
            );
            return;
        }

        PlaceData data = selectedMarker.Place;

        if (data == null)
        {
            Debug.LogWarning("선택한 마커에 PlaceData가 없습니다.");
            return;
        }

        // [B-2] 전투 장소면 바로 전투 (스토리 전투의 만화는 B-4)
        if (data.battle.mode != BattleMode.None && battleController != null)
        {
            StartBattleAt(data);
            return;
        }

        OpenInterior(data);
    }

    // [B-2] 장소 내부 화면 열기 (진입·패배 후 병원 복귀에서 사용)
    void OpenInterior(PlaceData data)
    {
        // 현재 장소 데이터를 상호작용 시스템에 전달
        if (interactionController != null)
        {
            interactionController.SetPlaceData(data);
        }
        else
        {
            Debug.LogWarning("InteractionController가 연결되지 않았습니다.");
        }

        // [C-6] 시작 병원에 처음 들어가면 모든 장소가 열린다
        bool unlocked = StartFlowController.TryUnlock(GameStateManager.Instance.State, mapView != null ? mapView.economy : null, data);

        // [C-2] 병원: 들어가면 복귀 지점이 된다 (성장경제규칙 3번)
        if (data.IsHospital && mapView != null && mapView.CurrentArea != null)
        {
            var player = GameStateManager.Instance.State.player;
            player.respawnAreaId = mapView.CurrentArea.areaId;
            player.respawnPlaceId = data.locationId;

            if (interactionController != null)
            {
                interactionController.ShowNotice("[이 병원이 복귀 지점이 되었다]");
            }
        }

        if (unlocked)
        {
            NoticeToast.Show(StartFlowController.UnlockNotice);
            RefreshMap();   // 잠긴 마커를 풀어서 다시 그린다 (나가면 구역 맵)
        }

        // 장소 이름 표시
        if (placeNameText != null)
        {
            placeNameText.text = data.placeName;
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
        if (placeInteriorPanel != null)
        {
            placeInteriorPanel.SetActive(true);
        }

        Debug.Log(data.placeName + "에 진입했습니다.");
    }

    // ---------- [B-2] 전투 ----------

    void StartBattleAt(PlaceData place)
    {
        AreaData area = mapView != null ? mapView.CurrentArea : null;

        // 전투가 끝나면 들어갔던 구역 맵(선택 없음, 카메라 초기 위치)으로 돌아온다
        ClearSelection();
        ResetCameraImmediately();

        if (!battleController.StartBattle(place, area))
        {
            Debug.LogWarning(place.placeName + ": 전투를 시작하지 못했습니다.");
        }
    }

    void HandleBattleFinished(BattleOutcome outcome, PlaceData place)
    {
        if (outcome != BattleOutcome.Lose)
        {
            Debug.Log("전투 종료 (" + outcome + "), 구역 맵으로 돌아왔습니다.");
            return;
        }

        // 패배: 마지막으로 들른 병원(복귀 장소)에서 시작
        var settings = battleController.settings;
        var player = GameStateManager.Instance.State.player;
        AreaData area = FindArea(player.respawnAreaId) ?? (settings != null ? settings.defaultRespawnArea : null);
        PlaceData respawn = FindPlace(area, player.respawnPlaceId) ?? (settings != null ? settings.defaultRespawnPlace : null);

        if (area == null || respawn == null || mapView == null)
        {
            Debug.LogWarning("복귀 장소를 찾지 못했습니다. BattleSettings의 기본 복귀 장소를 확인하세요.");
            return;
        }

        ClearSelection();
        ResetCameraImmediately();
        mapView.ShowArea(area);

        if (backToRegionButton != null)
        {
            backToRegionButton.SetActive(true);
        }

        OpenInterior(respawn);
        Debug.Log("패배 → " + respawn.placeName + "에서 깨어났습니다.");
    }

    AreaData FindArea(string areaId)
    {
        if (string.IsNullOrEmpty(areaId) || mapView == null || mapView.CurrentRegion == null)
        {
            return null;
        }

        var entry = mapView.CurrentRegion.areas.Find(e => e != null && e.area != null && e.area.areaId == areaId);
        return entry != null ? entry.area : null;
    }

    static PlaceData FindPlace(AreaData area, string placeId)
    {
        if (area == null || string.IsNullOrEmpty(placeId))
        {
            return null;
        }

        var entry = area.places.Find(e => e != null && e.place != null && e.place.locationId == placeId);
        return entry != null ? entry.place : null;
    }

    // [C-4] 지도를 다시 그린다 (매력으로 숨겨진 장소가 드러났을 때). 선택은 풀린다
    public void RefreshMap()
    {
        if (mapView == null) return;

        ClearSelection();

        if (mapView.CurrentArea != null) mapView.ShowArea(mapView.CurrentArea);
        else if (mapView.CurrentRegion != null) mapView.ShowRegion(mapView.CurrentRegion);
    }

    // 장소 내부 → 구역 맵 (ExitButton OnClick)
    public void ExitPlace()
    {
        // 장소 내부 화면 숨기기
        if (placeInteriorPanel != null)
        {
            placeInteriorPanel.SetActive(false);
        }

        // 지도 정보 패널 다시 표시
        if (locationInfoPanel != null &&
            selectedMarker != null)
        {
            locationInfoPanel.SetActive(true);
        }

        Debug.Log("지도 화면으로 돌아왔습니다.");
    }

    // 지도 정보 패널 닫기 및 카메라 초기화 (CloseButton OnClick)
    public void CloseLocationInfo()
    {
        ClearSelection();

        // 카메라 초기 위치와 확대값으로 복귀
        targetCameraPosition = initialCameraPosition;
        targetCameraSize = initialCameraSize;

        Debug.Log("전체 지도 화면으로 돌아갑니다.");
    }

    // [A-4] 구역 맵 → 지역 맵 (BackToRegionButton OnClick)
    public void BackToRegion()
    {
        if (mapView == null || mapView.CurrentArea == null)
        {
            return;
        }

        ClearSelection();
        ResetCameraImmediately();

        mapView.ShowRegion(mapView.CurrentRegion);

        if (backToRegionButton != null)
        {
            backToRegionButton.SetActive(false);
        }

        Debug.Log("지역 맵으로 돌아왔습니다.");
    }

    // 지역 맵 → 구역 맵
    private void EnterArea(AreaData area)
    {
        if (mapView == null)
        {
            Debug.LogWarning("LocationSelector에 MapView가 연결되지 않았습니다.");
            return;
        }

        ClearSelection();
        ResetCameraImmediately();

        mapView.ShowArea(area);

        if (backToRegionButton != null)
        {
            backToRegionButton.SetActive(true);
        }

        Debug.Log(area.areaName + " 구역으로 들어갔습니다.");
    }

    private void ClearSelection()
    {
        if (selectedMarker != null)
        {
            selectedMarker.SetSelected(false);
        }

        selectedMarker = null;

        if (locationInfoPanel != null)
        {
            locationInfoPanel.SetActive(false);
        }
    }

    // 맵이 바뀔 때는 카메라를 바로 처음 위치로 (다른 맵에서 확대된 채 넘어가지 않게)
    private void ResetCameraImmediately()
    {
        targetCameraPosition = initialCameraPosition;
        targetCameraSize = initialCameraSize;

        if (mainCamera != null)
        {
            mainCamera.transform.position = initialCameraPosition;

            if (mainCamera.orthographic)
            {
                mainCamera.orthographicSize = initialCameraSize;
            }
        }
    }
}
