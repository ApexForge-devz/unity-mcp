using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MCPForUnity.Editor.Services.Transport;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace MCPForUnityTests.Editor
{
    public class TransportCommandDispatcherQueueTests
    {
        private const string TestCommand = "__dispatcher_queue_test";
        private Dictionary<string, HandlerInfo> _handlers;
        private HandlerInfo _previousHandler;
        private TaskCompletionSource<object> _releaseFirst;
        private readonly List<int> _started = new List<int>();

        [SetUp]
        public void SetUp()
        {
            var field = typeof(CommandRegistry).GetField("_handlers", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(field);
            _handlers = (Dictionary<string, HandlerInfo>)field.GetValue(null);
            _handlers.TryGetValue(TestCommand, out _previousHandler);
            _releaseFirst = new TaskCompletionSource<object>();
            _started.Clear();
            _handlers[TestCommand] = new HandlerInfo(TestCommand, null, async parameters =>
            {
                int index = parameters.Value<int>("index");
                _started.Add(index);
                if (index == 1) await _releaseFirst.Task;
                return new JObject { ["index"] = index };
            });
        }

        [TearDown]
        public void TearDown()
        {
            _releaseFirst.TrySetResult(null);
            if (_previousHandler == null) _handlers.Remove(TestCommand);
            else _handlers[TestCommand] = _previousHandler;
        }

        [UnityTest]
        public IEnumerator AsyncCommandsExecuteInFIFOOrderWithoutOverlap()
        {
            var first = TransportCommandDispatcher.ExecuteCommandJsonAsync(
                new JObject { ["type"] = TestCommand, ["params"] = new JObject { ["index"] = 1 } }.ToString(),
                CancellationToken.None);
            var second = TransportCommandDispatcher.ExecuteCommandJsonAsync(
                new JObject { ["type"] = TestCommand, ["params"] = new JObject { ["index"] = 2 } }.ToString(),
                CancellationToken.None);
            Assert.That(_started, Is.EqualTo(new[] { 1 }));

            _releaseFirst.SetResult(null);
            yield return new UnityEngine.WaitUntil(() => second.IsCompleted && TransportCommandDispatcher.PendingCount == 0);
            Assert.That(first.IsCompleted, Is.True);
            Assert.That(_started, Is.EqualTo(new[] { 1, 2 }));
            Assert.That(second.IsFaulted, Is.False);
        }

        [UnityTest]
        public IEnumerator CallerCancellationDoesNotReleaseSlotBeforeHandlerFinishes()
        {
            using var cancellation = new CancellationTokenSource();
            var first = TransportCommandDispatcher.ExecuteCommandJsonAsync(
                new JObject { ["type"] = TestCommand, ["params"] = new JObject { ["index"] = 1 } }.ToString(),
                cancellation.Token);
            var second = TransportCommandDispatcher.ExecuteCommandJsonAsync(
                new JObject { ["type"] = TestCommand, ["params"] = new JObject { ["index"] = 2 } }.ToString(),
                CancellationToken.None);
            Assert.That(_started, Is.EqualTo(new[] { 1 }));

            cancellation.Cancel();
            Assert.That(first.IsCanceled, Is.True);
            Assert.That(_started, Is.EqualTo(new[] { 1 }));
            _releaseFirst.SetResult(null);
            yield return new UnityEngine.WaitUntil(() => second.IsCompleted && TransportCommandDispatcher.PendingCount == 0);
            Assert.That(_started, Is.EqualTo(new[] { 1, 2 }));
        }

        [Test]
        public void ModalCandidateMustHaveDisabledOwnerInSameProcess()
        {
            var disabledOwner = new System.IntPtr(20);
            var modal = new System.IntPtr(10);
            bool result = MCPForUnity.Editor.Helpers.ModalDialogProbe.HasDisabledOwner(
                modal, 42,
                handle => handle == modal ? disabledOwner : System.IntPtr.Zero,
                handle => handle != disabledOwner,
                handle => 42);
            Assert.IsTrue(result);
        }

        [Test]
        public void ModalCandidateWithoutOwnerIsNotGuessed()
        {
            bool result = MCPForUnity.Editor.Helpers.ModalDialogProbe.HasDisabledOwner(
                new System.IntPtr(10), 42,
                _ => System.IntPtr.Zero,
                _ => true,
                _ => 42);
            Assert.IsFalse(result);
        }

        [Test]
        public void ModalCandidateDoesNotCrossIntoAnotherProcessOwner()
        {
            var modal = new System.IntPtr(10);
            var foreignOwner = new System.IntPtr(20);
            bool result = MCPForUnity.Editor.Helpers.ModalDialogProbe.HasDisabledOwner(
                modal, 42,
                handle => handle == modal ? foreignOwner : System.IntPtr.Zero,
                _ => false,
                _ => 99);
            Assert.IsFalse(result);
        }
    }
}
