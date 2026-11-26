using System.Collections.Generic;

// 주인공 수치 (GameState.player). JsonUtility로 그대로 저장되는 형태만 사용 (E단계 세이브).
// 장비 보너스는 저장하지 않고 계산할 때 ItemData에서 더한다 (C단계).
[System.Serializable]
public class PlayerState
{
    public bool initialized = false;

    public int currentHp;
    public int maxHp;

    // 기력/마나: 소비 자원. 전투 후에도 쓴 만큼 유지, 전투 중 자동 회복 없음
    public int currentEnergy;
    public int maxEnergy;

    public int attack;
    public int defense;
    public float critChance;
    public int agility;

    public int diceCount;
    public int diceFaces;

    public int gold;

    // 패배 시 복귀 장소 (비어 있으면 BattleSettings 기본값)
    public string respawnAreaId = "";
    public string respawnPlaceId = "";

    // C단계 장비
    public string weaponId = "";
    public string armorId = "";

    public List<string> skillIds = new List<string>();
    public List<ItemStack> items = new List<ItemStack>();

    // 새 게임: 시작 수치로 채운다
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

        return p;
    }

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

    // 시뮬레이션·테스트용 복사본
    public PlayerState Clone()
    {
        var p = (PlayerState)MemberwiseClone();
        p.skillIds = new List<string>(skillIds);
        p.items = new List<ItemStack>();

        foreach (var s in items)
        {
            p.items.Add(new ItemStack { itemId = s.itemId, count = s.count });
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
