using System.IO;
using UnityEditor;
using UnityEngine;

// 메뉴: 요원 > 전투 시뮬레이션 (1000회) → Console + docs/밸런스기록.md 맨 아래에 결과 추가
public static class BattleSimulatorMenu
{
    const string SettingsPath = "Assets/Data/Battle/BattleSettings.asset";
    const string DatabasePath = "Assets/Data/Battle/GameDatabase.asset";
    const string RegionPath = "Assets/Data/Regions/Region_Main.asset";

    [MenuItem("요원/전투 시뮬레이션 (1000회)")]
    public static void Run()
    {
        string report = BuildReport(1000, 12345);
        Debug.Log(report);

        string path = Path.Combine(Application.dataPath, "..", "docs", "밸런스기록.md");
        string header = File.Exists(path) ? "" : "# 밸런스 기록\n\n> 요원 > 전투 시뮬레이션 실행 결과를 아래에 쌓는다.\n\n";
        File.AppendAllText(path, header + "## " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "\n\n" + report + "\n");
    }

    public static string BuildReport(int n, int seed)
    {
        var settings = AssetDatabase.LoadAssetAtPath<BattleSettings>(SettingsPath);
        var db = AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath);
        var region = AssetDatabase.LoadAssetAtPath<RegionData>(RegionPath);

        if (settings == null || db == null || region == null)
        {
            return "BattleSettings·GameDatabase·Region_Main 에셋을 찾지 못했습니다.";
        }

        return BattleSimulator.RunReport(settings, db, region, n, seed);
    }
}
