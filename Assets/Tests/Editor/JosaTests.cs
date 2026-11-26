using NUnit.Framework;

// 조사 고르기 (Josa) 검증
public class JosaTests
{
    [Test]
    public void Eul_ByFinalConsonant()
    {
        Assert.AreEqual("사기 저하를", Josa.Eul("사기 저하"));
        Assert.AreEqual("중독을", Josa.Eul("중독"));
        Assert.AreEqual("기침을", Josa.Eul("기침"));
    }

    [Test]
    public void Ga_SkipsBracketsAndReadsDigits()
    {
        Assert.AreEqual("괴이가", Josa.Ga("괴이"));
        Assert.AreEqual("[보스]가", Josa.Ga("[보스]"));
        Assert.AreEqual("[미정: urban_1]이", Josa.Ga("[미정: urban_1]"));   // 1 = 일(ㄹ 받침)
        Assert.AreEqual("[미정: urban_2]가", Josa.Ga("[미정: urban_2]"));   // 2 = 이
    }

    [Test]
    public void Euro_RieulTakesRo()
    {
        Assert.AreEqual("서울로", Josa.Euro("서울"));
        Assert.AreEqual("집으로", Josa.Euro("집"));
        Assert.AreEqual("학교로", Josa.Euro("학교"));
    }

    [Test]
    public void Eun_And_Gwa()
    {
        Assert.AreEqual("요원은", Josa.Eun("요원"));
        Assert.AreEqual("괴이는", Josa.Eun("괴이"));
        Assert.AreEqual("칼과", Josa.Gwa("칼"));
        Assert.AreEqual("주사위와", Josa.Gwa("주사위"));
    }
}
