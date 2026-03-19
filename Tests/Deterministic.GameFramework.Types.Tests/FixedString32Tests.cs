using System.Text;
using Xunit;
using Deterministic.GameFramework.Types;

namespace Deterministic.GameFramework.Types.Tests;

public class FixedString32Tests
{
    [Fact]
    public void Constructor_ShouldSetString()
    {
        var str = "Hello World";
        var fixedStr = new FixedString32(str);
        
        Assert.Equal(str, fixedStr.ToString());
    }

    [Fact]
    public void ImplicitConversion_ShouldWork()
    {
        string str = "Test String";
        FixedString32 fixedStr = str;
        string result = fixedStr;
        
        Assert.Equal(str, result);
    }

    [Fact]
    public void Equality_ShouldWork()
    {
        var str1 = new FixedString32("Hello");
        var str2 = new FixedString32("Hello");
        var str3 = new FixedString32("World");
        
        Assert.True(str1 == str2);
        Assert.False(str1 == str3);
        Assert.True(str1.Equals(str2));
        Assert.Equal(str1.GetHashCode(), str2.GetHashCode());
    }

    [Fact]
    public void MaxLength_ShouldTruncate()
    {
        // 32 chars is max. Let's try 33.
        var longString = "123456789012345678901234567890123";
        var expected = "12345678901234567890123456789012";
        
        var fixedStr = new FixedString32(longString);
        Assert.Equal(expected, fixedStr.ToString());
    }

    [Fact]
    public void EmptyString_ShouldWork()
    {
        var fixedStr = new FixedString32("");
        Assert.Equal("", fixedStr.ToString());
        
        var fixedStrNull = new FixedString32(null!);
        Assert.Equal("", fixedStrNull.ToString());
    }
    
    [Fact]
    public void SpecialCharacters_ShouldWork()
    {
        var str = "Héllo @#$";
        var fixedStr = new FixedString32(str);
        Assert.Equal(str, fixedStr.ToString());
    }

    [Fact]
    public void StandardOverrides_ShouldWork()
    {
        var s1 = new FixedString32("abc");
        var s2 = new FixedString32("abc");
        var s3 = new FixedString32("def");

        Assert.True(s1.Equals((object)s2));
        Assert.False(s1.Equals((object)s3));
        Assert.False(s1.Equals((object?)null));
        Assert.False(s1.Equals(123));

        Assert.True(s1 != s3);
        
        Assert.Equal(s1.GetHashCode(), s2.GetHashCode());
        Assert.NotEqual(s1.GetHashCode(), s3.GetHashCode());
    }
}
