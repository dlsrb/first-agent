using UnityEngine;
using TMPro;

// [C-4] 화면 위쪽 가운데 알림 (Canvas/NoticeToast). 몇 초 뒤 사라진다. 클릭을 막지 않는다.
// 예: "[매력으로 숨겨진 것이 드러났다]"
public class NoticeToast : MonoBehaviour
{
    public TMP_Text text;
    public GameObject box;
    public float seconds = 3f;

    static NoticeToast instance;
    float hideAt;

    public static string LastText { get; private set; } = "";

    void Awake()
    {
        instance = this;
        if (box != null) box.SetActive(false);
    }

    public static void Show(string message)
    {
        LastText = message;
        if (instance == null) instance = FindAnyObjectByType<NoticeToast>(FindObjectsInactive.Include);
        if (instance == null) { Debug.Log(message); return; }

        instance.text.text = message;
        instance.box.SetActive(true);
        instance.hideAt = Time.unscaledTime + instance.seconds;
    }

    void Update()
    {
        if (box != null && box.activeSelf && Time.unscaledTime >= hideAt) box.SetActive(false);
    }
}
