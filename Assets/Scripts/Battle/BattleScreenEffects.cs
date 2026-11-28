using UnityEngine;
using UnityEngine.UI;

// [C-5] 전투 화면 가장자리 연출 (UI규칙 3번). BattlePanel에 붙인다.
// - 체력이 기준(30%) 이하: 빨간 가장자리, 낮을수록 조금 진하게
// - 정신 계열 디버프(StatusEffectData.isMental)에 걸려 있는 동안: 검은 가장자리
// 가운데는 투명하고 가장자리로 갈수록 진한 그림을 실행 중에 만든다. 다른 UI보다 뒤(첫 자식)에 둬서 글자·버튼을 가리지 않는다.
// 깜빡임 없이 일정 속도로 나타나고 사라진다 (BattleSettings 칸으로 조정).
public class BattleScreenEffects : MonoBehaviour
{
    public BattleController battle;

    // 테스트·확인용: 지금 진하기와 목표 진하기
    public float LowHpAlpha { get; private set; }
    public float MentalAlpha { get; private set; }
    public float LowHpTarget { get; private set; }
    public float MentalTarget { get; private set; }

    Image lowHpImage;
    Image mentalImage;
    Sprite edgeSprite;
    float builtWidth = -1f;

    void Awake()
    {
        if (battle == null) battle = GetComponent<BattleController>();
        lowHpImage = MakeLayer("EdgeLowHp");
        mentalImage = MakeLayer("EdgeMental");
    }

    void OnEnable()
    {
        // 전투가 새로 시작되면 이전 전투의 가장자리를 남기지 않는다
        LowHpAlpha = MentalAlpha = 0f;
        Apply();
    }

    Image MakeLayer(string name)
    {
        var existing = transform.Find(name);
        var go = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform), typeof(Image));
        go.layer = gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(transform, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        rt.SetAsFirstSibling();
        var img = go.GetComponent<Image>();
        img.raycastTarget = false;
        img.color = Color.clear;
        return img;
    }

    void Update()
    {
        var s = battle != null ? battle.settings : null;
        var e = battle != null ? battle.Engine : null;
        if (s == null) return;

        if (!Mathf.Approximately(builtWidth, s.edgeWidth)) BuildSprite(s.edgeWidth);

        LowHpTarget = 0f;
        MentalTarget = 0f;

        if (e != null)
        {
            var p = e.player;
            float ratio = p.maxHp > 0 ? (float)p.currentHp / p.maxHp : 1f;

            if (ratio <= s.lowHpThreshold && p.currentHp > 0)
            {
                // 기준에서 min, 0에 가까울수록 max
                float t = s.lowHpThreshold > 0f ? 1f - ratio / s.lowHpThreshold : 1f;
                LowHpTarget = Mathf.Lerp(s.lowHpAlphaMin, s.lowHpAlphaMax, t);
            }

            if (e.playerEffects.Exists(x => x.data != null && x.data.isMental)) MentalTarget = s.mentalAlpha;
        }

        float step = s.edgeFadeSpeed * Time.unscaledDeltaTime;
        LowHpAlpha = Mathf.MoveTowards(LowHpAlpha, LowHpTarget, step);
        MentalAlpha = Mathf.MoveTowards(MentalAlpha, MentalTarget, step);
        Apply();
    }

    void Apply()
    {
        var s = battle != null ? battle.settings : null;
        if (s == null || lowHpImage == null) return;

        var c = s.lowHpColor; c.a = LowHpAlpha;
        lowHpImage.color = c;
        lowHpImage.enabled = LowHpAlpha > 0.001f;

        var m = s.mentalColor; m.a = MentalAlpha;
        mentalImage.color = m;
        mentalImage.enabled = MentalAlpha > 0.001f;
    }

    // 가운데 투명 → 가장자리 불투명 (부드러운 곡선). 화면 비율에 맞춰 늘어난다
    void BuildSprite(float width)
    {
        builtWidth = width;
        const int w = 128, h = 72;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[w * h];

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                // 가장자리까지 거리 (0 = 가장자리, 1 = 가운데)
                float dx = Mathf.Min(x, w - 1 - x) / (w * 0.5f);
                float dy = Mathf.Min(y, h - 1 - y) / (h * 0.5f);
                float d = Mathf.Min(dx, dy);
                float a = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(d / width));
                px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        }

        tex.SetPixels32(px);
        tex.Apply();

        if (edgeSprite != null) Destroy(edgeSprite.texture);
        edgeSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
        lowHpImage.sprite = edgeSprite;
        mentalImage.sprite = edgeSprite;
    }
}
