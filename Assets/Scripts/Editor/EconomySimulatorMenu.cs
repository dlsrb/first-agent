using System.IO;
using UnityEditor;
using UnityEngine;

// [C-6] 메뉴: 요원 > 경제 시뮬레이션 (1500명) → Console + docs/밸런스기록.md 맨 아래에 결과 추가
public static class EconomySimulatorMenu
{
    [MenuItem("요원/경제 시뮬레이션 (1500명)")]
    public static void Run()
    {
        string report = BuildReport(1500, 777);
        Debug.Log(report);
        string path = Path.Combine(Application.dataPath, "..", "docs", "밸런스기록.md");
        File.AppendAllText(path, "\n## 경제 시뮬레이션 " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "\n\n" + report + "\n");
    }

    public static string BuildReport(int runs, int seed)
    {
        var s = AssetDatabase.LoadAssetAtPath<BattleSettings>("Assets/Data/Battle/BattleSettings.asset");
        var db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Data/Battle/GameDatabase.asset");
        var region = AssetDatabase.LoadAssetAtPath<RegionData>("Assets/Data/Regions/Region_Main.asset");
        if (s == null || db == null || db.economy == null || region == null) return "BattleSettings·GameDatabase(economy)·Region_Main을 찾지 못했습니다.";
        return EconomySimulator.Report(s, db, region, runs, seed);
    }
}
