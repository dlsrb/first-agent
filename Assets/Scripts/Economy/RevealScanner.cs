using System.Collections.Generic;

// [C-4] 매력으로 숨겨진 것 찾기: 지역 데이터 안의 매력 조건(장소·상호작용·상점 품목·대화 선택지)을 훑어,
// 이번에 처음 조건을 채운 것을 "드러남" 플래그로 기록하고 개수를 돌려준다. 스탯·재능이 바뀐 뒤 호출 → 1개 이상이면 알림.
public static class RevealScanner
{
    public const string Notice = "[매력으로 숨겨진 것이 드러났다]";

    public static int Scan(GameState state, RegionData region)
    {
        if (state == null || region == null) return 0;

        int found = 0;
        var seenShops = new HashSet<ShopData>();

        foreach (var ae in region.areas)
        {
            if (ae == null || ae.area == null) continue;

            foreach (var pe in ae.area.places)
            {
                if (pe == null || pe.place == null) continue;
                var place = pe.place;
                found += Try(state, "place:" + place.locationId, pe.condition);

                foreach (var it in place.interactions)
                {
                    if (it == null) continue;
                    found += Try(state, "ia:" + place.locationId + ":" + it.interactionId, it.condition);

                    if (it.talk != null)
                    {
                        for (int i = 0; i < it.talk.choices.Count; i++)
                        {
                            found += Try(state, "choice:" + place.locationId + ":" + it.interactionId + ":" + i, it.talk.choices[i].condition);
                        }
                    }

                    if (it.shop != null && seenShops.Add(it.shop))
                    {
                        foreach (var e in it.shop.entries)
                        {
                            if (e != null && e.item != null) found += Try(state, "shop:" + it.shop.shopId + ":" + e.item.itemId, e.condition);
                        }
                    }
                }
            }
        }

        return found;
    }

    static int Try(GameState state, string what, ConditionSet set)
    {
        if (!ConditionChecker.HasCharm(set) || !ConditionChecker.Check(state, set)) return 0;

        string key = StateKeys.Revealed(what);
        if (state.flags.Contains(key)) return 0;

        state.flags.Add(key);
        return 1;
    }
}
