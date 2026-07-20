using System;
using UnityEngine;

namespace Sheng.GameFramework.Core
{
    internal static class SingletonRuntimeReset
    {
        internal static event Action ResetRequested;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetAll()
        {
            ResetRequested?.Invoke();
        }
    }
}
