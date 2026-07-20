using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sheng.GameFramework.Pooling
{
    /// <summary>
    /// 记录 GameObject 的对象池归属和租用状态
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PooledObject : MonoBehaviour
    {
        private readonly List<IPoolable> _poolables = new List<IPoolable>();

        private PoolManager _owner;
        private Rigidbody[] _rigidbodies = Array.Empty<Rigidbody>();
        private Rigidbody2D[] _rigidbodies2D = Array.Empty<Rigidbody2D>();
        private ParticleSystem[] _particleSystems = Array.Empty<ParticleSystem>();
        private TrailRenderer[] _trailRenderers = Array.Empty<TrailRenderer>();
        private Vector3 _originalLocalScale = Vector3.one;
        private bool _isDestroying;

        public PoolKey PoolKey { get; private set; }
        public bool IsRented { get; private set; }
        public int RentVersion { get; private set; }

        public bool ReturnToPool()
        {
            return _owner != null && _owner.Return(gameObject);
        }

        public void RefreshCallbacks()
        {
            _poolables.Clear();
            MonoBehaviour[] behaviours = GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IPoolable poolable)
                {
                    _poolables.Add(poolable);
                }
            }

            _rigidbodies = GetComponentsInChildren<Rigidbody>(true);
            _rigidbodies2D = GetComponentsInChildren<Rigidbody2D>(true);
            _particleSystems = GetComponentsInChildren<ParticleSystem>(true);
            _trailRenderers = GetComponentsInChildren<TrailRenderer>(true);
        }

        internal void Initialize(PoolManager owner, PoolKey poolKey)
        {
            _owner = owner;
            PoolKey = poolKey;
            IsRented = false;
            RentVersion = 0;
            _isDestroying = false;
            _originalLocalScale = transform.localScale;
            RefreshCallbacks();
            InvokePoolables(poolable => poolable.OnPoolCreated());
        }

        internal void Activate(
            Transform parent,
            Vector3 position,
            Quaternion rotation,
            GameObjectPoolOptions options)
        {
            if (_isDestroying)
            {
                return;
            }

            if (options.ResetTransform)
            {
                transform.SetParent(parent, true);
                transform.SetPositionAndRotation(position, rotation);
                transform.localScale = _originalLocalScale;
            }
            else
            {
                transform.SetParent(parent, true);
            }

            if (options.ResetPhysics)
            {
                ResetPhysics();
            }

            IsRented = true;
            RentVersion++;
            gameObject.SetActive(true);
            InvokePoolables(poolable => poolable.OnRentFromPool());
        }

        internal void Store(Transform poolRoot, GameObjectPoolOptions options)
        {
            if (_isDestroying)
            {
                return;
            }

            InvokePoolables(poolable => poolable.OnReturnToPool());
            if (options.StopEffectsOnReturn)
            {
                StopEffects();
            }

            if (options.ResetPhysics)
            {
                ResetPhysics();
            }

            IsRented = false;
            gameObject.SetActive(false);
            transform.SetParent(poolRoot, false);
            if (options.ResetTransform)
            {
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
                transform.localScale = _originalLocalScale;
            }
        }

        internal void DestroyManaged()
        {
            if (_isDestroying)
            {
                return;
            }

            _isDestroying = true;
            IsRented = false;
            InvokePoolables(poolable => poolable.OnPoolDestroyed());
            _owner = null;
            if (Application.isPlaying)
            {
                Destroy(gameObject);
            }
            else
            {
                DestroyImmediate(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (!_isDestroying && _owner != null)
            {
                PoolManager owner = _owner;
                _owner = null;
                owner.NotifyInstanceDestroyed(this);
            }
        }

        private void ResetPhysics()
        {
            for (int i = 0; i < _rigidbodies.Length; i++)
            {
                Rigidbody body = _rigidbodies[i];
                if (body == null)
                {
                    continue;
                }

                body.velocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            for (int i = 0; i < _rigidbodies2D.Length; i++)
            {
                Rigidbody2D body = _rigidbodies2D[i];
                if (body == null)
                {
                    continue;
                }

                body.velocity = Vector2.zero;
                body.angularVelocity = 0f;
            }
        }

        private void StopEffects()
        {
            for (int i = 0; i < _particleSystems.Length; i++)
            {
                if (_particleSystems[i] != null)
                {
                    _particleSystems[i].Stop(
                        true,
                        ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }

            for (int i = 0; i < _trailRenderers.Length; i++)
            {
                _trailRenderers[i]?.Clear();
            }
        }

        private void InvokePoolables(Action<IPoolable> callback)
        {
            for (int i = 0; i < _poolables.Count; i++)
            {
                try
                {
                    callback.Invoke(_poolables[i]);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }
            }
        }
    }
}
