using FluentAssertions;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using Xunit;
using static FluentAssertions.FluentActions;

namespace RiCo.SequentialGuid.Tests
{
    public sealed class SequentialGuidTests
    {
        [Fact]
        public void MultiThreadTest()
        {
            for (var i = 5000; i < 6000; i++)
            {
                var guid = Guid.NewGuid();
                var sequential = new SequentialGuid(guid);
                Enumerable.Range(0, i)
                    .AsParallel()
                    .WithDegreeOfParallelism(512)
                    .WithExecutionMode(ParallelExecutionMode.ForceParallelism)
                    .ForAll(_ => sequential.Next(DateTime.Now));

                var next = sequential.Next(DateTime.Now);
                var original = new BigInteger(guid.ToByteArray());
                var diff = new BigInteger(next.ToByteArray()) - original;
                Trace.WriteLine($"{guid} - {next} = {diff}");

                (i + 1).Should().Equals(diff);
            }
        }

        [Fact]
        public void MultithreadDuplicateTest()
        {
            for (var i = 5000; i < 6000; i++)
            {
                var bag = new ConcurrentBag<Guid>();
                var guid = Guid.NewGuid();
                var sequential = new SequentialGuid(guid);
                Enumerable.Range(0, i)
                    .AsParallel()
                    .WithDegreeOfParallelism(512)
                    .WithExecutionMode(ParallelExecutionMode.ForceParallelism)
                    .ForAll(_ => bag.Add(sequential.Next(DateTime.Now)));

                var distinct = bag.Distinct().ToList();

                distinct.Count.Should().Be(bag.Count);
            }
        }

        [Fact]
        public void CurrentTest()
        {
            var guid = Guid.NewGuid();
            var sequential = new SequentialGuid(guid);
            guid.Should().Be(sequential.Current);

            sequential.Next(DateTime.Now);
            guid.Should().NotBe(sequential.Current);
        }

        [Fact]
        public void OutOfRangeTest()
        {
            var guid = Guid.NewGuid();
            var endSequence = new DateTime(2010, 2, 3);
            var sequential = new SequentialGuid(guid, endSequence);
            Invoking(() => sequential.Next(DateTime.Now)).Should()
                .Throw<ArgumentOutOfRangeException>();
        }
    }
}