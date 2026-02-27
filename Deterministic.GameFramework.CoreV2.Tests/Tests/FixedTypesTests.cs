using Deterministic.GameFramework.CoreV2;
using FluentAssertions;
using Xunit;

namespace Deterministic.GameFramework.CoreV2.Tests
{
    public class FixedTypesTests
    {
        [Fact]
        public void FixedString32_ShouldHandleTruncation()
        {
            var longString = new string('A', 50);
            var fs = new FixedString32(longString);
            
            fs.ToString().Length.Should().BeLessOrEqualTo(32);
        }

        [Fact]
        public void FixedString32_ShouldBeDeterministic()
        {
            var fs1 = new FixedString32("Test");
            var fs2 = new FixedString32("Test");
            
            fs1.Equals(fs2).Should().BeTrue();
            fs1.GetHashCode().Should().Be(fs2.GetHashCode());
        }

        [Fact]
        public void FixedString32_ShouldHandleUTF8Correctly()
        {
            var emoji = "Hello 🎮";
            var fs = new FixedString32(emoji);
            
            fs.ToString().Should().Contain("Hello");
        }

        [Fact]
        public void List8_ShouldClearUnusedSlots()
        {
            var list = new List8<int>();
            list.Add(10);
            list.Add(20);
            list.Add(30);
            
            list.Clear();
            list.Count.Should().Be(0);
            
            list.Add(40);
            list[0].Should().Be(40);
        }

        [Fact]
        public void List8_ShouldThrowOnOverflow()
        {
            var list = new List8<int>();
            for (int i = 0; i < 8; i++)
            {
                list.Add(i);
            }
            
            list.Count.Should().Be(8);
            
            Action act = () => list.Add(999);
            act.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void List8_ShouldBeDeterministicAcrossInstances()
        {
            var list1 = new List8<int>();
            var list2 = new List8<int>();
            
            for (int i = 0; i < 5; i++)
            {
                list1.Add(i * 10);
                list2.Add(i * 10);
            }
            
            for (int i = 0; i < 5; i++)
            {
                list1[i].Should().Be(list2[i]);
            }
        }
    }
}
