﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using Projecto.DependencyInjection;
using Projecto.Tests.TestClasses;

namespace Projecto.Tests
{
    [TestFixture]
    public class ProjectorTests
    {
        private readonly Mock<IConnectionLifetimeScopeFactory> _factoryMock;
        private Mock<IProjection<FakeMessageEnvelope>>[] _projectionMocks;

        public ProjectorTests()
        {
            _factoryMock = new Mock<IConnectionLifetimeScopeFactory>();
            _factoryMock.Setup(x => x.BeginLifetimeScope()).Returns(() => new Mock<IConnectionLifetimeScope>().Object);
        }

        [SetUp]
        public void SetUp()
        {
            _projectionMocks = new []
            {
                new Mock<IProjection<FakeMessageEnvelope>>(),
                new Mock<IProjection<FakeMessageEnvelope>>(),
                new Mock<IProjection<FakeMessageEnvelope>>()
            };
        }

        [Test]
        public void Constructor_PassNullAsProjectionSet_ShouldThrowException()
        {
            var ex = Assert.Throws<ArgumentNullException>(() => new Projector<FakeMessageEnvelope>(null, _factoryMock.Object));
            Assert.That(ex.ParamName, Is.EqualTo("projections"));
        }

        [Test]
        public void Constructor_PassEmptyProjectionSet_ShouldThrowException()
        {
            var emptySet = new HashSet<IProjection<FakeMessageEnvelope>>();
            var ex = Assert.Throws<ArgumentException>(() => new Projector<FakeMessageEnvelope>(emptySet, _factoryMock.Object));
            Assert.That(ex.ParamName, Is.EqualTo("projections"));
        }

        [Test]
        public void Constructor_PassNullAsConnectionLifetimeScopeFactory_ShouldThrowException()
        {
            var projections = new HashSet<IProjection<FakeMessageEnvelope>>(_projectionMocks.Select(x => x.Object));
            var ex = Assert.Throws<ArgumentNullException>(() => new Projector<FakeMessageEnvelope>(projections, null));
            Assert.That(ex.ParamName, Is.EqualTo("connectionLifetimeScopeFactory"));
        }

        [Test]
        public void NextSequence_GetAfterConstruction_ShouldReturnLowestNextSequence()
        {
            _projectionMocks[0].SetupGet(x => x.NextSequenceNumber).Returns(100);
            _projectionMocks[1].SetupGet(x => x.NextSequenceNumber).Returns(73);
            _projectionMocks[2].SetupGet(x => x.NextSequenceNumber).Returns(102);

            var projections = new HashSet<IProjection<FakeMessageEnvelope>>(_projectionMocks.Select(x => x.Object));
            var projector = new Projector<FakeMessageEnvelope>(projections, _factoryMock.Object);
            Assert.That(projector.NextSequenceNumber, Is.EqualTo(73));
        }

        [Test]
        public void Project_MessageWithWrongSequenceNumber_ShouldThrowException()
        {
            var messageEnvelope = new FakeMessageEnvelope(2, new MessageA());
            _projectionMocks[0].SetupGet(x => x.NextSequenceNumber).Returns(5);
            _projectionMocks[1].SetupGet(x => x.NextSequenceNumber).Returns(6);
            _projectionMocks[2].SetupGet(x => x.NextSequenceNumber).Returns(3);

            var projections = new HashSet<IProjection<FakeMessageEnvelope>>(_projectionMocks.Select(x => x.Object));
            var projector = new Projector<FakeMessageEnvelope>(projections, _factoryMock.Object);
            var ex = Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => projector.Project(messageEnvelope));
            Assert.That(ex.ParamName, Is.EqualTo("SequenceNumber"));
        }

        [Test]
        public void Project_ProjectionDoesntIncrementSequenceNumber_ShouldThrowException()
        {
            var messageEnvelope = new FakeMessageEnvelope(3, new MessageA());
            _projectionMocks[0].SetupGet(x => x.NextSequenceNumber).Returns(5);
            _projectionMocks[1].SetupGet(x => x.NextSequenceNumber).Returns(6);
            _projectionMocks[2].SetupGet(x => x.NextSequenceNumber).Returns(3);

            var projections = new HashSet<IProjection<FakeMessageEnvelope>>(_projectionMocks.Select(x => x.Object));
            var projector = new Projector<FakeMessageEnvelope>(projections, _factoryMock.Object);
            var ex = Assert.ThrowsAsync<InvalidOperationException>(() => projector.Project(messageEnvelope));
            Assert.That(ex.Message, Contains.Substring("did not increment NextSequence (3) after processing message"));
        }

        [Test]
        public async Task Project_MessageWithCorrectSequenceNumber_ShouldIncrementSequenceNumber()
        {
            var nextSequence = 3;
            var messageEnvelope = new FakeMessageEnvelope(nextSequence, new MessageA());
            _projectionMocks[0].SetupGet(x => x.NextSequenceNumber).Returns(5);
            _projectionMocks[1].SetupGet(x => x.NextSequenceNumber).Returns(6);
            _projectionMocks[2].SetupGet(x => x.NextSequenceNumber).Returns(() => nextSequence);
            _projectionMocks[2]
                .Setup(x => x.Handle(It.IsAny<Func<object>>(), messageEnvelope, CancellationToken.None))
                .Callback(() => nextSequence++)
                .Returns(() => Task.FromResult(0));

            var projections = new HashSet<IProjection<FakeMessageEnvelope>>(_projectionMocks.Select(x => x.Object));
            var projector = new Projector<FakeMessageEnvelope>(projections, _factoryMock.Object);
            Assert.That(projector.NextSequenceNumber, Is.EqualTo(3));
            await projector.Project(messageEnvelope);
            Assert.That(projector.NextSequenceNumber, Is.EqualTo(4));
        }

        [Test]
        public async Task Project_MessageWithCorrectSequenceNumber_ShouldCallHandleMethodOfProjectionsWithMatchingSequenceNumber()
        {
            var message = new MessageA();
            var nextSequences = new[] { 5, 6, 5 };
            _projectionMocks[0].SetupGet(x => x.NextSequenceNumber).Returns(() => nextSequences[0]);
            _projectionMocks[0]
                .Setup(x => x.Handle(It.IsAny<Func<object>>(), It.Is<FakeMessageEnvelope>(e => e.Message == message), CancellationToken.None))
                .Callback(() => nextSequences[0]++).Returns(() => Task.FromResult(0));

            _projectionMocks[1].SetupGet(x => x.NextSequenceNumber).Returns(() => nextSequences[1]);
            _projectionMocks[1]
                .Setup(x => x.Handle(It.IsAny<Func<object>>(), It.Is<FakeMessageEnvelope>(e => e.Message == message), CancellationToken.None))
                .Callback(() => nextSequences[1]++).Returns(() => Task.FromResult(0));

            _projectionMocks[2].SetupGet(x => x.NextSequenceNumber).Returns(() => nextSequences[2]);
            _projectionMocks[2]
                .Setup(x => x.Handle(It.IsAny<Func<object>>(), It.Is<FakeMessageEnvelope>(e => e.Message == message), CancellationToken.None))
                .Callback(() => nextSequences[2]++).Returns(() => Task.FromResult(0));

            var projections = new HashSet<IProjection<FakeMessageEnvelope>>(_projectionMocks.Select(x => x.Object));
            var projector = new Projector<FakeMessageEnvelope>(projections, _factoryMock.Object);

            await projector.Project(new FakeMessageEnvelope(5, message));
            _projectionMocks[0].Verify(
                x => x.Handle(It.IsAny<Func<object>>(), It.IsAny<FakeMessageEnvelope>(), It.IsAny<CancellationToken>()),
                Times.Once);
            _projectionMocks[1].Verify(
                x => x.Handle(It.IsAny<Func<object>>(), It.IsAny<FakeMessageEnvelope>(), It.IsAny<CancellationToken>()),
                Times.Never);
            _projectionMocks[2].Verify(
                x => x.Handle(It.IsAny<Func<object>>(), It.IsAny<FakeMessageEnvelope>(), It.IsAny<CancellationToken>()),
                Times.Once);

            await projector.Project(new FakeMessageEnvelope(6, message));
            _projectionMocks[0].Verify(
                x => x.Handle(It.IsAny<Func<object>>(), It.IsAny<FakeMessageEnvelope>(), It.IsAny<CancellationToken>()),
                Times.Exactly(2));
            _projectionMocks[1].Verify(
                x => x.Handle(It.IsAny<Func<object>>(), It.IsAny<FakeMessageEnvelope>(), It.IsAny<CancellationToken>()),
                Times.Once);
            _projectionMocks[2].Verify(
                x => x.Handle(It.IsAny<Func<object>>(), It.IsAny<FakeMessageEnvelope>(), It.IsAny<CancellationToken>()),
                Times.Exactly(2));
        }

        [Test]
        public async Task Project_CancelMessageWithCorrectSequenceNumber_ShouldCancelHandlerAndNotThrowSequenceNumberException()
        {
            var sequenceNumber = 10;
            var messageEnvelope = new FakeMessageEnvelope(sequenceNumber, new MessageA());
            var isCancelled = false;
            _projectionMocks[0].SetupGet(x => x.NextSequenceNumber).Returns(() => sequenceNumber);
            _projectionMocks[0]
                .Setup(x => x.Handle(It.IsAny<Func<object>>(), messageEnvelope, It.IsAny<CancellationToken>()))
                .Returns<object, FakeMessageEnvelope, CancellationToken>((_, __, token) =>
                {
                    return Task.Run(() =>
                    {
                        Thread.Sleep(20);
                        isCancelled = token.IsCancellationRequested;
                    });
                });

            var projections = new HashSet<IProjection<FakeMessageEnvelope>>(_projectionMocks.Take(1).Select(x => x.Object));
            var projector = new Projector<FakeMessageEnvelope>(projections, _factoryMock.Object);
            var cancellationTokenSource = new CancellationTokenSource(10);
            await projector.Project(messageEnvelope, cancellationTokenSource.Token);
            Assert.True(isCancelled);
        }

        [Test]
        public async Task Project_WithProjectionThatResolvesConnection_ShouldCreateResolveDisposeConnectionLifetimeScopeInCorrectOrder()
        {
            var nextSequence = 5;
            var messageEnvelope = new FakeMessageEnvelope(nextSequence, new MessageA());
            var connectionType = typeof(ConnectionA);

            _projectionMocks[0].SetupGet(x => x.NextSequenceNumber).Returns(() => nextSequence);
            _projectionMocks[0].SetupGet(x => x.ConnectionType).Returns(connectionType);
            _projectionMocks[0]
                .Setup(x => x.Handle(It.IsAny<Func<object>>(), messageEnvelope, CancellationToken.None))
                .Callback<Func<object>, FakeMessageEnvelope, CancellationToken>((connectionResolver, _, __) =>
                {
                    Assert.That(connectionResolver(), Is.Not.Null);
                    nextSequence++;
                })
                .Returns(() => Task.FromResult(0));

            var executionOrder = 0;

            var factoryMock = new Mock<IConnectionLifetimeScopeFactory>();
            factoryMock
                .Setup(x => x.BeginLifetimeScope())
                .Callback(() => Assert.That(executionOrder++, Is.EqualTo(0)))
                .Returns(() =>
                {
                    var scopeMock = new Mock<IConnectionLifetimeScope>();
                    scopeMock
                        .Setup(x => x.ResolveConnection(connectionType))
                        .Callback(() => Assert.That(executionOrder++, Is.EqualTo(1)))
                        .Returns(() => new FakeConnection());

                    scopeMock
                        .Setup(x => x.Dispose())
                        .Callback(() => Assert.That(executionOrder++, Is.EqualTo(2)));
                    return scopeMock.Object;
                });

            var projections = new HashSet<IProjection<FakeMessageEnvelope>>(_projectionMocks.Take(1).Select(x => x.Object));
            var projector = new Projector<FakeMessageEnvelope>(projections, factoryMock.Object);

            await projector.Project(messageEnvelope);

            Assert.That(executionOrder, Is.EqualTo(3));
        }
    }
}
