// 한국어 조사 고르기: 앞 단어의 마지막 글자 받침에 따라 을/를, 이/가, 은/는, 와/과, 으로/로.
// 예) Josa.Eul("사기 저하") → "사기 저하를", Josa.Ga("괴이") → "괴이가"
// 끝의 ] ) 따옴표·공백은 건너뛰고 본다. 숫자는 읽는 소리 기준, 영문은 받침 없음으로 본다.
public static class Josa
{
    public static string Eul(string word) { return word + Pick(word, "을", "를"); }
    public static string Ga(string word) { return word + Pick(word, "이", "가"); }
    public static string Eun(string word) { return word + Pick(word, "은", "는"); }
    public static string Gwa(string word) { return word + Pick(word, "과", "와"); }

    // 으로/로: ㄹ 받침도 "로"
    public static string Euro(string word)
    {
        int jong = FinalConsonant(word);
        return word + (jong > 0 && jong != 8 ? "으로" : "로");
    }

    // 받침 있으면 withFinal, 없으면 withoutFinal
    public static string Pick(string word, string withFinal, string withoutFinal)
    {
        return FinalConsonant(word) > 0 ? withFinal : withoutFinal;
    }

    // 마지막 글자의 종성 번호 (0 = 받침 없음, 8 = ㄹ)
    static int FinalConsonant(string word)
    {
        if (string.IsNullOrEmpty(word)) return 0;

        for (int i = word.Length - 1; i >= 0; i--)
        {
            char c = word[i];

            if (c == ']' || c == ')' || c == '"' || c == '\'' || c == ' ' || c == '.' || c == '!' || c == '?')
            {
                continue;
            }

            if (c >= 0xAC00 && c <= 0xD7A3)
            {
                return (c - 0xAC00) % 28;
            }

            if (c >= '0' && c <= '9')
            {
                // 영(ㅇ) 일(ㄹ) 이 삼(ㅁ) 사 오 육(ㄱ) 칠(ㄹ) 팔(ㄹ) 구
                switch (c)
                {
                    case '0': return 21;
                    case '1': case '7': case '8': return 8;
                    case '3': return 16;
                    case '6': return 1;
                    default: return 0;
                }
            }

            return 0;   // 영문 등: 받침 없음으로 본다
        }

        return 0;
    }
}
