using System;
using ModLoader.Framework;
using ModLoader.Framework.Attributes;
using UnityEngine;
using VTOLAPI;

namespace SoakHarness
{
    [ItemId("eywuddup.vtolvr-connect.soak")]
    public class Main : VtolMod
    {
        private GameObject _host;
        private SoakController _controller;
        private bool _hooked;

        public void Awake()
        {
            Debug.Log("[SoakHarness] Awake");
            try
            {
                if (!_hooked)
                {
                    VTAPI.SceneLoaded += OnSceneLoaded;
                    _hooked = true;
                }

                EnsureController();
            }
            catch (Exception ex)
            {
                Debug.LogError("[SoakHarness] Awake failed: " + ex);
                ResultWriter.Write("error", "Awake", ex.Message, 0);
            }
        }

        public override void UnLoad()
        {
            Debug.Log("[SoakHarness] UnLoad");
            if (_hooked)
            {
                try { VTAPI.SceneLoaded -= OnSceneLoaded; }
                catch { /* ignore */ }
                _hooked = false;
            }

            if (_host != null)
            {
                Destroy(_host);
                _host = null;
                _controller = null;
            }
        }

        private void OnSceneLoaded(VTScenes scene)
        {
            Debug.Log("[SoakHarness] SceneLoaded " + scene);
            EnsureController();
        }

        private void EnsureController()
        {
            if (_controller != null)
                return;

            _host = new GameObject("SoakHarnessHost");
            DontDestroyOnLoad(_host);
            _controller = _host.AddComponent<SoakController>();

            var seedEnv = Environment.GetEnvironmentVariable("VTOLVR_CONNECT_SEED");
            int seed;
            if (string.IsNullOrEmpty(seedEnv) || !int.TryParse(seedEnv, out seed))
                seed = Environment.TickCount;

            _controller.Begin(seed);
        }
    }
}
