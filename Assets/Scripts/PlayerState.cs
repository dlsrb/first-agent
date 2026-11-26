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

    // ---- 성장 (C-1은 경험치 누적만, 레벨 업은 C-4) ----
    public int level = 1;
    public int exp = 0;
    public string rankId = "trainee";   // C-2: 병원비. 승급은 C-4

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
            diceFaces = b.diceFaces
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

    public bool IsEquipped(int uid)
    {
        return uid != 0 && (equippedWeaponUid == uid || equippedArmorUid == uid);
    }

    // 시뮬레이션·테스트용 복사본
    public PlayerState Clone()
    {
        var p = (PlayerState)MemberwiseClone();
        p.skillIds = new List<string>(skillIds);
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
