using System.Collections.Generic;
using UnityEngine;

// 만화(컷신) 1편 — 미국 카툰식: 한 페이지(1920x1080)에 칸 여러 개 (docs/아트방향.md).
// 그림 규칙: 칸마다 페이지와 같은 크기(1920x1080)의 투명 PNG, 칸 테두리·바탕은 "페이지 바탕" 1장, 대사는 그림에 넣지 않음.
// 이미지는 비워 두고 나중에 채운다. 비어 있으면 칸 위치(area)에 회색 사각형 + "[만화 n페이지 m칸 미정]".
// 재생: ComicViewer.Instance.Play(comic, 끝나면 할 일) — 게임 시작·스토리 중간·보스 입장·페이즈 전환 어디서든.
[CreateAssetMenu(fileName = "Comic_New", menuName = "요원/만화 (ComicData)")]
public class ComicData : ScriptableObject
{
    public string comicId = "";

    public List<ComicPage> pages = new List<ComicPage>();

    [Tooltip("배경 음악 (재생 칸만, E단계)")]
    public AudioClip bgm;
}

[System.Serializable]
public class ComicPage
{
    [Tooltip("페이지 바탕 레이어 (칸 테두리·배경, 1920x1080). 비어 있으면 검은 바탕")]
    public Sprite background;

    [Tooltip("칸 목록 (클릭할 때마다 순서대로 등장)")]
    public List<ComicPanel> panels = new List<ComicPanel>();
}

public enum PanelEffect
{
    Fade,    // 서서히 나타남
    Slide,   // 미끄러져 들어옴
    Pop      // 튀어나오듯 확대
}

public enum SlideFrom
{
    Left,
    Right,
    Top,
    Bottom
}

[System.Serializable]
public class ComicPanel
{
    [Tooltip("칸 이미지 (페이지와 같은 1920x1080 투명 PNG, 칸 바깥은 투명)")]
    public Sprite image;

    [Tooltip("칸 위치 (페이지 안 비율 0~1, 왼쪽 아래 기준). 이미지가 없을 때 회색 자리, 확대 효과의 중심에 쓰임")]
    public Rect area = new Rect(0f, 0f, 0.5f, 0.5f);

    public PanelEffect effect = PanelEffect.Fade;

    [Tooltip("Slide일 때 들어오는 방향")]
    public SlideFrom slideFrom = SlideFrom.Left;

    [Tooltip("이 칸과 함께 나타나는 대사")]
    public List<ComicLine> lines = new List<ComicLine>();
}

[System.Serializable]
public class ComicLine
{
    [TextArea(1, 4)]
    public string text = "[대사 미정]";

    [Tooltip("대사 위치 (페이지 안 비율 0~1, 말풍선 가운데)")]
    public Vector2 position = new Vector2(0.5f, 0.5f);

    [Tooltip("말풍선 너비 (픽셀, 1920 기준)")]
    public float width = 420f;
}
