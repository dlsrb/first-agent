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

    void Start()
    {
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

            Collider2D hit = Physics2D.OverlapPoint(clickPosition);

            if (hit != null && originalColors.ContainsKey(hit.gameObject))
            {
                SelectLocation(hit.gameObject);
            }
        }
    }

    void SelectLocation(GameObject location)
    {
        // 이전에 선택한 장소의 색상을 복구한다.
        if (selectedLocation != null)
        {
            SpriteRenderer previousRenderer =
                selectedLocation.GetComponent<SpriteRenderer>();

            if (previousRenderer != null)
            {
                previousRenderer.color = originalColors[selectedLocation];
            }
        }

        // 새 장소를 선택하고 파란색으로 표시한다.
        selectedLocation = location;

        SpriteRenderer currentRenderer =
            selectedLocation.GetComponent<SpriteRenderer>();

        if (currentRenderer != null)
        {
            currentRenderer.color = Color.blue;
        }

        // 선택한 장소의 데이터를 가져온다.
        LocationData data = location.GetComponent<LocationData>();

        if (data != null)
        {
            // 장소 정보 UI에 데이터를 표시한다.
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
                locationTypeText.text = data.locationType.ToString();
            }

            if (locationDescriptionText != null)
            {
                locationDescriptionText.text = data.description;
            }

            Debug.Log("장소 이름: " + data.locationName);
            Debug.Log("장소 유형: " + data.locationType);
            Debug.Log("장소 설명: " + data.description);
        }
        else
        {
            Debug.LogWarning(
                location.name + "에 LocationData가 없습니다.");
        }
    }


}
