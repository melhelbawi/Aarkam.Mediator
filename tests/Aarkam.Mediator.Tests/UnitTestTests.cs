using System;
using Xunit;
using Aarkam.Mediator;

namespace Aarkam.Mediator.Tests
{
    public class UnitTestTests
    {
        [Fact]
        public void Unit_Value_Equals_DefaultUnit()
        {
            var u1 = Unit.Value;
            var u2 = new Unit();

            Assert.Equal(u1, u2);
            Assert.True(u1.Equals(u2));
        }

        [Fact]
        public void Unit_Value_Equals_Itself()
        {
            var u = Unit.Value;
            Assert.True(u.Equals(u));
        }

        [Fact]
        public void Unit_EqualsObject_ReturnsTrueForUnit()
        {
            object obj = Unit.Value;
            Assert.True(Unit.Value.Equals(obj));
        }

        [Fact]
        public void Unit_EqualsObject_ReturnsFalseForNull()
        {
            Assert.False(Unit.Value.Equals((object)null!));
        }

        [Fact]
        public void Unit_EqualsObject_ReturnsFalseForOtherTypes()
        {
            Assert.False(Unit.Value.Equals("not unit"));
            Assert.False(Unit.Value.Equals(123));
            Assert.False(Unit.Value.Equals(Guid.NewGuid()));
        }

        [Fact]
        public void Unit_GetHashCode_AlwaysReturnsZero()
        {
            Assert.Equal(0, Unit.Value.GetHashCode());
            Assert.Equal(0, new Unit().GetHashCode());
        }

        [Fact]
        public void Unit_CompareTo_Unit_AlwaysReturnsZero()
        {
            Assert.Equal(0, Unit.Value.CompareTo(Unit.Value));
            Assert.Equal(0, Unit.Value.CompareTo(new Unit()));
        }

        [Fact]
        public void Unit_CompareToObject_ReturnsZeroForUnit()
        {
            object boxed = new Unit();
            Assert.Equal(0, Unit.Value.CompareTo(boxed));
        }

        [Fact]
        public void Unit_CompareToObject_ReturnsZeroForNull()
        {
            Assert.Equal(0, Unit.Value.CompareTo((object)null!));
        }

        [Fact]
        public void Unit_CompareToObject_ThrowsArgumentExceptionForOtherTypes()
        {
            Assert.Throws<ArgumentException>(() => Unit.Value.CompareTo("invalid"));
            Assert.Throws<ArgumentException>(() => Unit.Value.CompareTo(42));
        }

        [Fact]
        public void Unit_ToString_ReturnsParentheses()
        {
            Assert.Equal("()", Unit.Value.ToString());
            Assert.Equal("()", new Unit().ToString());
        }

        [Fact]
        public void Unit_EqualityOperator_ReturnsTrue()
        {
            var u1 = Unit.Value;
            var u2 = new Unit();
            Assert.True(u1 == u2);
        }

        [Fact]
        public void Unit_InequalityOperator_ReturnsFalse()
        {
            var u1 = Unit.Value;
            var u2 = new Unit();
            Assert.False(u1 != u2);
        }
    }
}
