using System;
using System.Collections.Generic;
using System.Reflection;
using Sheng.GameFramework.Assets;
using Sheng.GameFramework.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Sheng.GameFramework.UI
{
    /// <summary>
    /// 负责 UI 根节点 分层加载 模态遮罩和面板缓存
    /// </summary>
    public sealed class UIManager : PersistentMonoSingleton<UIManager>
    {
        private sealed class PendingPanelLoad
        {
            public readonly int Version;
            public readonly object UserData;
            public readonly List<Action<UIPanel>> Callbacks = new List<Action<UIPanel>>();

            public PendingPanelLoad(int version, object userData)
            {
                Version = version;
                UserData = userData;
            }
        }

        private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

        private readonly Dictionary<UILayer, int> _layerOrders = new Dictionary<UILayer, int>
        {
            { UILayer.Background, 0 },
            { UILayer.HUD, 100 },
            { UILayer.Touch, 200 },
            { UILayer.Normal, 300 },
            { UILayer.Popup, 500 },
            { UILayer.Tip, 700 },
            { UILayer.System, 900 }
        };

        private readonly Dictionary<UILayer, RectTransform> _layerRoots =
            new Dictionary<UILayer, RectTransform>();
        private readonly Dictionary<Type, UIPanelAttribute> _panelConfigs =
            new Dictionary<Type, UIPanelAttribute>();
        private readonly Dictionary<Type, UIPanel> _openedPanels =
            new Dictionary<Type, UIPanel>();
        private readonly Dictionary<Type, UIPanel> _cachedPanels =
            new Dictionary<Type, UIPanel>();
        private readonly Dictionary<Type, PendingPanelLoad> _pendingLoads =
            new Dictionary<Type, PendingPanelLoad>();
        private readonly Dictionary<Type, int> _loadVersions =
            new Dictionary<Type, int>();
        private readonly Dictionary<UIPanel, GameObject> _modalMasks =
            new Dictionary<UIPanel, GameObject>();

        private RectTransform _uiRoot;

        public RectTransform Root => _uiRoot;

        protected override void OnSingletonAwake()
        {
            EnsureRoot();
            EnsureEventSystem();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        protected override void OnSingletonDestroyed()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            CancelAllPendingLoads();
            _openedPanels.Clear();
            _cachedPanels.Clear();
            _modalMasks.Clear();
            _layerRoots.Clear();
            _panelConfigs.Clear();
        }

        public void OpenAsync<T>(Action<T> completed = null, object userData = null)
            where T : UIPanel
        {
            EnsureRoot();
            Type panelType = typeof(T);

            if (_openedPanels.TryGetValue(panelType, out UIPanel openedPanel))
            {
                BringToFront(openedPanel);
                InvokeSafely(completed, openedPanel as T);
                return;
            }

            if (_cachedPanels.TryGetValue(panelType, out UIPanel cachedPanel) && cachedPanel != null)
            {
                _cachedPanels.Remove(panelType);
                OpenPanelInstance(panelType, cachedPanel, userData);
                InvokeSafely(completed, cachedPanel as T);
                return;
            }

            Action<UIPanel> wrappedCallback = panel => InvokeSafely(completed, panel as T);
            if (_pendingLoads.TryGetValue(panelType, out PendingPanelLoad pendingLoad))
            {
                if (completed != null)
                {
                    pendingLoad.Callbacks.Add(wrappedCallback);
                }

                return;
            }

            int version = _loadVersions.TryGetValue(panelType, out int oldVersion)
                ? oldVersion + 1
                : 1;
            _loadVersions[panelType] = version;

            PendingPanelLoad newLoad = new PendingPanelLoad(version, userData);
            if (completed != null)
            {
                newLoad.Callbacks.Add(wrappedCallback);
            }

            _pendingLoads.Add(panelType, newLoad);
            UIPanelAttribute config = GetPanelConfig(panelType);
            LoadPanelPrefabAsync<T>(config, version);
        }

        public void Close<T>(bool destroy = false) where T : UIPanel
        {
            Close(typeof(T), destroy);
        }

        public void Close(Type panelType, bool destroy = false)
        {
            if (panelType == null || !typeof(UIPanel).IsAssignableFrom(panelType))
            {
                return;
            }

            CancelPendingLoad(panelType);

            if (!_openedPanels.TryGetValue(panelType, out UIPanel panel) || panel == null)
            {
                if (destroy && _cachedPanels.TryGetValue(panelType, out UIPanel cachedPanel))
                {
                    _cachedPanels.Remove(panelType);
                    if (cachedPanel != null)
                    {
                        Destroy(cachedPanel.gameObject);
                    }
                }

                return;
            }

            _openedPanels.Remove(panelType);
            DestroyModalMask(panel);
            panel.Close();

            UIPanelAttribute config = GetPanelConfig(panelType);
            if (destroy || !config.CacheOnClose)
            {
                Destroy(panel.gameObject);
                return;
            }

            if (_cachedPanels.TryGetValue(panelType, out UIPanel oldCachedPanel)
                && oldCachedPanel != null
                && oldCachedPanel != panel)
            {
                Destroy(oldCachedPanel.gameObject);
            }

            _cachedPanels[panelType] = panel;
        }

        public void CloseAll(bool destroy = false)
        {
            CancelAllPendingLoads();

            Type[] openedTypes = new Type[_openedPanels.Count];
            _openedPanels.Keys.CopyTo(openedTypes, 0);
            for (int i = 0; i < openedTypes.Length; i++)
            {
                Close(openedTypes[i], destroy);
            }

            if (!destroy)
            {
                return;
            }

            foreach (KeyValuePair<Type, UIPanel> pair in _cachedPanels)
            {
                if (pair.Value != null)
                {
                    Destroy(pair.Value.gameObject);
                }
            }

            _cachedPanels.Clear();
            CancelAllPendingLoads();
        }

        public T Get<T>() where T : UIPanel
        {
            return _openedPanels.TryGetValue(typeof(T), out UIPanel panel) ? panel as T : null;
        }

        public bool IsOpen<T>() where T : UIPanel
        {
            return _openedPanels.ContainsKey(typeof(T));
        }

        public RectTransform GetLayerRoot(UILayer layer)
        {
            EnsureRoot();
            return _layerRoots.TryGetValue(layer, out RectTransform root) ? root : null;
        }

        private void LoadPanelPrefabAsync<T>(UIPanelAttribute config, int version)
            where T : UIPanel
        {
            if (config.AssetSource == UIAssetSource.Resources)
            {
                GameObject resourcesPrefab = Resources.Load<GameObject>(config.AssetName);
                CompletePanelLoad<T>(version, resourcesPrefab);
                return;
            }

            if (AssetManager.Instance == null)
            {
                Debug.LogError($"[UIManager] AssetManager 不可用 {typeof(T).Name}");
                CompletePanelLoad<T>(version, null);
                return;
            }

            AssetManager.Instance.LoadAssetHandleAsync<GameObject>(
                config.BundleName,
                config.AssetName,
                handle =>
                {
                    if (this == null)
                    {
                        handle?.Dispose();
                        return;
                    }

                    try
                    {
                        CompletePanelLoad<T>(version, handle?.Asset);
                    }
                    finally
                    {
                        handle?.Dispose();
                    }
                });
        }

        private void CompletePanelLoad<T>(int version, GameObject prefab)
            where T : UIPanel
        {
            Type panelType = typeof(T);
            if (!_pendingLoads.TryGetValue(panelType, out PendingPanelLoad pendingLoad)
                || pendingLoad.Version != version)
            {
                return;
            }

            if (prefab == null)
            {
                Debug.LogError($"[UIManager] 面板资源加载失败 {panelType.Name}");
                CompletePendingLoad(panelType, null);
                return;
            }

            GameObject panelObject = Instantiate(prefab);
            T panel = panelObject.GetComponent<T>();
            if (panel == null)
            {
                Debug.LogError($"[UIManager] 面板预制体缺少组件 {panelType.Name}", panelObject);
                Destroy(panelObject);
                CompletePendingLoad(panelType, null);
                return;
            }

            panelObject.name = panelType.Name;
            OpenPanelInstance(panelType, panel, pendingLoad.UserData);
            CompletePendingLoad(panelType, panel);
        }

        private void OpenPanelInstance(Type panelType, UIPanel panel, object userData)
        {
            UIPanelAttribute config = GetPanelConfig(panelType);
            RectTransform layerRoot = GetLayerRoot(config.Layer);
            panel.transform.SetParent(layerRoot, false);
            PreparePanelRect(panel, config.UseSafeArea);
            panel.Initialize();
            _openedPanels[panelType] = panel;

            if (config.Modal)
            {
                CreateModalMask(panelType, panel, config, layerRoot);
            }

            panel.Open(userData);
            BringToFront(panel);
        }

        private void PreparePanelRect(UIPanel panel, bool useSafeArea)
        {
            RectTransform panelRect = panel.RectTransform ?? panel.transform as RectTransform;
            if (panelRect == null)
            {
                return;
            }

            StretchRect(panelRect);
            SafeAreaAdapter adapter = panel.GetComponent<SafeAreaAdapter>();
            if (useSafeArea)
            {
                adapter ??= panel.gameObject.AddComponent<SafeAreaAdapter>();
                adapter.enabled = true;
                adapter.ApplyNow();
                return;
            }

            if (adapter != null)
            {
                adapter.enabled = false;
            }

            StretchRect(panelRect);
        }

        private void BringToFront(UIPanel panel)
        {
            if (panel == null)
            {
                return;
            }

            if (_modalMasks.TryGetValue(panel, out GameObject mask) && mask != null)
            {
                mask.transform.SetAsLastSibling();
            }

            panel.transform.SetAsLastSibling();
            panel.NotifyBroughtToFront();
        }

        private void CreateModalMask(
            Type panelType,
            UIPanel panel,
            UIPanelAttribute config,
            RectTransform layerRoot)
        {
            DestroyModalMask(panel);

            GameObject mask = new GameObject(
                $"{panelType.Name}_ModalMask",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            RectTransform maskRect = mask.transform as RectTransform;
            maskRect.SetParent(layerRoot, false);
            StretchRect(maskRect);

            Image image = mask.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, Mathf.Clamp01(config.MaskAlpha));
            image.raycastTarget = true;

            if (config.CloseOnMaskClick)
            {
                Button button = mask.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                button.targetGraphic = image;
                button.onClick.AddListener(() => Close(panelType));
            }

            int panelSiblingIndex = panel.transform.GetSiblingIndex();
            mask.transform.SetSiblingIndex(panelSiblingIndex);
            _modalMasks[panel] = mask;
        }

        private void DestroyModalMask(UIPanel panel)
        {
            if (panel == null || !_modalMasks.TryGetValue(panel, out GameObject mask))
            {
                return;
            }

            _modalMasks.Remove(panel);
            if (mask != null)
            {
                Destroy(mask);
            }
        }

        private void CompletePendingLoad(Type panelType, UIPanel panel)
        {
            if (!_pendingLoads.TryGetValue(panelType, out PendingPanelLoad pendingLoad))
            {
                return;
            }

            _pendingLoads.Remove(panelType);
            Action<UIPanel>[] callbacks = pendingLoad.Callbacks.ToArray();
            for (int i = 0; i < callbacks.Length; i++)
            {
                InvokeSafely(callbacks[i], panel);
            }
        }

        private void CancelPendingLoad(Type panelType)
        {
            if (!_pendingLoads.TryGetValue(panelType, out PendingPanelLoad pendingLoad))
            {
                return;
            }

            _pendingLoads.Remove(panelType);
            _loadVersions[panelType] = pendingLoad.Version + 1;
            Action<UIPanel>[] callbacks = pendingLoad.Callbacks.ToArray();
            for (int i = 0; i < callbacks.Length; i++)
            {
                InvokeSafely(callbacks[i], null);
            }
        }

        private void CancelAllPendingLoads()
        {
            Type[] pendingTypes = new Type[_pendingLoads.Count];
            _pendingLoads.Keys.CopyTo(pendingTypes, 0);
            for (int i = 0; i < pendingTypes.Length; i++)
            {
                CancelPendingLoad(pendingTypes[i]);
            }
        }

        private UIPanelAttribute GetPanelConfig(Type panelType)
        {
            if (_panelConfigs.TryGetValue(panelType, out UIPanelAttribute config))
            {
                return config;
            }

            config = panelType.GetCustomAttribute<UIPanelAttribute>(false)
                     ?? new UIPanelAttribute(panelType.Name);
            _panelConfigs.Add(panelType, config);
            return config;
        }

        private void EnsureRoot()
        {
            if (_uiRoot != null)
            {
                return;
            }

            GameObject rootObject = new GameObject(
                "UI_Root",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler));
            rootObject.transform.SetParent(transform, false);
            _uiRoot = rootObject.transform as RectTransform;
            StretchRect(_uiRoot);

            Canvas rootCanvas = rootObject.GetComponent<Canvas>();
            rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            rootCanvas.pixelPerfect = false;

            CanvasScaler scaler = rootObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100f;

            foreach (KeyValuePair<UILayer, int> pair in _layerOrders)
            {
                CreateLayer(pair.Key, pair.Value);
            }
        }

        private void CreateLayer(UILayer layer, int sortingOrder)
        {
            GameObject layerObject = new GameObject(
                $"{layer}Layer",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(GraphicRaycaster));
            RectTransform layerRect = layerObject.transform as RectTransform;
            layerRect.SetParent(_uiRoot, false);
            StretchRect(layerRect);

            Canvas canvas = layerObject.GetComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = sortingOrder;
            _layerRoots[layer] = layerRect;
        }

        private void EnsureEventSystem()
        {
            EventSystem[] eventSystems = FindObjectsOfType<EventSystem>(true);
            EventSystem primaryEventSystem = ResolvePrimaryEventSystem(eventSystems);
            if (primaryEventSystem == null)
            {
                GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
                primaryEventSystem = eventSystemObject.GetComponent<EventSystem>();
            }

            primaryEventSystem.gameObject.SetActive(true);
            primaryEventSystem.enabled = true;
            EnsureInputModule(primaryEventSystem.gameObject);
            EventSystem.current = primaryEventSystem;

            for (int i = 0; i < eventSystems.Length; i++)
            {
                EventSystem eventSystem = eventSystems[i];
                if (eventSystem == null || eventSystem == primaryEventSystem)
                {
                    continue;
                }

                eventSystem.enabled = false;
                BaseInputModule[] extraModules = eventSystem.GetComponents<BaseInputModule>();
                for (int moduleIndex = 0; moduleIndex < extraModules.Length; moduleIndex++)
                {
                    if (extraModules[moduleIndex] != null)
                    {
                        extraModules[moduleIndex].enabled = false;
                    }
                }
            }
        }

        private static EventSystem ResolvePrimaryEventSystem(EventSystem[] eventSystems)
        {
            if (EventSystem.current != null
                && EventSystem.current.enabled
                && EventSystem.current.gameObject.activeInHierarchy)
            {
                return EventSystem.current;
            }

            if (eventSystems == null || eventSystems.Length == 0)
            {
                return null;
            }

            for (int i = 0; i < eventSystems.Length; i++)
            {
                EventSystem eventSystem = eventSystems[i];
                if (eventSystem != null
                    && eventSystem.enabled
                    && eventSystem.gameObject.activeInHierarchy)
                {
                    return eventSystem;
                }
            }

            return eventSystems[0];
        }

        private static void EnsureInputModule(GameObject eventSystemObject)
        {
            BaseInputModule[] inputModules = eventSystemObject.GetComponents<BaseInputModule>();
            for (int i = 0; i < inputModules.Length; i++)
            {
                if (inputModules[i] != null && inputModules[i].enabled)
                {
                    return;
                }
            }

#if ENABLE_LEGACY_INPUT_MANAGER
            eventSystemObject.AddComponent<StandaloneInputModule>();
#else
            Debug.LogWarning("[UIManager] 当前输入模式需要自行添加 UI Input Module");
#endif
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureEventSystem();
        }

        private static void StretchRect(RectTransform rectTransform)
        {
            if (rectTransform == null)
            {
                return;
            }

            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.localScale = Vector3.one;
            rectTransform.localRotation = Quaternion.identity;
        }

        private static void InvokeSafely<T>(Action<T> callback, T value)
        {
            if (callback == null)
            {
                return;
            }

            try
            {
                callback.Invoke(value);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }
}
