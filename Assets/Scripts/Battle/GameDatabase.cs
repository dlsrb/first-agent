using System.Collections.Generic;
using UnityEngine;

// ID → 에셋 찾기용 목록 (저장 데이터에는 ID만 넣고, 불러올 때 여기서 에셋을 찾는다)
[CreateAssetMenu(fileName = "GameDatabase", menuName = "요원/게임 데이터베이스 (GameDatabase)")]
public class GameDatabase : ScriptableObject
{
    public List<SkillData> skills = new List<SkillData>();
    public List<ItemData> items = new List<ItemData>();
    public List<MonsterData> monsters = new List<MonsterData>();

    public SkillData FindSkill(string id)
    {
        return skills.Find(s => s != null && s.skillId == id);
    }

    public ItemData FindItem(string id)
    {
        return items.Find(i => i != null && i.itemId == id);
    }

    public MonsterData FindMonster(string id)
    {
        return monsters.Find(m => m != null && m.monsterId == id);
    }
}
