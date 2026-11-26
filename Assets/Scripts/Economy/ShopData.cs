using System.Collections.Generic;
using UnityEngine;

// 상점 1곳 (에셋 1개). 장소 상호작용(Shop)에 연결한다. 편의점·특수 상점·히든 상점 모두 이 형식.
[CreateAssetMenu(fileName = "Shop_New", menuName = "요원/상점 (ShopData)")]
public class ShopData : ScriptableObject
{
    public string shopId = "";
    public string shopName = "[상점 이름 미정]";
    public Sprite icon;

    [Header("점원 (비어 있으면 회색 자리)")]
    public string keeperName = "[점원 이름 미정]";
    public Sprite keeperImage;

    [Header("판매 목록 (순서 = 표시 순서)")]
    public List<ShopEntry> entries = new List<ShopEntry>();

    [Header("탭")]
    [Tooltip("판매 탭 (특수 상점)")]
    public bool canSell = false;
    [Tooltip("강화 탭 (특수 상점)")]
    public bool canEnhance = false;
}

[System.Serializable]
public class ShopEntry
{
    public ItemData item;

    [Tooltip("-1이면 아이템 기본 가격")]
    public int priceOverride = -1;

    [Tooltip("이 품목이 보이거나 살 수 있는 조건 (C-4에서 매력 판정)")]
    public ConditionSet condition = new ConditionSet();

    public int BasePrice => priceOverride >= 0 ? priceOverride : (item != null ? item.price : 0);
}

// 직급 1단계 (EconomySettings.ranks). 승급 판정은 C-4
[System.Serializable]
public class RankData
{
    public string rankId = "";
    public string rankName = "[직급 미정]";
    public int minLevel = 1;
    [Tooltip("레벨 외 추가 조건 (요원: 보스 격파)")]
    public ConditionSet condition = new ConditionSet();
    [Tooltip("병원 전체 치료 비용")]
    public int hospitalCost = 10;
}
