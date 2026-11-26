using System.Collections.Generic;
using UnityEngine;

// 만화(컷신) 1편. 이미지는 비워 두고 나중에 채운다. 비어 있는 장은 "[만화 n/m 미정]"으로 표시.
[CreateAssetMenu(fileName = "Comic_New", menuName = "요원/만화 (ComicData)")]
public class ComicData : ScriptableObject
{
    public string comicId = "";

    [Tooltip("넘겨 볼 이미지 (순서대로). 칸 수 = 장 수")]
    public List<Sprite> pages = new List<Sprite>();

    [Tooltip("배경 음악 (재생 칸만, E단계)")]
    public AudioClip bgm;
}
