using System;

using NUnit.Framework;

using Scp.Domain;

namespace Scp.Domain.Tests
{
    public sealed class DeterministicRandomTests
    {
        [Test]
        public void NextInt_SameSeed_ProducesSameSequence()
        {
            var first = new DeterministicRandom(42);
            var second = new DeterministicRandom(42);

            for (var index = 0; index < 1000; index++)
            {
                Assert.That(first.NextInt(-10, 25), Is.EqualTo(second.NextInt(-10, 25)));
            }
        }

        [Test]
        public void NextInt_ValidRange_StaysInsideBounds()
        {
            var random = new DeterministicRandom(7);

            for (var index = 0; index < 1000; index++)
            {
                var value = random.NextInt(-3, 4);
                Assert.That(value, Is.GreaterThanOrEqualTo(-3).And.LessThan(4));
            }
        }

        [Test]
        public void Chance_BoundariesAndInvalidValues_AreHandled()
        {
            var random = new DeterministicRandom(9);

            Assert.That(random.Chance(0), Is.False);
            Assert.That(random.Chance(10000), Is.True);
            Assert.Throws<ArgumentOutOfRangeException>(() => random.Chance(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => random.Chance(10001));
        }
    }
}
