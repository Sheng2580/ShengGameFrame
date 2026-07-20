using UnityEngine;

namespace Sheng.GameFramework.UI
{
    /// <summary>
    /// UI 面板生命周期基类
    /// </summary>
    [DisallowMultipleComponent]
    public abstract class UIPanel : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;

        private RectTransform _rectTransform;
        private bool _created;

        public bool IsOpen { get; private set; }
        public RectTransform RectTransform => _rectTransform;

        protected virtual void Awake()
        {
            CacheComponents();
        }

        public void CloseSelf(bool destroy = false)
        {
            UIManager.Instance?.Close(GetType(), destroy);
        }

        protected virtual void OnCreated()
        {
        }

        protected virtual void OnOpened(object userData)
        {
        }

        protected virtual void OnClosed()
        {
        }

        protected virtual void OnBroughtToFront()
        {
        }

        internal void Initialize()
        {
            CacheComponents();
            if (_created)
            {
                return;
            }

            _created = true;
            OnCreated();
        }

        internal void Open(object userData)
        {
            Initialize();
            gameObject.SetActive(true);
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            IsOpen = true;
            OnOpened(userData);
        }

        internal void Close()
        {
            if (!IsOpen)
            {
                gameObject.SetActive(false);
                return;
            }

            OnClosed();
            IsOpen = false;
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            gameObject.SetActive(false);
        }

        internal void NotifyBroughtToFront()
        {
            OnBroughtToFront();
        }

        private void CacheComponents()
        {
            _rectTransform = transform as RectTransform;
            if (_rectTransform == null)
            {
                Debug.LogError($"[UIPanel] 面板根节点必须使用 RectTransform {name}", this);
            }

            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }

            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }
    }
}
