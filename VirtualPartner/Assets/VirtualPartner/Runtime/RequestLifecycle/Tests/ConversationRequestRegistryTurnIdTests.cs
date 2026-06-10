using NUnit.Framework;

namespace VirtualPartner.Runtime.Tests
{
    public sealed class ConversationRequestRegistryTurnIdTests
    {
        [Test]
        public void RegisterStoresStableTurnId()
        {
            var registry = new ConversationRequestRegistry();

            registry.Register(8, "toki", "turn_toki_abc");

            Assert.AreEqual("turn_toki_abc", registry.GetTurnId(8));
        }

        [Test]
        public void RegisterCreatesRequestFallbackTurnIdWhenMissing()
        {
            var registry = new ConversationRequestRegistry();

            registry.Register(9, "toki");

            Assert.AreEqual("request:9", registry.GetTurnId(9));
        }
    }
}
