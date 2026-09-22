namespace dotNetJavaWithJNI;

public class JavaBridgeTests
{
    [Fact]
    public void customAdd_WhenGiven2And3_Returns5()
    {
        var result = JavaBridge.InvokeCustomAdd(2, 3);

        Assert.Equal(5, result);
    }
}
