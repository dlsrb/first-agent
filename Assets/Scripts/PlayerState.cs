using System.Collections.Generic;

// 주인공 수치 (GameState.player). JsonUtility로 그대로 저장되는 형태만 사용 (E단계 세이브): List·기본형·[Serializable] 클래스, Dictionary 금지.
// [C-1] 저장하는 원본(장비·경험치 등)과 계산된 수치(maxHp·attack 등)를 나눈다.
//       계산된 수치는 PlayerStats.Recalculate가 기본값 + 장비 + 강화로 다시 채운다 (C-4에서 스탯·재능·숙련도 추가).
[System.Serializable]
public class PlayerState
{
    public bool initialized = false;

    // ---- 소비 자원 ----
    public int currentHp;
    public int currentEnergy;   // 기력: 전투 후에도 쓴 만큼 유지, 전투 중 자동 회복 없음
    public int gold;

    // ---- 계산된 수치 (PlayerStats.Recalculate가 채움, 직접 고치지 않음. 테스트는 직접 넣어도 됨) ----
    public int maxHp;
    public int maxEnergy;
    public int attack;
    public int defense;
    public float critChance;
    public int agility;
    public int diceCount;
    public int diceFaces;
    public List<string> skillIds = new List<string>();   // 장착 무기의 스킬
    public StatBlock totalStats = new StatBlock();   // [C-4] 찍은 스탯 + 재능 (조건 판정·표시)
    public float debuffResist;      // 디버프 무시 확률 (정신력·재능)
    public float infoChance;        // 전투 시작 시 괴이 정보 획득 확률 (지능)
    public float shopDiscount;      // 상점 할인 (매력)
    public float expBonus;          // 경험치 +% (재능)
    public float dropBonus;         // 드롭 확률 +% (재능)
    public float enhanceDiscount;   // 강화 골드 −% (재능)
    public float comboBonus;        // 조합 배수 +% (특성)
    public bool critExtraDie;       // 치명타 시 주사위 1개 더 (특성)

    // ---- 성장 (C-1은 경험치 누적만, 레벨 업은 C-4) ----
    public int level = 1;
    public int exp = 0;
    public string rankId = "trainee";   // C-2: 병원비, C-4: 승급
    public int statPoints = 0;           // [C-4] 레벨 업으로 받은 남은 포인트
    public StatBlock stats = new StatBlock();        // [C-4] 찍은 스탯 (레벨 포인트·훈련)
    public StatBlock trainCount = new StatBlock();   // [C-5] 스탯별 훈련 성공 횟수 (누적 한도·비용·구간)
    public List<TrainDay> trainDays = new List<TrainDay>();   // [11/28] 스탯별 마지막 훈련 날짜·그날 성공 수 (하루 한도)
    public List<string> talentIds = new List<string>();
    public int pendingTalentPicks = 0;   // 아직 안 고른 재능 수 (새 게임 1, 승급마다 +1)
    public List<MasteryState> masteries = new List<MasteryState>();

    // ---- 복귀 지점 (비어 있으면 BattleSettings 기본값) ----
    public string respawnAreaId = "";
    public string respawnPlaceId = "";

    // ---- 아이템 ----
    public List<ItemStack> items = new List<ItemStack>();              // 소비·재료 (개수)
    public List<OwnedEquipment> equipment = new List<OwnedEquipment>(); // 무기·방어구 (한 개씩, 강화 단계 포함)
    public int nextEquipUid = 1;
    public int equippedWeaponUid = 0;   // 0 = 없음
    public int equippedArmorUid = 0;

    // 새 게임: 시작 지급품을 넣는다. 계산된 수치는 호출한 쪽에서 PlayerStats.Recalculate (GameStateManager.EnsurePlayer)
    public static PlayerState CreateFrom(PlayerBase b)
    {
        var p = new PlayerState
        {
            initialized = true,
            maxHp = b.maxHp,
            currentHp = b.maxHp,
            maxEnergy = b.maxEnergy,
            currentEnergy = b.maxEnergy,
            attack = b.attack,
            defense = b.defense,
            critChance = b.critChance,
            agility = b.agility,
            diceCount = b.diceCount,
            diceFaces = b.diceFaces,
            pendingTalentPicks = 1   // 시작 재능 (C-6 시작 흐름에서 고름)
        };

        foreach (var skill in b.startSkills)
        {
            if (skill != null)
            {
                p.skillIds.Add(skill.skillId);
            }
        }

        foreach (var start in b.startItems)
        {
            if (start != null && start.item != null && start.count > 0)
            {
                p.AddItem(start.item.itemId, start.count);
            }
        }

        if (b.startWeapon != null)
        {
            p.equippedWeaponUid = p.AddEquipment(b.startWeapon.itemId);
        }

        if (b.startArmor != null)
        {
            p.equippedArmorUid = p.AddEquipment(b.startArmor.itemId);
        }

        return p;
    }

    // ---- 소비·재료 ----

    public int CountOf(string itemId)
    {
        var stack = items.Find(s => s.itemId == itemId);
        return stack != null ? stack.count : 0;
    }

    public void AddItem(string itemId, int count)
    {
        var stack = items.Find(s => s.itemId == itemId);

        if (stack == null)
        {
            items.Add(new ItemStack { itemId = itemId, count = count });
        }
        else
        {
            stack.count += count;
        }
    }

    public bool RemoveItem(string itemId, int count)
    {
        var stack = items.Find(s => s.itemId == itemId);

        if (stack == null || stack.count < count)
        {
            return false;
        }

        stack.count -= count;

        if (stack.count == 0)
        {
            items.Remove(stack);
        }

        return true;
    }

    // ---- 장비 ----

    // 장비 1개 추가 (장착하지 않음). 새 uid를 돌려준다
    public int AddEquipment(string itemId, int enhance = 0)
    {
        int uid = nextEquipUid++;
        equipment.Add(new OwnedEquipment { uid = uid, itemId = itemId, enhance = enhance });
        return uid;
    }

    public OwnedEquipment FindEquipment(int uid)
    {
        return uid == 0 ? null : equipment.Find(e => e.uid == uid);
    }

    // 장착 중이면 해제하고 목록에서 뺀다
    public bool RemoveEquipment(int uid)
    {
        var e = FindEquipment(uid);

        if (e == null)
        {
            return false;
        }

        if (equippedWeaponUid == uid) equippedWeaponUid = 0;
        if (equippedArmorUid == uid) equippedArmorUid = 0;
        equipment.Remove(e);
        return true;
    }

    // [C-4] 무기 유형 숙련도 (없으면 만든다)
    public MasteryState MasteryOf(WeaponType type)
    {
        string key = type.ToString();
        var m = masteries.Find(x => x.weaponType == key);

        if (m == null)
        {
            m = new MasteryState { weaponType = key };
            masteries.Add(m);
        }

        return m;
    }

    public bool IsEquipped(int uid)
    {
        return uid != 0 && (equippedWeaponUid == uid || equippedArmorUid == uid);
    }

    // 시뮬레이션·테스트용 복사본
    public PlayerState Clone()
    {
        var p = (PlayerState)MemberwiseClone();
        p.skillIds = new List<string>(skillIds);
        p.stats = stats.Copy();
        p.trainCount = trainCount.Copy();
        p.totalStats = totalStats.Copy();
        p.talentIds = new List<string>(talentIds);
        p.trainDays = new List<TrainDay>();
        foreach (var d in trainDays) p.trainDays.Add(new TrainDay { stat = d.stat, date = d.date, count = d.count });
        p.masteries = new List<MasteryState>();
        foreach (var m in masteries) p.masteries.Add(m.Copy());
        p.items = new List<ItemStack>();
        p.equipment = new List<OwnedEquipment>();

        foreach (var s in items)
        {
            p.items.Add(new ItemStack { itemId = s.itemId, count = s.count });
        }

        foreach (var e in equipment)
        {
            p.equipment.Add(new OwnedEquipment { uid = e.uid, itemId = e.itemId, enhance = e.enhance });
        }

        return p;
    }
}

[System.Serializable]
public class ItemStack
{
    public string itemId = "";
    public int count = 0;
}

// 가진 장비 1개
[System.Serializable]
public class OwnedEquipment
{
    public int uid;
    public string itemId = "";
    public int enhance = 0;   // 강화 단계 0~5
}
