using UnityEngine;
using TMPro;

public class InteractionController : MonoBehaviour
{
    [Header("상호작용 결과 UI")]
    public TMP_Text interactionResultText;

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
    }

    // 진열대 조사
    public void InvestigateShelf()
    {
        if (currentLocationData == null)
        {
            Debug.LogWarning("현재 장소 데이터가 연결되지 않았습니다.");
            return;
        }

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
    }

    // 수상한 사람 조사
    public void InvestigateSuspiciousPerson()
    {
        if (currentLocationData == null)
        {
            Debug.LogWarning("현재 장소 데이터가 연결되지 않았습니다.");
            return;
        }

        DisplayResult(currentLocationData.suspiciousPersonResult);
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