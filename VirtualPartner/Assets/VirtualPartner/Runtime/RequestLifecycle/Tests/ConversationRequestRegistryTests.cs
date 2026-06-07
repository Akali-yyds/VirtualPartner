using NUnit.Framework;

namespace VirtualPartner.Runtime.Tests
{
    public sealed class ConversationRequestRegistryTests
    {
        // Property 1: 单一终态 — 进入终态后状态不再改变。
        [Test]
        public void TerminalStatusIsFinal()
        {
            var registry = new ConversationRequestRegistry();
            registry.Register(1, "toki");

            Assert.IsTrue(registry.TrySetStatus(1, RequestStatus.Finished));
            Assert.IsFalse(registry.TrySetStatus(1, RequestStatus.Failed));
            Assert.IsFalse(registry.TrySetStatus(1, RequestStatus.Canceled));

            Assert.IsTrue(registry.TryGet(1, out var request));
            Assert.AreEqual(RequestStatus.Finished, request.Status);
        }

        // Property 2: characterId 不变（且规范化为小写 trim）。
        [Test]
        public void CharacterIdNormalizedAndStable()
        {
            var registry = new ConversationRequestRegistry();
            registry.Register(7, "  Toki ");

            Assert.AreEqual("toki", registry.GetCharacterId(7));

            registry.TrySetStatus(7, RequestStatus.Playing);
            registry.TrySetStatus(7, RequestStatus.Finished);

            Assert.AreEqual("toki", registry.GetCharacterId(7));
        }

        // Property 3: 合法转移 — 不能回到 Pending；Playing 仅自 Pending/Playing；终态后拒绝。
        [Test]
        public void OnlyLegalTransitionsAreAccepted()
        {
            var registry = new ConversationRequestRegistry();
            registry.Register(2, "toki");

            Assert.IsTrue(registry.TrySetStatus(2, RequestStatus.Playing));
            Assert.IsFalse(registry.TrySetStatus(2, RequestStatus.Pending), "不应能回到 Pending");
            Assert.IsTrue(registry.TrySetStatus(2, RequestStatus.Finished));

            // 未注册的 requestId 转移被拒绝。
            Assert.IsFalse(registry.TrySetStatus(999, RequestStatus.Finished));
        }

        // Property 4: 活跃集合有界 — 全部终态化后活跃数归零、跟踪数受上限约束。
        [Test]
        public void ActiveSetIsBounded()
        {
            var registry = new ConversationRequestRegistry();
            const int total = 1000;
            for (var id = 1; id <= total; id++)
            {
                registry.Register(id, "toki");
                registry.TrySetStatus(id, RequestStatus.Finished);
            }

            Assert.AreEqual(0, registry.ActiveCount, "全部完成后不应有活跃请求");
            Assert.LessOrEqual(registry.TrackedCount, 256, "终态记录应被有界回收");
        }

        // Property 5: 批量替换只影响未终态，不动已完成项与最新项。
        [Test]
        public void MarkOlderPendingReplacedOnlyAffectsNonTerminal()
        {
            var registry = new ConversationRequestRegistry();
            registry.Register(10, "toki");
            registry.Register(11, "toki");
            registry.Register(12, "toki");
            registry.Register(20, "other");

            registry.TrySetStatus(10, RequestStatus.Finished); // 已终态，不应被改

            registry.MarkOlderPendingReplaced("toki", newestRequestId: 12);

            Assert.AreEqual(RequestStatus.Finished, GetStatus(registry, 10), "已完成项不应被替换");
            Assert.AreEqual(RequestStatus.Replaced, GetStatus(registry, 11), "更早的未终态项应被替换");
            Assert.AreEqual(RequestStatus.Pending, GetStatus(registry, 12), "最新项不应被替换");
            Assert.AreEqual(RequestStatus.Pending, GetStatus(registry, 20), "其它角色不受影响");
        }

        [Test]
        public void CancelCharacterCancelsOnlyNonTerminalOfThatCharacter()
        {
            var registry = new ConversationRequestRegistry();
            registry.Register(30, "toki");
            registry.Register(31, "toki");
            registry.Register(40, "other");
            registry.TrySetStatus(31, RequestStatus.Finished);

            registry.CancelCharacter("toki");

            Assert.AreEqual(RequestStatus.Canceled, GetStatus(registry, 30));
            Assert.AreEqual(RequestStatus.Finished, GetStatus(registry, 31), "已完成项不应被取消");
            Assert.AreEqual(RequestStatus.Pending, GetStatus(registry, 40), "其它角色不受影响");
        }

        private static RequestStatus GetStatus(ConversationRequestRegistry registry, int requestId)
        {
            Assert.IsTrue(registry.TryGet(requestId, out var request), $"requestId {requestId} 应存在");
            return request.Status;
        }
    }
}
