using UnityEngine;
using TMPro;

public class InteractionController : MonoBehaviour
{
    [Header("상호작용 결과 UI")]
    public TMP_Text interactionResultText;

    // 진열대 조사
    public void InvestigateShelf()
    {
        interactionResultText.text =
            "다양한 상품이 진열되어 있다.";
    }

    // 점원과 대화
    public void TalkToClerk()
    {
        interactionResultText.text =
            "점원: 어서 오세요. 무엇을 찾으시나요?";
    }

    // 수상한 사람 조사
    public void InvestigateSuspiciousPerson()
    {
        interactionResultText.text =
            "누군가 당신을 바라보고 있다.";
    }
}