using UnityEngine;
using TMPro;

public class InteractionController : MonoBehaviour
{
    [Header("상호작용 결과 UI")]
    public TMP_Text interactionResultText;

    // [8일차 추가] 점원 대화 선택지 UI
    [Header("선택지 UI")]
    public GameObject choicePanel;
    public TMP_Text choiceAButtonText;
    public TMP_Text choiceBButtonText;

    // 현재 장소 데이터
    private LocationData currentLocationData;

    // 장소 데이터 전달받기
    public void SetLocationData(LocationData data)
    {
        currentLocationData = data;

        // 새로운 장소에 들어가면 이전 결과 초기화
        if (interactionResultText != null)
        {
            interactionResultText.text = "";
        }

        // [8일차 추가] 새로운 장소에 들어가면 이전 선택지 숨기기
        HideChoices();
    }

    // 진열대 조사
    public void InvestigateShelf()
    {
        if (currentLocationData == null)
        {
            Debug.LogWarning("현재 장소 데이터가 연결되지 않았습니다.");
            return;
        }

        // [8일차 추가] 다른 행동을 하면 선택지 숨기기
        HideChoices();

        DisplayResult(currentLocationData.shelfResult);
    }

    // 점원과 대화
    public void TalkToClerk()
    {
        if (currentLocationData == null)
        {
            Debug.LogWarning("현재 장소 데이터가 연결되지 않았습니다.");
            return;
        }

        DisplayResult(currentLocationData.clerkResult);

        // [8일차 추가] 점원 대사 후 선택지 보여주기
        ShowChoices();
    }

    // 수상한 사람 조사
    public void InvestigateSuspiciousPerson()
    {
        if (currentLocationData == null)
        {
            Debug.LogWarning("현재 장소 데이터가 연결되지 않았습니다.");
            return;
        }

        // [8일차 추가] 다른 행동을 하면 선택지 숨기기
        HideChoices();

        DisplayResult(currentLocationData.suspiciousPersonResult);
    }

    // [8일차 추가] 선택지 A 선택
    public void ChooseA()
    {
        if (currentLocationData == null)
        {
            Debug.LogWarning("현재 장소 데이터가 연결되지 않았습니다.");
            return;
        }

        DisplayResult(currentLocationData.choiceAResult);
        HideChoices();
    }

    // [8일차 추가] 선택지 B 선택
    public void ChooseB()
    {
        if (currentLocationData == null)
        {
            Debug.LogWarning("현재 장소 데이터가 연결되지 않았습니다.");
            return;
        }

        DisplayResult(currentLocationData.choiceBResult);
        HideChoices();
    }

    // [8일차 추가] 선택지 보여주기
    private void ShowChoices()
    {
        if (choicePanel == null)
        {
            Debug.LogWarning("ChoicePanel이 연결되지 않았습니다.");
            return;
        }

        // 현재 장소의 선택지 문구를 버튼에 넣기
        if (choiceAButtonText != null)
        {
            choiceAButtonText.text = currentLocationData.choiceAText;
        }

        if (choiceBButtonText != null)
        {
            choiceBButtonText.text = currentLocationData.choiceBText;
        }

        choicePanel.SetActive(true);
    }

    // [8일차 추가] 선택지 숨기기
    private void HideChoices()
    {
        if (choicePanel != null)
        {
            choicePanel.SetActive(false);
        }
    }

    // 결과 출력
    private void DisplayResult(string result)
    {
        if (interactionResultText == null)
        {
            Debug.LogWarning("InteractionResultText가 연결되지 않았습니다.");
            return;
        }

        interactionResultText.text = result;
    }
}