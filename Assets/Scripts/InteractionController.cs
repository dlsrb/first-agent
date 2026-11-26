using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 장소 내부 화면 (PlaceInteriorPanel에 부착)
// [A-3] 상호작용 버튼은 PlaceData.interactions 목록대로 템플릿을 복제해 만든다.
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

    // [A-3 추가]
    [Header("상호작용 버튼")]
    [Tooltip("InteractionArea 안의 꺼진 버튼. 장소 데이터의 상호작용 개수만큼 복제된다")]
    public Button interactionButtonTemplate;

    [Header("이미지 (데이터가 비어 있으면 회색 그대로)")]
    public Image characterImage;
    public Image interiorBackgroundImage;

    // 현재 장소 데이터
    private PlaceData currentPlace;

    // 지금 열려 있는 대화 (선택지를 고를 때 사용)
    private PlaceInteraction currentTalk;

    // 템플릿에서 만든 버튼들
    private readonly List<Button> spawnedButtons = new List<Button>();

    // 데이터에 이미지가 없을 때 되돌릴 기본 모양
    private bool defaultsSaved;
    private Sprite defaultCharacterSprite;
    private Color defaultCharacterColor;
    private Sprite defaultBackgroundSprite;
    private Color defaultBackgroundColor;

    // 장소 데이터 전달받기 (LocationSelector가 진입 시 호출)
    public void SetPlaceData(PlaceData data)
    {
        SaveDefaults();

        currentPlace = data;

        // 새로운 장소에 들어가면 이전 결과 초기화
        if (interactionResultText != null)
        {
            interactionResultText.text = "";
        }

        // 새로운 장소에 들어가면 대화창·선택지 닫기 (진행 상태는 GameStateManager에 유지)
        CloseDialogue();

        ApplyBackground();
        BuildInteractionButtons();
    }

    // [8일차 추가] 선택지 A 선택 (ChoiceAButton OnClick)
    public void ChooseA()
    {
        Choose(0);
    }

    // [8일차 추가] 선택지 B 선택 (ChoiceBButton OnClick)
    public void ChooseB()
    {
        Choose(1);
    }

    // [9일차 추가] 대화창 닫기 (CloseDialogueButton OnClick)
    public void CloseDialogue()
    {
        HideChoices();

        currentTalk = null;

        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(false);
        }
    }

    // ---- 상호작용 버튼 ----

    private void BuildInteractionButtons()
    {
        foreach (Button old in spawnedButtons)
        {
            if (old != null)
            {
                // Destroy는 프레임 끝에 지워지므로 먼저 꺼서 바로 안 보이게
                old.gameObject.SetActive(false);
                Destroy(old.gameObject);
            }
        }

        spawnedButtons.Clear();

        if (interactionButtonTemplate == null)
        {
            Debug.LogWarning("InteractionButtonTemplate이 연결되지 않았습니다.");
            return;
        }

        interactionButtonTemplate.gameObject.SetActive(false);

        if (currentPlace == null)
        {
            return;
        }

        for (int i = 0; i < currentPlace.interactions.Count; i++)
        {
            PlaceInteraction interaction = currentPlace.interactions[i];

            if (interaction == null)
            {
                continue;
            }

            Button button = Instantiate(
                interactionButtonTemplate,
                interactionButtonTemplate.transform.parent
            );

            button.name = "Interaction_" + InteractionIdOf(interaction, i);

            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);

            if (label != null)
            {
                label.text = interaction.label;
            }

            button.onClick.AddListener(() => OnInteraction(interaction));
            button.gameObject.SetActive(true);

            spawnedButtons.Add(button);
        }
    }

    private void OnInteraction(PlaceInteraction interaction)
    {
        if (currentPlace == null)
        {
            Debug.LogWarning("현재 장소 데이터가 연결되지 않았습니다.");
            return;
        }

        switch (interaction.type)
        {
            case InteractionType.Investigate:
                // [8일차 추가] 다른 행동을 하면 선택지 숨기기
                HideChoices();
                DisplayResult(interaction.resultText);

                // [B-1] 괴이 정보를 주는 조사면 정보 플래그 기록
                if (interaction.revealsMonster != null)
                {
                    GameStateManager.Instance.SetFlag(StateKeys.MonsterInfo(interaction.revealsMonster.monsterId));
                }

                break;

            case InteractionType.Talk:
                StartTalk(interaction);
                break;
        }
    }

    // ---- 대화 ----

    private void StartTalk(PlaceInteraction interaction)
    {
        TalkData talk = interaction.talk;

        if (talk == null)
        {
            Debug.LogWarning(interaction.label + "에 대화 데이터가 없습니다.");
            return;
        }

        currentTalk = interaction;

        string chosenKey = StateKeys.TalkChosen(
            LocationIdOf(currentPlace),
            InteractionIdOf(interaction, currentPlace.interactions.IndexOf(interaction))
        );

        // [10일차] 이미 선택지를 골랐으면 재방문 대사만 표시
        if (GameStateManager.Instance.HasFlag(chosenKey))
        {
            OpenDialogue(talk.npc, talk.repeatLine);
            HideChoices();
        }
        else
        {
            OpenDialogue(talk.npc, talk.firstLine);
            ShowChoices(talk.choices);
        }
    }

    private void Choose(int index)
    {
        if (currentPlace == null || currentTalk == null)
        {
            Debug.LogWarning("열려 있는 대화가 없습니다.");
            return;
        }

        List<TalkChoice> choices = currentTalk.talk.choices;

        if (index >= choices.Count)
        {
            Debug.LogWarning("선택지 " + index + "번 데이터가 없습니다.");
            return;
        }

        string locationId = LocationIdOf(currentPlace);
        string interactionId = InteractionIdOf(currentTalk, currentPlace.interactions.IndexOf(currentTalk));

        // 선택했음 + 어떤 선택지를 골랐는지 기록
        GameStateManager.Instance.SetFlag(StateKeys.TalkChosen(locationId, interactionId));
        GameStateManager.Instance.SetFlag(StateKeys.TalkChoice(locationId, interactionId, index));

        string result = choices[index].result;

        // [9일차] 대화창을 닫고 결과는 결과 텍스트에 표시
        CloseDialogue();
        DisplayResult(result);
    }

    // [9일차 추가] 대화창 열기
    private void OpenDialogue(NpcData npc, string line)
    {
        if (dialoguePanel == null)
        {
            Debug.LogWarning("DialoguePanel이 연결되지 않았습니다.");
            return;
        }

        if (npcNameText != null)
        {
            npcNameText.text = npc != null ? npc.npcName : "";
        }

        if (dialogueText != null)
        {
            dialogueText.text = line;
        }

        if (characterImage != null)
        {
            bool hasPortrait = npc != null && npc.portrait != null;
            characterImage.sprite = hasPortrait ? npc.portrait : defaultCharacterSprite;
            characterImage.color = hasPortrait ? Color.white : defaultCharacterColor;
        }

        dialoguePanel.SetActive(true);
    }

    // [8일차 추가] 선택지 보여주기 (현재 UI는 최대 2개)
    private void ShowChoices(List<TalkChoice> choices)
    {
        if (choicePanel == null)
        {
            Debug.LogWarning("ChoicePanel이 연결되지 않았습니다.");
            return;
        }

        if (choices == null || choices.Count == 0)
        {
            HideChoices();
            return;
        }

        if (choices.Count > 2)
        {
            Debug.LogWarning("선택지가 " + choices.Count + "개입니다. 현재 UI는 2개까지만 표시합니다.");
        }

        SetChoiceButton(choiceAButtonText, choices.Count > 0 ? choices[0] : null);
        SetChoiceButton(choiceBButtonText, choices.Count > 1 ? choices[1] : null);

        choicePanel.SetActive(true);
    }

    private void SetChoiceButton(TMP_Text buttonText, TalkChoice choice)
    {
        if (buttonText == null)
        {
            return;
        }

        Button button = buttonText.GetComponentInParent<Button>(true);

        if (button != null)
        {
            button.gameObject.SetActive(choice != null);
        }

        if (choice != null)
        {
            buttonText.text = choice.text;
        }
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

    // ---- 이미지 ----

    private void SaveDefaults()
    {
        if (defaultsSaved)
        {
            return;
        }

        if (characterImage != null)
        {
            defaultCharacterSprite = characterImage.sprite;
            defaultCharacterColor = characterImage.color;
        }

        if (interiorBackgroundImage != null)
        {
            defaultBackgroundSprite = interiorBackgroundImage.sprite;
            defaultBackgroundColor = interiorBackgroundImage.color;
        }

        defaultsSaved = true;
    }

    private void ApplyBackground()
    {
        if (interiorBackgroundImage == null)
        {
            return;
        }

        bool hasBackground = currentPlace != null && currentPlace.interiorBackground != null;
        interiorBackgroundImage.sprite = hasBackground ? currentPlace.interiorBackground : defaultBackgroundSprite;
        interiorBackgroundImage.color = hasBackground ? Color.white : defaultBackgroundColor;
    }

    // ---- ID (비어 있으면 대체값 + 경고) ----

    private static string LocationIdOf(PlaceData place)
    {
        if (!StateKeys.IsValidId(place.locationId))
        {
            Debug.LogWarning(place.name + "의 locationId가 올바르지 않아 에셋 이름을 대신 사용합니다.");
            return place.name;
        }

        return place.locationId;
    }

    private static string InteractionIdOf(PlaceInteraction interaction, int index)
    {
        if (!StateKeys.IsValidId(interaction.interactionId))
        {
            Debug.LogWarning("상호작용 '" + interaction.label + "'의 interactionId가 올바르지 않아 순번을 대신 사용합니다.");
            return "i" + index;
        }

        return interaction.interactionId;
    }
}
