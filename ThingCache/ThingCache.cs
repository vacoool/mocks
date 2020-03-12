using System;
using System.Collections.Generic;
using System.Linq;
using FakeItEasy;
using FluentAssertions;
using NUnit.Framework;

namespace MockFramework
{
    public class ThingCache
    {
        private readonly IDictionary<string, Thing> dictionary
            = new Dictionary<string, Thing>();
        private readonly IThingService thingService;

        public ThingCache(IThingService thingService)
        {
            this.thingService = thingService;
        }

        public Thing Get(string thingId)
        {
            Thing thing;
            if (dictionary.TryGetValue(thingId, out thing))
                return thing;
            if (thingService.TryRead(thingId, out thing))
            {
                dictionary[thingId] = thing;
                return thing;
            }
            return null;
        }
    }

    [TestFixture]
    public class ThingCache_Should
    {
        private IThingService thingService;
        private ThingCache thingCache;

        private const string thingId1 = "TheDress";
        private Thing thing1 = new Thing(thingId1);

        private const string thingId2 = "CoolBoots";
        private Thing thing2 = new Thing(thingId2);

        [SetUp]
        public void SetUp()
        {
            thingService = A.Fake<IThingService>();
            thingCache = new ThingCache(thingService);
        }

        [Test]
        public void TryRead_ExecutedOnce_AfterFirstGetSameThing()
        {
            Thing _;
            thingCache.Get(thingId1).Should().BeNull();
            A.CallTo(() => thingService.TryRead(thingId1, out _))
                .MustHaveHappened(Repeated.Exactly.Once);
        }
        
        [Test]
        public void TryRead_NotExecuted_AfterFirstGetAnotherThing()
        {
            Thing _;
            thingCache.Get(thingId1).Should().BeNull();
            
            A.CallTo(() => thingService.TryRead(thingId2, out _))
                .MustNotHaveHappened();
        }

        [Test]
        public void TryRead_ExecutedExactlyOnce_AfterGetMultipleThings()
        {
            Thing _;
            A.CallTo(() => thingService.TryRead(thingId2, out _))
                .Returns(true).AssignsOutAndRefParameters(thing2);
            thingCache.Get(thingId2).Should().NotBeNull();
            thingCache.Get(thingId2).Should().NotBeNull();
            A.CallTo(() => thingService.TryRead(thingId2, out _)).MustHaveHappened(Repeated.Exactly.Once);
        }
        
                
        [Test]
        public void TryRead_ExecutedExactlyTwice_AfterGetMultipleThings()
        {
            Thing _;
            //thingCache.Get(thingId1).Should().BeNull();
            thingCache.Get(thingId2).Should().BeNull();
            thingCache.Get(thingId2).Should().BeNull();
            A.CallTo(() => thingService.TryRead(thingId2, out _)).MustHaveHappened(Repeated.Exactly.Twice);
        }
        
        [Test]
        public void TryRead_ExecutedOnce_ForEachThing()
        {
            Thing _;
            A.CallTo(() => thingService.TryRead(thingId1, out _))
                .Returns(true).AssignsOutAndRefParameters(thing1);
            A.CallTo(() => thingService.TryRead(thingId2, out _))
                .Returns(true).AssignsOutAndRefParameters(thing2);
            thingCache.Get(thingId1).Should().NotBeNull();
            thingCache.Get(thingId2).Should().NotBeNull();
            A.CallTo(() => thingService.TryRead(thingId2, out _)).MustHaveHappened(Repeated.Exactly.Once);
            A.CallTo(() => thingService.TryRead(thingId1, out _)).MustHaveHappened(Repeated.Exactly.Once);
        }
        
        [Test]
        public void TryRead_Throws_OnNullId()
        {
            Thing _;
            A.CallTo(() => thingService.TryRead(null, out _)).Throws<Exception>();
        }
        
        [Test]
        public void TryRead_CheckCalls()
        {
            thingService.TryRead(thingId1, out var _);
            thingService.TryRead(thingId1, out var _);
            var calls = Fake.GetCalls(thingService).ToList();
            calls.Count.ShouldBeEquivalentTo(2);
            calls[0].Method.Name.ShouldBeEquivalentTo("TryRead");
            calls[1].Method.Name.ShouldBeEquivalentTo("TryRead");
        }
        
        
        
    }
}