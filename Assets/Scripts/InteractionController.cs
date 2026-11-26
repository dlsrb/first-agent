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

    // [9일차 추가] 대화창 UI
    [Header("대화창 UI")]
    public GameObject dialoguePanel;
    public TMP_Text npcNameText;
    public TMP_Text dialogueText;

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

        // [9일차 추가] 새로운 장소에 들어가면 대화창 닫기
        CloseDialogue();
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

        // [10일차 변경] 이미 선택지를 골랐으면 재방문 대사만 표시
        // [A단계] 상태는 GameStateManager에서 조회
        if (GameStateManager.Instance.HasChosenClerkChoice(CurrentLocationId()))
        {
            OpenDialogue(currentLocationData.clerkRepeatDialogue);
            HideChoices();
        }
        else
        {
            // [9일차 변경] 결과 텍스트 대신 대화창에 이름과 대사 표시
            OpenDialogue(currentLocationData.clerkResult);

            // [8일차 추가] 점원 대사 후 선택지 보여주기
            ShowChoices();
        }
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

        // [10일차 추가] 이 장소에서 선택했다는 상태 기록
        GameStateManager.Instance.MarkClerkChoice(CurrentLocationId());

        // [9일차 변경] 대화창을 닫고 결과는 결과 텍스트에 표시
        CloseDialogue();
        DisplayResult(currentLocationData.choiceAResult);
    }

    // [8일차 추가] 선택지 B 선택
    public void ChooseB()
    {
        if (currentLocationData == null)
        {
            Debug.LogWarning("현재 장소 데이터가 연결되지 않았습니다.");
            return;
        }

        // [10일차 추가] 이 장소에서 선택했다는 상태 기록
        GameStateManager.Instance.MarkClerkChoice(CurrentLocationId());

        // [9일차 변경] 대화창을 닫고 결과는 결과 텍스트에 표시
        CloseDialogue();
        DisplayResult(currentLocationData.choiceBResult);
    }

    // [A단계] 상태 키로 쓸 장소 ID. 비어 있으면 장소 이름으로 대신하고 경고
    private string CurrentLocationId()
    {
        if (string.IsNullOrEmpty(currentLocationData.locationId))
        {
            Debug.LogWarning(currentLocationData.locationName + "의 locationId가 비어 있어 장소 이름을 대신 사용합니다.");
            return currentLocationData.locationName;
        }

        return currentLocationData.locationId;
    }

    // [9일차 추가] 대화창 열기
    // [10일차 변경] 보여줄 대사를 받아서 표시
    private void OpenDialogue(string dialogue)
    {
        if (dialoguePanel == null)
        {
            Debug.LogWarning("DialoguePanel이 연결되지 않았습니다.");
            return;
        }

        if (npcNameText != null)
        {
            npcNameText.text = currentLocationData.npcName;
        }

        if (dialogueText != null)
        {
            dialogueText.text = dialogue;
        }

        dialoguePanel.SetActive(true);
    }

    // [9일차 추가] 대화창 닫기 (닫기 버튼에 연결)
    public void CloseDialogue()
    {
        HideChoices();

        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(false);
        }
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