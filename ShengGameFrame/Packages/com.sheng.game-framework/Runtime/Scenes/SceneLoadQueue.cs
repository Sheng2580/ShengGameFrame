using System.Collections.Generic;

namespace Sheng.GameFramework.Scenes
{
    /// <summary>
    /// 串行管理场景加载请求
    /// </summary>
    internal sealed class SceneLoadQueue
    {
        private readonly Queue<SceneLoadRequest> _pending =
            new Queue<SceneLoadRequest>();

        public SceneLoadRequest Current { get; private set; }
        public int PendingCount => _pending.Count;

        public void Enqueue(SceneLoadRequest request)
        {
            _pending.Enqueue(request);
        }

        public bool TryBeginNext(out SceneLoadRequest request)
        {
            if (Current != null || _pending.Count == 0)
            {
                request = null;
                return false;
            }

            Current = _pending.Dequeue();
            request = Current;
            return true;
        }

        public bool CompleteCurrent(SceneLoadRequest request)
        {
            if (!ReferenceEquals(Current, request))
            {
                return false;
            }

            Current = null;
            return true;
        }

        public bool CancelPending(SceneLoadRequest request)
        {
            if (request == null || ReferenceEquals(Current, request))
            {
                return false;
            }

            bool removed = false;
            int count = _pending.Count;
            for (int i = 0; i < count; i++)
            {
                SceneLoadRequest current = _pending.Dequeue();
                if (!removed && ReferenceEquals(current, request))
                {
                    removed = true;
                    continue;
                }

                _pending.Enqueue(current);
            }

            return removed;
        }

        public int ClearPending(List<SceneLoadRequest> removedRequests)
        {
            int removedCount = _pending.Count;
            while (_pending.Count > 0)
            {
                SceneLoadRequest request = _pending.Dequeue();
                removedRequests?.Add(request);
            }

            return removedCount;
        }
    }
}
