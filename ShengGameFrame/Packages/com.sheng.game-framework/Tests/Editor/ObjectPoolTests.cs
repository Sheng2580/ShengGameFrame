using System;
using NUnit.Framework;
using Sheng.GameFramework.Pooling;

namespace Sheng.GameFramework.Tests
{
    public sealed class ObjectPoolTests
    {
        private sealed class TestItem
        {
            public TestItem(int id)
            {
                Id = id;
            }

            public int Id { get; }
        }

        [Test]
        public void Constructor_PrewarmCreatesInactiveItems()
        {
            int nextId = 0;
            ObjectPool<TestItem> pool = new ObjectPool<TestItem>(
                () => new TestItem(++nextId),
                3,
                5);

            Assert.AreEqual(3, pool.CountAll);
            Assert.AreEqual(0, pool.CountActive);
            Assert.AreEqual(3, pool.CountInactive);
        }

        [Test]
        public void Rent_ReusesInactiveItemsInLifoOrder()
        {
            int nextId = 0;
            ObjectPool<TestItem> pool = new ObjectPool<TestItem>(
                () => new TestItem(++nextId));
            TestItem first = pool.Rent();
            TestItem second = pool.Rent();

            pool.Return(first);
            pool.Return(second);

            Assert.AreSame(second, pool.Rent());
            Assert.AreSame(first, pool.Rent());
        }

        [Test]
        public void Rent_AtBoundedCapacityReturnsNullWithoutStealing()
        {
            int nextId = 0;
            ObjectPool<TestItem> pool = new ObjectPool<TestItem>(
                () => new TestItem(++nextId),
                0,
                1);
            TestItem active = pool.Rent();

            Assert.NotNull(active);
            Assert.IsNull(pool.Rent());
            Assert.AreEqual(1, pool.CountActive);
        }

        [Test]
        public void UnlimitedPool_GrowsBeyondInitialCapacity()
        {
            int nextId = 0;
            ObjectPool<TestItem> pool = new ObjectPool<TestItem>(
                () => new TestItem(++nextId),
                1,
                -1);

            for (int i = 0; i < 20; i++)
            {
                Assert.NotNull(pool.Rent());
            }

            Assert.AreEqual(20, pool.CountAll);
            Assert.AreEqual(-1, pool.MaxCapacity);
        }

        [Test]
        public void Constructor_RejectsInvalidCapacity()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new ObjectPool<TestItem>(() => new TestItem(1), 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new ObjectPool<TestItem>(() => new TestItem(1), 0, -2));
            Assert.Throws<ArgumentException>(() =>
                new ObjectPool<TestItem>(() => new TestItem(1), 2, 1));
        }

        [Test]
        public void Return_WithChecksRejectsDuplicateAndForeignItems()
        {
            ObjectPool<TestItem> pool = new ObjectPool<TestItem>(
                () => new TestItem(1));
            TestItem item = pool.Rent();
            pool.Return(item);

            Assert.Throws<InvalidOperationException>(() => pool.Return(item));
            Assert.Throws<InvalidOperationException>(() =>
                pool.Return(new TestItem(2)));
        }

        [Test]
        public void Return_WithoutChecksReportsInvalidItems()
        {
            ObjectPool<TestItem> pool = new ObjectPool<TestItem>(
                () => new TestItem(1),
                collectionChecks: false);
            TestItem item = pool.Rent();

            Assert.IsTrue(pool.Return(item));
            Assert.IsFalse(pool.Return(item));
            Assert.IsFalse(pool.Return(new TestItem(2)));
        }

        [Test]
        public void SetMaxCapacity_DestroysExcessAsActiveItemsReturn()
        {
            int nextId = 0;
            int destroyedCount = 0;
            ObjectPool<TestItem> pool = new ObjectPool<TestItem>(
                () => new TestItem(++nextId),
                onDestroy: _ => destroyedCount++);
            TestItem first = pool.Rent();
            TestItem second = pool.Rent();
            TestItem third = pool.Rent();

            pool.SetMaxCapacity(1);
            pool.Return(first);
            pool.Return(second);
            pool.Return(third);

            Assert.AreEqual(1, pool.CountAll);
            Assert.AreEqual(1, pool.CountInactive);
            Assert.AreEqual(2, destroyedCount);
        }

        [Test]
        public void Clear_DestroysInactiveButKeepsActiveItems()
        {
            int nextId = 0;
            int destroyedCount = 0;
            ObjectPool<TestItem> pool = new ObjectPool<TestItem>(
                () => new TestItem(++nextId),
                2,
                4,
                onDestroy: _ => destroyedCount++);
            TestItem active = pool.Rent();

            pool.Clear();

            Assert.AreEqual(1, pool.CountAll);
            Assert.AreEqual(1, pool.CountActive);
            Assert.AreEqual(0, pool.CountInactive);
            Assert.AreEqual(1, destroyedCount);
            Assert.IsTrue(pool.Return(active));
        }

        [Test]
        public void Lease_DisposeReturnsItemOnce()
        {
            ObjectPool<TestItem> pool = new ObjectPool<TestItem>(
                () => new TestItem(1));
            PoolLease<TestItem> lease = pool.RentLease();

            lease.Dispose();
            lease.Dispose();

            Assert.AreEqual(0, pool.CountActive);
            Assert.AreEqual(1, pool.CountInactive);
        }
    }
}
