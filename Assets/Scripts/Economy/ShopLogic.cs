// 상점·병원·강화 규칙 (순수 C#). 화면(ShopController·HospitalController)과 테스트가 같이 쓴다.
// 결과 문구는 화면에 그대로 표시한다.
public class ShopResult
{
    public bool ok;
    public string message = "";
    public int equipmentUid;   // 장비를 샀으면 새 uid

    public static ShopResult Fail(string msg) { return new ShopResult { ok = false, message = msg }; }
    public static ShopResult Ok(string msg) { return new ShopResult { ok = true, message = msg }; }
}

public static class ShopLogic
{
    // ---------- 가격 ----------

    // [C-4] 매력 할인 (PlayerState.shopDiscount, 매력 1부터 포인트당 1%, 최대 20%)
    public static int BuyPrice(ShopEntry entry, PlayerState p)
    {
        if (entry == null) return 0;
        float d = p != null ? p.shopDiscount : 0f;
        return (int)System.Math.Round(entry.BasePrice * (1f - d), System.MidpointRounding.AwayFromZero);
    }

    // [C-4] 강화 골드 (재능 "손재주" 할인)
    public static int EnhanceGold(EnhanceStep step, PlayerState p)
    {
        float d = p != null ? p.enhanceDiscount : 0f;
        return (int)System.Math.Round(step.gold * (1f - d), System.MidpointRounding.AwayFromZero);
    }

    // ---------- 구매 ----------

    public static ShopResult Buy(GameState state, ShopEntry entry)
    {
        var p = state.player;

        if (entry == null || entry.item == null)
        {
            return ShopResult.Fail("[살 수 없는 품목]");
        }

        if (!ConditionChecker.Check(state, entry.condition))
        {
            return ShopResult.Fail(ConditionChecker.LockedText(entry.condition));
        }

        int price = BuyPrice(entry, p);

        if (p.gold < price)
        {
            return ShopResult.Fail("골드가 부족하다. (" + price + "G 필요, 보유 " + p.gold + "G)");
        }

        p.gold -= price;
        var r = ShopResult.Ok(Josa.Eul(entry.item.itemName) + " 샀다. (-" + price + "G)");

        if (entry.item.IsEquipment)
        {
            r.equipmentUid = p.AddEquipment(entry.item.itemId);
        }
        else
        {
            p.AddItem(entry.item.itemId, 1);
        }

        return r;
    }

    // ---------- 판매 ----------

    public static ShopResult SellEquipment(PlayerState p, int uid, EconomySettings eco, GameDatabase db)
    {
        var owned = p.FindEquipment(uid);
        var item = owned != null ? db.FindItem(owned.itemId) : null;

        if (item == null)
        {
            return ShopResult.Fail("[없는 장비]");
        }

        if (p.IsEquipped(uid))
        {
            return ShopResult.Fail("장착 중인 장비는 팔 수 없다.");
        }

        int price = eco.SellPrice(item);
        p.RemoveEquipment(uid);
        p.gold += price;
        return ShopResult.Ok(Josa.Eul(NameWithEnhance(item, owned.enhance)) + " 팔았다. (+" + price + "G)");
    }

    public static ShopResult SellItem(PlayerState p, string itemId, EconomySettings eco, GameDatabase db)
    {
        var item = db.FindItem(itemId);

        if (item == null || !p.RemoveItem(itemId, 1))
        {
            return ShopResult.Fail("[팔 물건이 없다]");
        }

        int price = eco.SellPrice(item);
        p.gold += price;
        return ShopResult.Ok(Josa.Eul(item.itemName) + " 팔았다. (+" + price + "G)");
    }

    // ---------- 강화 ----------

    public static ShopResult Enhance(PlayerState p, int uid, EconomySettings eco, BattleSettings settings, GameDatabase db)
    {
        var owned = p.FindEquipment(uid);
        var item = owned != null ? db.FindItem(owned.itemId) : null;

        if (item == null || !item.IsEquipment)
        {
            return ShopResult.Fail("[강화할 장비가 없다]");
        }

        var step = eco.NextEnhance(owned.enhance);

        if (step == null)
        {
            return ShopResult.Fail("이미 최대 강화(+" + eco.MaxEnhance + ")다.");
        }

        string matId = eco.materialItem != null ? eco.materialItem.itemId : "";
        int have = p.CountOf(matId);

        int gold = EnhanceGold(step, p);

        if (p.gold < gold || have < step.materials)
        {
            return ShopResult.Fail("재료가 부족하다. (" + gold + "G·강화석 " + step.materials + "개 필요, 보유 " + p.gold + "G·" + have + "개)");
        }

        p.gold -= gold;
        if (step.materials > 0) p.RemoveItem(matId, step.materials);
        owned.enhance++;
        PlayerStats.Recalculate(p, settings, db);
        return ShopResult.Ok(Josa.Ga(NameWithEnhance(item, owned.enhance - 1)) + " " + Josa.Euro("+" + owned.enhance) + " 강화되었다. (-" + gold + "G·강화석 " + step.materials + "개)");
    }

    // ---------- [C-3] 소비 아이템 사용 (홈 창 인벤토리, 전투 밖) ----------

    public static ShopResult UseItem(PlayerState p, ItemData item)
    {
        if (item == null || item.type != ItemType.Consumable || p.CountOf(item.itemId) <= 0)
        {
            return ShopResult.Fail("[쓸 수 없는 아이템]");
        }

        bool hpFull = item.healHp <= 0 || p.currentHp >= p.maxHp;
        bool energyFull = item.healEnergy <= 0 || p.currentEnergy >= p.maxEnergy;
        bool hasGain = item.statGains != null && item.statGains.Exists(g => g != null && g.amount != 0);

        if (hpFull && energyFull && !hasGain)
        {
            return ShopResult.Fail("지금은 쓸 필요가 없다.");
        }

        p.RemoveItem(item.itemId, 1);
        int hpBefore = p.currentHp, enBefore = p.currentEnergy;
        p.currentHp = System.Math.Min(p.maxHp, p.currentHp + item.healHp);
        p.currentEnergy = System.Math.Min(p.maxEnergy, p.currentEnergy + item.healEnergy);

        string msg = Josa.Eul(item.itemName) + " 썼다.";
        string gains = GrowthLogic.ApplyStatGains(p, item.statGains);
        if (gains != "") msg += " " + gains;
        if (p.currentHp > hpBefore) msg += " 체력 +" + (p.currentHp - hpBefore);
        if (p.currentEnergy > enBefore) msg += " 기력 +" + (p.currentEnergy - enBefore);
        return ShopResult.Ok(msg);
    }

    // ---------- 병원 ----------

    public static ShopResult Heal(PlayerState p, EconomySettings eco)
    {
        int cost = eco.HospitalCost(p);

        if (p.currentHp >= p.maxHp && p.currentEnergy >= p.maxEnergy)
        {
            return ShopResult.Fail("치료할 곳이 없다.");
        }

        if (p.gold < cost)
        {
            return ShopResult.Fail("골드가 부족하다. (" + cost + "G 필요, 보유 " + p.gold + "G)");
        }

        p.gold -= cost;
        p.currentHp = p.maxHp;
        p.currentEnergy = p.maxEnergy;
        return ShopResult.Ok("체력과 기력이 모두 회복되었다. (-" + cost + "G)");
    }

    // ---------- 표시 ----------

    public static string NameWithEnhance(ItemData item, int enhance)
    {
        return enhance > 0 ? item.itemName + " +" + enhance : item.itemName;
    }

    // 상품 효과 한 줄
    public static string EffectText(ItemData item, int enhance = 0)
    {
        if (item == null) return "";

        switch (item.type)
        {
            case ItemType.Weapon:
                string w = DisplayNames.Grade(item.grade) + " 무기 · 공격 +" + (item.attackBonus + item.attackPerEnhance * enhance);
                if (item.critBonus > 0) w += " · 치명 +" + UnityEngine.Mathf.RoundToInt(item.critBonus * 100) + "%";
                w += " · 스킬 " + item.skills.Count + "개";
                return w;
            case ItemType.Armor:
                return DisplayNames.Grade(item.grade) + " 방어구 · 방어 +" + (item.defenseBonus + item.defensePerEnhance * enhance);
            case ItemType.Material:
                return "강화 재료";
            default:
                string c = "";
                if (item.healHp > 0) c += "체력 +" + item.healHp;
                if (item.healEnergy > 0) c += (c.Length > 0 ? " · " : "") + "기력 +" + item.healEnergy;
                if (c == "" && item.statGains != null && item.statGains.Count > 0) c = "[효과 미정]";   // 히든 스탯 아이템은 효과를 밝히지 않는다
                return c;
        }
    }
}
