using UnityEngine;

namespace Sheng.GameFramework.UI
{
    /// <summary>
    /// 根据屏幕安全区更新面板根节点锚点
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SafeAreaAdapter : MonoBehaviour
    {
        private RectTransform _rectTransform;
        private Rect _lastSafeArea;
        private int _lastScreenWidth;
        private int _lastScreenHeight;

        private void OnEnable()
        {
            ApplyNow();
        }

        private void Update()
        {
            if (_lastSafeArea != Screen.safeArea
                || _lastScreenWidth != Screen.width
                || _lastScreenHeight != Screen.height)
            {
                ApplyNow();
            }
        }

        public void ApplyNow()
        {
            _rectTransform ??= transform as RectTransform;
            if (_rectTransform == null || Screen.width <= 0 || Screen.height <= 0)
            {
                return;
            }

            Rect safeArea = Screen.safeArea;
            Vector2 anchorMin = safeArea.position;
            Vector2 anchorMax = safeArea.position + safeArea.size;
            anchorMin.x /= Screen.width;
            anchorMin.y /= Screen.height;
            anchorMax.x /= Screen.width;
            anchorMax.y /= Screen.height;

            _rectTransform.anchorMin = anchorMin;
            _rectTransform.anchorMax = anchorMax;
            _rectTransform.offsetMin = Vector2.zero;
            _rectTransform.offsetMax = Vector2.zero;
            _rectTransform.anchoredPosition = Vector2.zero;
            _rectTransform.localScale = Vector3.one;

            _lastSafeArea = safeArea;
            _lastScreenWidth = Screen.width;
            _lastScreenHeight = Screen.height;
        }
    }
}
