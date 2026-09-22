using com.example.operations;

namespace dotNetJavaWithkvm;

public class CustomOperationsKvmTests
{
    [Fact]
    public void customAdd_ReturnsSum()
    {
        var sut = new CustomOperationsImpl();

        var result = sut.customAdd(2, 3);

        Assert.Equal(5, result);
    }
}
