using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 만화 뷰어 (Canvas의 ComicPanel에 부착, 맨 위에 그려짐).
// 어디서든: ComicViewer.Instance.Play(comic, () => { 끝난 뒤 할 일 });
// 진행: 화면 클릭 → 다음 칸 등장 (등장 중이면 즉시 완료) → 페이지 끝이면 다음 페이지 → 마지막이면 끝. 건너뛰기 버튼.
public class ComicViewer : MonoBehaviour
{
    [Header("구성 요소")]
    public Image pageBackground;
    public RectTransform panelsRoot;
    public TMP_Text pageText;

    [Header("모양")]
    public TMP_FontAsset font;
    [Tooltip("말풍선·회색 자리용 스프라이트")]
    public Sprite bubbleSprite;
    public float effectSeconds = 0.35f;

    static ComicViewer instance;

    // 처음엔 꺼져 있어서 Awake가 돌지 않으므로, 꺼진 오브젝트까지 찾아서 돌려준다
    public static ComicViewer Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindAnyObjectByType<ComicViewer>(FindObjectsInactive.Include);
            }

            return instance;
        }
    }

    public bool IsPlaying => gameObject.activeSelf;
    public int PageIndex { get; private set; }
    public int PanelIndex { get; private set; }
    public bool Animating => animating;

    ComicData comic;
    Action onFinished;
    bool animating;
    Coroutine running;
    RectTransform currentGroup;
    readonly List<GameObject> shown = new List<GameObject>();

    static readonly Color PlaceholderColor = new Color(0.55f, 0.55f, 0.55f);
    static readonly Color Dark = new Color(0.12f, 0.12f, 0.12f);

    // ---------- 재생 ----------

    public void Play(ComicData data, Action finished)
    {
        if (data == null || data.pages == null || data.pages.Count == 0)
        {
            finished?.Invoke();
            return;
        }

        comic = data;
        onFinished = finished;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        ShowPage(0);
    }

    // 화면 클릭 (전체 화면 버튼 OnClick)
    public void OnAdvance()
    {
        if (comic == null) return;

        if (animating)
        {
            CompleteAnimation();
            return;
        }

        var page = comic.pages[PageIndex];

        if (PanelIndex + 1 < page.panels.Count)
        {
            ShowPanel(PanelIndex + 1);
        }
        else if (PageIndex + 1 < comic.pages.Count)
        {
            ShowPage(PageIndex + 1);
        }
        else
        {
            Finish();
        }
    }

    // 건너뛰기 버튼 OnClick
    public void OnSkip()
    {
        if (comic == null) return;
        Finish();
    }

    void Finish()
    {
        if (running != null)
        {
            StopCoroutine(running);
        }

        running = null;
        animating = false;
        ClearPanels();
        comic = null;
        gameObject.SetActive(false);

        var callback = onFinished;
        onFinished = null;
        callback?.Invoke();
    }

    // ---------- 페이지·칸 ----------

    void ShowPage(int index)
    {
        PageIndex = index;
        PanelIndex = -1;
        ClearPanels();

        var page = comic.pages[index];
        pageBackground.sprite = page.background;
        pageBackground.color = page.background != null ? Color.white : Color.black;

        if (page.panels.Count > 0)
        {
            ShowPanel(0);
        }
        else
        {
            UpdatePageText();
        }
    }

    void ShowPanel(int index)
    {
        CompleteAnimation();
        PanelIndex = index;

        var panel = comic.pages[PageIndex].panels[index];

        // 칸 + 대사를 한 묶음으로 등장시킨다 (페이지 전체 크기, 피벗은 칸 가운데)
        var group = NewRect("Panel_" + (index + 1), panelsRoot, Vector2.zero, Vector2.one);
        group.pivot = panel.area.center;
        group.gameObject.AddComponent<CanvasGroup>();
        shown.Add(group.gameObject);

        if (panel.image != null)
        {
            var img = NewRect("Image", group, Vector2.zero, Vector2.one).gameObject.AddComponent<Image>();
            img.sprite = panel.image;
            img.raycastTarget = false;
        }
        else
        {
            // 이미지가 없으면 칸 위치에 회색 자리
            var ph = NewRect("Placeholder", group, panel.area.min, panel.area.max);
            ph.offsetMin = new Vector2(6, 6);
            ph.offsetMax = new Vector2(-6, -6);
            var img = ph.gameObject.AddComponent<Image>();
            img.color = PlaceholderColor;
            img.raycastTarget = false;
            var label = NewText("Label", ph, "[만화 " + (PageIndex + 1) + "페이지 " + (index + 1) + "칸 미정]", 34, Color.white);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.sizeDelta = Vector2.zero;
        }

        foreach (var line in panel.lines)
        {
            AddLine(group, line);
        }

        currentGroup = group;
        running = StartCoroutine(Animate(group, panel));
        UpdatePageText();
    }

    void AddLine(RectTransform parent, ComicLine line)
    {
        var bubble = NewRect("Line", parent, line.position, line.position);
        var bg = bubble.gameObject.AddComponent<Image>();
        bg.sprite = bubbleSprite;
        bg.type = Image.Type.Sliced;
        bg.color = Color.white;
        bg.raycastTarget = false;

        var t = NewText("Text", bubble, line.text, 30, Dark);
        Vector2 size = t.GetPreferredValues(line.text, line.width - 40f, 0f);
        bubble.sizeDelta = new Vector2(line.width, size.y + 36f);
        t.rectTransform.anchorMin = Vector2.zero;
        t.rectTransform.anchorMax = Vector2.one;
        t.rectTransform.offsetMin = new Vector2(20, 14);
        t.rectTransform.offsetMax = new Vector2(-20, -14);
    }

    void UpdatePageText()
    {
        if (pageText == null) return;
        int panels = comic.pages[PageIndex].panels.Count;
        pageText.text = (PageIndex + 1) + "/" + comic.pages.Count + " 페이지 · " + Mathf.Max(0, PanelIndex + 1) + "/" + panels + "칸";
    }

    void ClearPanels()
    {
        foreach (var go in shown)
        {
            if (go != null)
            {
                go.SetActive(false);
                Destroy(go);
            }
        }

        shown.Clear();
        currentGroup = null;
    }

    // ---------- 등장 효과 ----------

    IEnumerator Animate(RectTransform group, ComicPanel panel)
    {
        animating = true;
        var cg = group.GetComponent<CanvasGroup>();
        Vector2 from = Vector2.zero;
        var size = panelsRoot.rect.size;

        if (panel.effect == PanelEffect.Slide)
        {
            switch (panel.slideFrom)
            {
                case SlideFrom.Left: from = new Vector2(-size.x * 0.35f, 0); break;
                case SlideFrom.Right: from = new Vector2(size.x * 0.35f, 0); break;
                case SlideFrom.Top: from = new Vector2(0, size.y * 0.35f); break;
                default: from = new Vector2(0, -size.y * 0.35f); break;
            }
        }

        float t = 0f;
        float duration = Mathf.Max(0.01f, effectSeconds);

        while (t < 1f)
        {
            t = Mathf.Min(1f, t + Time.unscaledDeltaTime / duration);
            float e = 1f - (1f - t) * (1f - t);   // 감속

            cg.alpha = panel.effect == PanelEffect.Pop ? Mathf.Min(1f, t * 2f) : e;

            if (panel.effect == PanelEffect.Slide)
            {
                group.anchoredPosition = Vector2.Lerp(from, Vector2.zero, e);
            }
            else if (panel.effect == PanelEffect.Pop)
            {
                // 0.6 → 1.08 → 1.0 (튀어나오는 느낌)
                float s = t < 0.7f ? Mathf.Lerp(0.6f, 1.08f, t / 0.7f) : Mathf.Lerp(1.08f, 1f, (t - 0.7f) / 0.3f);
                group.localScale = Vector3.one * s;
            }

            yield return null;
        }

        SetFinal(group);
        animating = false;
        running = null;
    }

    void CompleteAnimation()
    {
        if (!animating) return;

        if (running != null)
        {
            StopCoroutine(running);
        }

        if (currentGroup != null)
        {
            SetFinal(currentGroup);
        }

        running = null;
        animating = false;
    }

    static void SetFinal(RectTransform group)
    {
        group.GetComponent<CanvasGroup>().alpha = 1f;
        group.anchoredPosition = Vector2.zero;
        group.localScale = Vector3.one;
    }

    // ---------- 도우미 ----------

    static RectTransform NewRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        return rt;
    }

    TMP_Text NewText(string name, Transform parent, string text, float size, Color color)
    {
        var rt = NewRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
        return t;
    }
}
