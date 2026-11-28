using System;
using System.Collections.Generic;

// 전투 규칙 계산 (docs/전투규칙.md). MonoBehaviour가 아닌 순수 C# — 게임 화면과 시뮬레이터가 같은 엔진을 쓴다.
// 난수는 생성자에서 받는다 (테스트·시뮬레이션은 고정 시드).
//
// 한 턴 흐름:
//   BeginTurn(): 괴이가 이번 행동을 고르고 예고 (Reflect면 바로 반사 자세)
//   주인공 행동 1개: [RollDice → (스킬) → ConfirmAttack] / Defend / Special / Flee
//   → 괴이 행동 실행 → 판정 → EndTurn(): 상태 효과 처리 → 다음 BeginTurn()
public class BattleEngine
{
    public readonly BattleSettings settings;
    public readonly GameDatabase database;
    public readonly PlayerState player;
    public readonly MonsterData monster;

    readonly Random rng;

    public bool KnowsMonster { get; private set; }
    public bool CanFlee { get; private set; }

    public int MonsterHp { get; private set; }
    public int MonsterMaxHp { get; private set; }

    // -1 = 기본 행동(1페이즈), 0 이상 = monster.phases[index] 적용 중
    public int PhaseIndex { get; private set; } = -1;
    float phaseAttackMultiplier = 1f;
    float phaseDefenseMultiplier = 1f;

    public readonly List<ActiveEffect> playerEffects = new List<ActiveEffect>();
    public readonly List<ActiveEffect> monsterEffects = new List<ActiveEffect>();

    public MonsterAction Intent { get; private set; }
    MonsterAction lastAction;
    public bool MonsterReflecting { get; private set; }

    public int Turn { get; private set; }
    public BattleOutcome Outcome { get; private set; } = BattleOutcome.None;

    // 페이즈 전환 시 화면이 보여 줄 만화·문구 (화면이 보여 준 뒤 ClearPendingPhase)
    public ComicData PendingComic { get; private set; }
    public string PendingPhaseMessage { get; private set; }

    // 화면·디버그용 기록
    public readonly List<string> log = new List<string>();

    // 이번 턴 상태
    SpecialAction usedSpecial = SpecialAction.None;
    bool defendSucceeded;
    bool fleeFailed;
    bool tauntUsedThisTurn;

    // 공격 진행 중 (굴린 뒤 확정 전)
    public DiceRoll PendingRoll { get; private set; }
    bool rerollUsed;
    int lockedDieValue;  // 0 = 고정 없음

    // 통계 (시뮬레이션 보고용)
    public int attackCount, comboCount, critCount, damageDealt, comboBonusDamage, damageTaken, energyUsed, skillUses;

    public BattleEngine(BattleSettings settings, GameDatabase database, PlayerState player, MonsterData monster,
                        bool knowsMonster, bool allowFlee, Random rng)
    {
        this.settings = settings;
        this.database = database;
        this.player = player;
        this.monster = monster;
        this.rng = rng;

        KnowsMonster = knowsMonster;
        CanFlee = allowFlee && monster.canFlee;

        MonsterMaxHp = monster.maxHp;
        MonsterHp = monster.maxHp;
    }

    // 전투 시작. 선공 괴이는 예고 없이 첫 행동을 바로 실행
    public void Start()
    {
        if (monster.firstStrike)
        {
            MonsterAction first = ChooseAction();
            Log(monster.monsterName + "의 선공!");
            ExecuteMonsterAction(first, false);
            lastAction = first;

            if (CheckEnd())
            {
                return;
            }
        }

        BeginTurn();
    }

    // ---------- 계산된 수치 ----------

    public int MonsterAttack => Round(monster.attack * phaseAttackMultiplier * (1f + SumAttackPercent(monsterEffects) / 100f));

    public int MonsterDefense => Math.Max(0, Round(monster.defense * phaseDefenseMultiplier * (1f + SumDefensePercent(monsterEffects) / 100f)));

    // [C-1] 장비·강화는 PlayerStats.Recalculate가 player.attack·defense에 이미 더해 둔다
    public int PlayerAttackStat => player.attack;

    public int PlayerDefenseStat => Math.Max(0, Round(player.defense * (1f + SumDefensePercent(playerEffects) / 100f)));

    // 사기 저하 등: 주인공 공격 피해 전체에 적용 (설계안 임시 9번)
    float PlayerDamageFactor => Math.Max(0f, 1f + SumAttackPercent(playerEffects) / 100f);

    public float TouchBonusMax
    {
        get
        {
            float atk = MonsterAttack;

            if (atk <= settings.touchBonusAttackLow)
            {
                return settings.touchBonusMax;
            }

            if (atk >= settings.touchBonusAttackHigh)
            {
                return settings.touchBonusMin;
            }

            float t = (atk - settings.touchBonusAttackLow) / (settings.touchBonusAttackHigh - settings.touchBonusAttackLow);
            return settings.touchBonusMax + (settings.touchBonusMin - settings.touchBonusMax) * t;
        }
    }

    public float DefenseChance(float touchBonus)
    {
        float bonus = Math.Max(0f, Math.Min(touchBonus, TouchBonusMax));
        float chance = settings.defenseChanceBase + bonus;

        foreach (var e in playerEffects)
        {
            chance += e.data.defenseChancePoints / 100f;
        }

        return Math.Max(0f, Math.Min(settings.defenseChanceCap, chance));
    }

    public float FleeChance => Math.Min(settings.fleeChanceMax, settings.fleeChanceBase + player.agility * settings.fleeChancePerAgility);

    public float ComboMultiplier(int diceCount)
    {
        var list = settings.comboMultipliers;

        if (diceCount < 2 || list == null || list.Count == 0)
        {
            return 1f;
        }

        int index = Math.Min(diceCount - 2, list.Count - 1);
        return list[index];
    }

    // 예고 텍스트: 정보가 있으면 정확한 문구
    public string IntentText
    {
        get
        {
            if (Intent == null)
            {
                return "";
            }

            string text;

            if (KnowsMonster && !string.IsNullOrEmpty(Intent.telegraphClear))
            {
                text = Intent.telegraphClear;
            }
            else
            {
                text = string.IsNullOrEmpty(Intent.telegraphVague) ? Intent.actionName : Intent.telegraphVague;
            }

            // [B-4] 보스 페이즈 예고 문구 (예: 3페이즈 "도망칠 준비") — 텍스트만, 기믹 없음
            if (PhaseIndex >= 0 && !string.IsNullOrEmpty(monster.phases[PhaseIndex].intentNote))
            {
                text = monster.phases[PhaseIndex].intentNote + "\n" + text;
            }

            return text;
        }
    }

    // ---------- 턴 ----------

    void BeginTurn()
    {
        Turn++;
        usedSpecial = SpecialAction.None;
        defendSucceeded = false;
        fleeFailed = false;
        tauntUsedThisTurn = false;
        PendingRoll = null;
        rerollUsed = false;

        Intent = ChooseAction();
        MonsterReflecting = Intent != null && Intent.kind == MonsterActionKind.Reflect;
    }

    List<MonsterAction> CurrentActions
    {
        get
        {
            if (PhaseIndex >= 0 && monster.phases[PhaseIndex].actions.Count > 0)
            {
                return monster.phases[PhaseIndex].actions;
            }

            return monster.actions;
        }
    }

    MonsterAction ChooseAction()
    {
        var all = CurrentActions;
        var candidates = new List<MonsterAction>();
        float hpPercent = MonsterHp * 100f / MonsterMaxHp;

        foreach (var a in all)
        {
            if (a == null || a.weight <= 0)
            {
                continue;
            }

            if (a.condition == ActionCondition.HpBelow && hpPercent > a.hpBelowPercent)
            {
                continue;
            }

            if (a.condition == ActionCondition.NoRepeat && lastAction != null && lastAction.actionId == a.actionId)
            {
                continue;
            }

            candidates.Add(a);
        }

        if (candidates.Count == 0)
        {
            candidates.AddRange(all.FindAll(a => a != null));
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        int total = 0;

        foreach (var a in candidates)
        {
            total += Math.Max(1, a.weight);
        }

        int pick = rng.Next(total);

        foreach (var a in candidates)
        {
            pick -= Math.Max(1, a.weight);

            if (pick < 0)
            {
                return a;
            }
        }

        return candidates[candidates.Count - 1];
    }

    // ---------- 주인공 행동: 공격 ----------

    public DiceRoll RollDice()
    {
        if (Outcome != BattleOutcome.None || PendingRoll != null)
        {
            return PendingRoll;
        }

        var roll = new DiceRoll { faces = new int[Math.Max(1, player.diceCount)] };

        for (int i = 0; i < roll.faces.Length; i++)
        {
            if (i == 0 && lockedDieValue > 0)
            {
                roll.faces[i] = lockedDieValue;
                roll.lockedIndex = 0;
            }
            else
            {
                roll.faces[i] = rng.Next(1, player.diceFaces + 1);
            }
        }

        lockedDieValue = 0;
        Evaluate(roll);
        PendingRoll = roll;
        return roll;
    }

    void Evaluate(DiceRoll roll)
    {
        roll.sum = 0;
        bool allSame = roll.faces.Length >= 2;

        for (int i = 0; i < roll.faces.Length; i++)
        {
            roll.sum += roll.faces[i];

            if (roll.faces[i] != roll.faces[0])
            {
                allSame = false;
            }
        }

        roll.combo = allSame;
        // [C-4] 특성 "조합 배수 강화"
        roll.comboMultiplier = allSame ? ComboMultiplier(roll.faces.Length) * (1f + player.comboBonus) : 1f;
    }

    public List<SkillData> EquippedSkills
    {
        get
        {
            var list = new List<SkillData>();

            if (database == null)
            {
                return list;
            }

            foreach (var id in player.skillIds)
            {
                var s = database.FindSkill(id);

                if (s != null && list.Count < 2)
                {
                    list.Add(s);
                }
            }

            return list;
        }
    }

    public bool CanUseSkill(SkillData skill)
    {
        if (skill == null || PendingRoll == null || player.currentEnergy < skill.energyCost)
        {
            return false;
        }

        switch (skill.kind)
        {
            case SkillKind.Reroll: return !rerollUsed;
            case SkillKind.LockDie: return lockedDieValue == 0;
            default: return false;
        }
    }

    // 다시 굴리기: 이번 공격의 주사위를 한 번 다시 굴림
    public bool UseReroll(SkillData skill)
    {
        if (skill == null || skill.kind != SkillKind.Reroll || !CanUseSkill(skill))
        {
            return false;
        }

        SpendEnergy(skill);
        rerollUsed = true;

        for (int i = 0; i < PendingRoll.faces.Length; i++)
        {
            PendingRoll.faces[i] = rng.Next(1, player.diceFaces + 1);
        }

        PendingRoll.rerolled = true;
        PendingRoll.lockedIndex = -1;
        Evaluate(PendingRoll);
        Log(skill.skillName + "!");
        return true;
    }

    // 주사위 고정: 다음 공격에서 이 주사위 눈을 첫 번째 주사위로 고정
    public bool UseLockDie(SkillData skill, int dieIndex)
    {
        if (skill == null || skill.kind != SkillKind.LockDie || !CanUseSkill(skill) ||
            dieIndex < 0 || dieIndex >= PendingRoll.faces.Length)
        {
            return false;
        }

        SpendEnergy(skill);
        lockedDieValue = PendingRoll.faces[dieIndex];
        Log(skill.skillName + ": 다음 공격에서 " + lockedDieValue + " 고정");
        return true;
    }

    public int LockedDieValue => lockedDieValue;

    void SpendEnergy(SkillData skill)
    {
        player.currentEnergy -= skill.energyCost;
        energyUsed += skill.energyCost;
        skillUses++;
    }

    // 예상 피해 (치명타 없음 기준, 화면 표시용)
    public int PreviewDamage(DiceRoll roll)
    {
        return roll == null ? 0 : ComputePlayerDamage(roll, false, true);
    }

    int ComputePlayerDamage(DiceRoll roll, bool crit, bool withCombo)
    {
        float dice = roll.sum * (withCombo ? roll.comboMultiplier : 1f);
        float raw = (dice + PlayerAttackStat) * (crit ? settings.critMultiplier : 1f) * PlayerDamageFactor;
        float taken = Math.Max(0f, 1f + SumDamageTakenPercent(monsterEffects) / 100f);   // 빈틈 등
        return Clamp(raw * taken * 100f / (100f + MonsterDefense));
    }

    public AttackResult ConfirmAttack()
    {
        if (Outcome != BattleOutcome.None || PendingRoll == null)
        {
            return null;
        }

        var roll = PendingRoll;
        PendingRoll = null;

        bool crit = rng.NextDouble() < player.critChance;

        // [C-4] 특성 "치명타 시 주사위 1개 더": 합에 더한다
        if (crit && player.critExtraDie)
        {
            int extra = rng.Next(1, player.diceFaces + 1);
            roll.sum += extra;
            Log("치명타! 주사위 하나 더 → +" + extra);
        }

        int damage = ComputePlayerDamage(roll, crit, true);

        var result = new AttackResult { roll = roll, crit = crit, damage = damage };

        attackCount++;

        if (roll.combo)
        {
            comboCount++;
            comboBonusDamage += damage - ComputePlayerDamage(roll, crit, false);
        }

        if (crit)
        {
            critCount++;
        }

        if (MonsterReflecting)
        {
            result.reflected = true;
            result.reflectDamage = Clamp(damage * settings.reflectRatio);
            DamagePlayer(result.reflectDamage);
            Log("반사! " + result.reflectDamage + " 피해를 받았다.");
        }
        else
        {
            MonsterHp = Math.Max(0, MonsterHp - damage);
            damageDealt += damage;
            Log("주사위 " + string.Join(",", roll.faces) + (roll.combo ? " 조합 ×" + roll.comboMultiplier : "") +
                (crit ? " 치명타!" : "") + " → " + damage + " 피해");
        }

        AfterPlayerAction();
        return result;
    }

    // ---------- 주인공 행동: 방어 ----------

    public bool Defend(float touchBonus)
    {
        if (Outcome != BattleOutcome.None)
        {
            return false;
        }

        float chance = DefenseChance(touchBonus);
        bool success = rng.NextDouble() < chance;
        defendSucceeded = success;
        Log("방어 " + Math.Round(chance * 100) + "% → " + (success ? "성공" : "실패"));
        AfterPlayerAction();   // 다음 턴 시작에서 defendSucceeded가 초기화되므로 결과는 지역 변수로 돌려줌
        return success;
    }

    // ---------- 주인공 행동: 특수 ----------

    public bool CanSpecial(SpecialAction action)
    {
        if (Outcome != BattleOutcome.None || action == SpecialAction.None)
        {
            return false;
        }

        if (action == SpecialAction.EatFood)
        {
            return FirstConsumable() != null;
        }

        // 도발은 턴을 쓰지 않으므로 한 턴에 한 번만
        if (action == SpecialAction.Taunt)
        {
            return !tauntUsedThisTurn && settings.tauntEffect != null;
        }

        return true;
    }

    // 도발을 이번 턴에 이미 했는지 (화면 표시용)
    public bool TauntUsedThisTurn => tauntUsedThisTurn;

    public void Special(SpecialAction action)
    {
        if (!CanSpecial(action))
        {
            return;
        }

        // [B-3 규칙 변경] 도발은 턴을 쓰지 않는다: 효과만 걸고 같은 턴에 다른 행동을 이어서 한다
        if (action == SpecialAction.Taunt)
        {
            tauntUsedThisTurn = true;
            AddOrRefresh(monsterEffects, settings.tauntEffect, false);
            Log("도발! 괴이의 방어가 약해지고 공격이 거세진다 (" + settings.tauntEffect.duration + "턴)");
            return;
        }

        usedSpecial = action;

        switch (action)
        {
            case SpecialAction.EatFood:
                var item = FirstConsumable();
                player.RemoveItem(item.itemId, 1);
                player.currentHp = Math.Min(player.maxHp, player.currentHp + item.healHp);
                player.currentEnergy = Math.Min(player.maxEnergy, player.currentEnergy + item.healEnergy);
                Log(item.itemName + " 사용");
                break;

            default:
                Log(DisplayNames.Special(action) + "!");
                break;
        }

        AfterPlayerAction();
    }

    ItemData FirstConsumable()
    {
        if (database == null)
        {
            return null;
        }

        foreach (var stack in player.items)
        {
            var item = database.FindItem(stack.itemId);

            if (item != null && item.type == ItemType.Consumable && stack.count > 0)
            {
                return item;
            }
        }

        return null;
    }

    // ---------- 주인공 행동: 도망 ----------

    public bool Flee()
    {
        if (Outcome != BattleOutcome.None || !CanFlee)
        {
            return false;
        }

        if (rng.NextDouble() < FleeChance)
        {
            Outcome = BattleOutcome.Fled;
            Log("도망 성공");
            return true;
        }

        fleeFailed = true;
        Log("도망 실패! 이번 턴 받는 피해 ×" + settings.fleeFailDamageMultiplier);
        AfterPlayerAction();
        return false;
    }

    // ---------- 괴이 행동 ----------

    void AfterPlayerAction()
    {
        if (CheckEnd())
        {
            return;
        }

        CheckPhase(null);

        ExecuteMonsterAction(Intent, true);
        lastAction = Intent;

        if (CheckEnd())
        {
            return;
        }

        CheckPhase(lastAction);
        EndTurn();
    }

    void ExecuteMonsterAction(MonsterAction action, bool useTurnFlags)
    {
        if (action == null)
        {
            return;
        }

        switch (action.kind)
        {
            case MonsterActionKind.Attack:
                MonsterHit(action, useTurnFlags);
                break;

            case MonsterActionKind.Debuff:
                if (action.damageMultiplier > 0f)
                {
                    MonsterHit(action, useTurnFlags);
                }

                if (action.effect != null)
                {
                    bool blocked = useTurnFlags && action.effect.blockedBy != SpecialAction.None && usedSpecial == action.effect.blockedBy;

                    if (blocked)
                    {
                        Log(Josa.Eul(action.effect.effectName) + " 막았다!");
                        GiveOpening();
                    }
                    else if (player.debuffResist > 0f && rng.NextDouble() < player.debuffResist)
                    {
                        // [C-4] 정신력: 디버프 무시
                        Log("정신력으로 " + Josa.Eul(action.effect.effectName) + " 버텼다!");
                    }
                    else
                    {
                        AddOrRefresh(playerEffects, action.effect, true);
                        Log(action.effect.effectName + "에 걸렸다.");
                    }
                }
                break;

            case MonsterActionKind.Reflect:
                Log(Josa.Ga(monster.monsterName) + " 자세를 풀었다.");

                if (useTurnFlags && usedSpecial == SpecialAction.Wait)
                {
                    GiveOpening();
                }
                break;

            case MonsterActionKind.Buff:
                if (action.effect != null)
                {
                    AddOrRefresh(monsterEffects, action.effect, true);
                    Log(monster.monsterName + ": " + action.effect.effectName);
                }
                break;
        }
    }

    // 예고 대응 성공 보상: 괴이에게 빈틈 (다음 턴 공격 피해 증가)
    public int counterSuccessCount;

    void GiveOpening()
    {
        counterSuccessCount++;

        if (settings.counterOpeningEffect != null)
        {
            AddOrRefresh(monsterEffects, settings.counterOpeningEffect, true);
            Log("빈틈! 다음 공격 피해 증가");
        }
    }

    void MonsterHit(MonsterAction action, bool useTurnFlags)
    {
        if (useTurnFlags && defendSucceeded)
        {
            Log(action.actionName + " → 방어했다!");
            return;
        }

        float raw = MonsterAttack * action.damageMultiplier * 100f / (100f + PlayerDefenseStat);

        if (useTurnFlags && fleeFailed)
        {
            raw *= settings.fleeFailDamageMultiplier;
        }

        int damage = Clamp(raw);
        DamagePlayer(damage);
        Log(action.actionName + " → " + damage + " 피해");
    }

    void DamagePlayer(int damage)
    {
        player.currentHp = Math.Max(0, player.currentHp - damage);
        damageTaken += damage;
    }

    // ---------- 턴 끝 ----------

    void EndTurn()
    {
        TickEffects(playerEffects, true);
        TickEffects(monsterEffects, false);
        MonsterReflecting = false;

        if (CheckEnd())
        {
            return;
        }

        BeginTurn();
    }

    void TickEffects(List<ActiveEffect> effects, bool onPlayer)
    {
        for (int i = effects.Count - 1; i >= 0; i--)
        {
            var e = effects[i];

            // 괴이가 건 효과는 다음 턴부터 센다 (설계안 임시 10번)
            if (e.skipNextTick)
            {
                e.skipNextTick = false;
                continue;
            }

            if (onPlayer)
            {
                if (e.data.damagePerTurn > 0)
                {
                    DamagePlayer(e.data.damagePerTurn);
                    Log(e.data.effectName + " " + e.data.damagePerTurn + " 피해");
                }

                if (e.data.healPerTurn > 0)
                {
                    player.currentHp = Math.Min(player.maxHp, player.currentHp + e.data.healPerTurn);
                }
            }

            e.turnsLeft--;

            if (e.turnsLeft <= 0)
            {
                effects.RemoveAt(i);
            }
        }
    }

    // 중첩 없음: 이미 걸려 있으면 남은 턴만 처음으로
    void AddOrRefresh(List<ActiveEffect> effects, StatusEffectData data, bool skipNextTick)
    {
        var existing = effects.Find(e => e.data == data);

        if (existing != null)
        {
            existing.turnsLeft = data.duration;
            existing.skipNextTick = skipNextTick;
            return;
        }

        effects.Add(new ActiveEffect { data = data, turnsLeft = data.duration, skipNextTick = skipNextTick });
    }

    // ---------- 판정·페이즈 ----------

    bool CheckEnd()
    {
        if (Outcome != BattleOutcome.None)
        {
            return true;
        }

        if (MonsterHp <= 0)
        {
            Outcome = BattleOutcome.Win;
            Log("승리!");
        }
        else if (player.currentHp <= 0)
        {
            Outcome = BattleOutcome.Lose;
            Log("패배...");
        }

        return Outcome != BattleOutcome.None;
    }

    void CheckPhase(MonsterAction justDone)
    {
        // 한 번에 여러 페이즈 조건을 넘으면 차례로 적용
        while (PhaseIndex + 1 < monster.phases.Count)
        {
            var next = monster.phases[PhaseIndex + 1];
            bool triggered = next.trigger == PhaseTrigger.HpBelow
                ? MonsterHp * 100f / MonsterMaxHp <= next.hpPercent
                : justDone != null && justDone.actionId == next.afterActionId;

            if (!triggered)
            {
                return;
            }

            PhaseIndex++;
            phaseAttackMultiplier = next.attackMultiplier;
            phaseDefenseMultiplier = next.defenseMultiplier;
            PendingComic = next.comic;
            PendingPhaseMessage = next.phaseMessage;
            Log("페이즈 전환: " + next.phaseName);
        }
    }

    public void ClearPendingPhase()
    {
        PendingComic = null;
        PendingPhaseMessage = null;
    }

    // ---------- 도우미 ----------

    static float SumAttackPercent(List<ActiveEffect> effects)
    {
        float sum = 0f;

        foreach (var e in effects)
        {
            sum += e.data.attackPercent;
        }

        return sum;
    }

    static float SumDefensePercent(List<ActiveEffect> effects)
    {
        float sum = 0f;

        foreach (var e in effects)
        {
            sum += e.data.defensePercent;
        }

        return sum;
    }

    static float SumDamageTakenPercent(List<ActiveEffect> effects)
    {
        float sum = 0f;

        foreach (var e in effects)
        {
            sum += e.data.damageTakenPercent;
        }

        return sum;
    }

    static int Round(float value)
    {
        return (int)Math.Round(value, MidpointRounding.AwayFromZero);
    }

    // 반올림 + 최소 피해
    int Clamp(float value)
    {
        return Math.Max(settings.minDamage, Round(value));
    }

    void Log(string message)
    {
        log.Add("[" + Turn + "] " + message);
    }
}

public enum BattleOutcome
{
    None,
    Win,
    Lose,
    Fled
}

public class ActiveEffect
{
    public StatusEffectData data;
    public int turnsLeft;
    public bool skipNextTick;
}

public class DiceRoll
{
    public int[] faces;
    public int sum;
    public bool combo;
    public float comboMultiplier = 1f;
    public bool rerolled;
    public int lockedIndex = -1;
}

public class AttackResult
{
    public DiceRoll roll;
    public bool crit;
    public int damage;
    public bool reflected;
    public int reflectDamage;
}
