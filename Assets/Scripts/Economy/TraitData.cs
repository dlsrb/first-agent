using System.Collections.Generic;
using UnityEngine;

// [C-4] 무기 특성 데이터

// 무기 특성 효과
public enum TraitEffect
{
    DiceCount,              // 주사위 +값 개
    DiceFaces,              // 주사위 면 +값
    ComboMultiplierPercent, // 조합 배수 +값 %
    CritExtraDie            // 치명타 시 주사위 1개 더 굴려 합에 더함
}

// 특성 1개 (에셋 1개)
[CreateAssetMenu(fileName = "Trait_New", menuName = "요원/성장/무기 특성 (TraitData)")]
public class TraitData : ScriptableObject
{
    public string traitId = "";
    public string traitName = "[특성 이름 미정]";
    [TextArea(2, 4)] public string description = "[특성 설명 미정]";
    public TraitEffect effect = TraitEffect.DiceCount;
    public float value = 1f;
}

