using System;
using System.Collections;
using UnityEngine;
using VTOLAPI;

namespace SoakHarness
{
    internal sealed class SoakController : MonoBehaviour
    {
        private enum Phase
        {
            Boot,
            ReadyRoom,
            VehicleConfig,
            Briefing,
            Flight,
            Done
        }

        private Phase _phase = Phase.Boot;
        private System.Random _rng;
        private int _seed;
        private float _phaseEntered;
        private float _deadline;
        private bool _busy;
        private string _lastDetail = "boot";

        private const float ReadyRoomTimeout = 120f;
        private const float ConfigTimeout = 90f;
        private const float BriefingTimeout = 90f;
        private const float FlightTimeout = 180f;
        private const float TotalTimeout = 420f;

        public void Begin(int seed)
        {
            _seed = seed;
            _rng = new System.Random(seed);
            _deadline = Time.realtimeSinceStartup + TotalTimeout;
            _phase = Phase.Boot;
            Enter(Phase.Boot, "waiting for scenes");
            DontDestroyOnLoad(gameObject);
            StartCoroutine(MainLoop());
        }

        private void Enter(Phase phase, string detail)
        {
            _phase = phase;
            _phaseEntered = Time.realtimeSinceStartup;
            _lastDetail = detail;
            Debug.Log("[SoakHarness] phase=" + phase + " " + detail);
        }

        private IEnumerator MainLoop()
        {
            // Give Mod Loader / VTOLAPI a moment after Awake.
            yield return new WaitForSecondsRealtime(2f);

            while (_phase != Phase.Done)
            {
                if (Time.realtimeSinceStartup > _deadline)
                {
                    Fail("timeout", "global timeout at " + _phase + ": " + _lastDetail);
                    yield break;
                }

                var scene = VTAPI.currentScene;
                try
                {
                    if (!_busy)
                    {
                        if (scene == VTScenes.ReadyRoom && _phase <= Phase.ReadyRoom)
                        {
                            if (_phase != Phase.ReadyRoom)
                                Enter(Phase.ReadyRoom, "ready room");
                            if (Time.realtimeSinceStartup - _phaseEntered > ReadyRoomTimeout)
                            {
                                Fail("timeout", "ready room");
                                yield break;
                            }
                            StartCoroutine(DriveReadyRoom());
                        }
                        else if (scene == VTScenes.VehicleConfiguration && _phase <= Phase.VehicleConfig)
                        {
                            if (_phase != Phase.VehicleConfig)
                                Enter(Phase.VehicleConfig, "vehicle config");
                            if (Time.realtimeSinceStartup - _phaseEntered > ConfigTimeout)
                            {
                                Fail("timeout", "vehicle config");
                                yield break;
                            }
                            StartCoroutine(DriveVehicleConfig());
                        }
                        else if (IsFlightScene(scene) && _phase <= Phase.Flight)
                        {
                            if (_phase != Phase.Flight)
                                Enter(Phase.Flight, "flight scene " + scene);
                            if (Time.realtimeSinceStartup - _phaseEntered > FlightTimeout)
                            {
                                Fail("timeout", "flight ready");
                                yield break;
                            }
                            StartCoroutine(WatchFlightReady());
                        }
                        else if (_phase == Phase.Briefing || scene == VTScenes.LoadingScene)
                        {
                            // Loading / transitional — also try briefing UI if present in any scene.
                            StartCoroutine(TryBriefingFly());
                        }
                        else
                        {
                            // Briefing UI can appear outside the VTScenes enum mapping we care about.
                            StartCoroutine(TryBriefingFly());
                        }
                    }
                }
                catch (Exception ex)
                {
                    Fail("error", ex.GetType().Name + ": " + ex.Message);
                    yield break;
                }

                yield return new WaitForSecondsRealtime(0.5f);
            }
        }

        private static bool IsFlightScene(VTScenes scene)
        {
            return scene == VTScenes.OpenWater
                || scene == VTScenes.Akutan
                || scene == VTScenes.MeshTerrain
                || scene == VTScenes.CustomMapBase
                || scene == VTScenes.CustomMapBase_OverCloud;
        }

        private IEnumerator DriveReadyRoom()
        {
            if (_busy) yield break;
            _busy = true;
            try
            {
                // Ensure a pilot is selected / on main vehicle screen.
                var pilotUi = FindObjectOfType<PilotSelectUI>();
                if (pilotUi != null)
                {
                    try { pilotUi.StartSelectedPilotButton(); }
                    catch { /* may already be past pilot select */ }
                    yield return new WaitForSecondsRealtime(1.5f);

                    // Random vehicle: mash NextVehicle a few times then select.
                    var hops = _rng.Next(0, 8);
                    for (var i = 0; i < hops; i++)
                    {
                        try { pilotUi.NextVehicleButton(); }
                        catch { break; }
                        yield return new WaitForSecondsRealtime(0.35f);
                    }

                    try { pilotUi.SelectVehicleButton(); }
                    catch { /* ignore */ }
                    yield return new WaitForSecondsRealtime(1.5f);
                }

                var campaign = FindObjectOfType<CampaignSelectorUI>();
                if (campaign == null)
                {
                    _lastDetail = "no CampaignSelectorUI yet";
                    yield break;
                }

                try { campaign.MissionsButton(); }
                catch { /* already on missions */ }
                yield return new WaitForSecondsRealtime(1f);

                try { campaign.OpenCampaignSelector(null); }
                catch { /* may already be open */ }
                yield return new WaitForSecondsRealtime(2f);

                // Advance campaigns a bit for variety.
                var campHops = _rng.Next(0, 4);
                for (var i = 0; i < campHops; i++)
                {
                    try { campaign.NextCampaign(); }
                    catch { break; }
                    yield return new WaitForSecondsRealtime(0.4f);
                }

                try { campaign.SelectCampaign(); }
                catch { /* ignore */ }
                yield return new WaitForSecondsRealtime(1.5f);

                var missionHops = _rng.Next(0, 6);
                for (var i = 0; i < missionHops; i++)
                {
                    try { campaign.NextMission(); }
                    catch { break; }
                    yield return new WaitForSecondsRealtime(0.35f);
                }
                yield return new WaitForSecondsRealtime(0.5f);

                _lastDetail = "StartMission hops=" + missionHops;
                try { campaign.StartMission(); }
                catch (Exception ex)
                {
                    Fail("error", "StartMission: " + ex.Message);
                    yield break;
                }

                Enter(Phase.VehicleConfig, "started mission, awaiting config/briefing");
            }
            finally
            {
                _busy = false;
            }
        }

        private IEnumerator DriveVehicleConfig()
        {
            if (_busy) yield break;
            _busy = true;
            try
            {
                yield return new WaitForSecondsRealtime(2f);

                var loadout = FindObjectOfType<LoadoutConfigurator>();
                if (loadout != null)
                {
                    try
                    {
                        if (_rng.NextDouble() < 0.7)
                            loadout.LoadRecommended();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning("[SoakHarness] LoadRecommended: " + ex.Message);
                    }
                    yield return new WaitForSecondsRealtime(1f);
                }

                var setup = FindObjectOfType<VehicleConfigSceneSetup>();
                if (setup == null)
                {
                    _lastDetail = "waiting VehicleConfigSceneSetup";
                    yield break;
                }

                _lastDetail = "LaunchMission";
                try { setup.LaunchMission(); }
                catch (Exception ex)
                {
                    Fail("error", "LaunchMission: " + ex.Message);
                    yield break;
                }

                Enter(Phase.Briefing, "launched from vehicle config");
            }
            finally
            {
                _busy = false;
            }
        }

        private IEnumerator TryBriefingFly()
        {
            if (_busy) yield break;
            var briefing = FindObjectOfType<MissionBriefingUI>();
            if (briefing == null)
                yield break;

            _busy = true;
            try
            {
                if (_phase < Phase.Briefing)
                    Enter(Phase.Briefing, "briefing UI found");

                if (Time.realtimeSinceStartup - _phaseEntered > BriefingTimeout)
                {
                    Fail("timeout", "briefing");
                    yield break;
                }

                // Briefing intro animations — wait a bit then mash Fly.
                yield return new WaitForSecondsRealtime(3f);
                _lastDetail = "FlyButton";
                try { briefing.FlyButton(); }
                catch (Exception ex)
                {
                    Debug.LogWarning("[SoakHarness] FlyButton: " + ex.Message);
                }
            }
            finally
            {
                _busy = false;
            }
        }

        private IEnumerator WatchFlightReady()
        {
            if (_busy) yield break;
            _busy = true;
            try
            {
                for (var i = 0; i < 40; i++)
                {
                    if (IsCockpitReady(out var detail))
                    {
                        Pass(detail);
                        yield break;
                    }
                    _lastDetail = detail;
                    yield return new WaitForSecondsRealtime(0.5f);
                }
            }
            finally
            {
                _busy = false;
            }
        }

        private static bool IsCockpitReady(out string detail)
        {
            detail = "checking";
            try
            {
                var ready = FlightSceneManager.isFlightReady;
                var vehicle = VTAPI.GetPlayersVehicleGameObject();
                if (ready && vehicle != null)
                {
                    detail = "isFlightReady vehicle=" + vehicle.name;
                    return true;
                }

                if (ready)
                {
                    detail = "isFlightReady but no player vehicle GO yet";
                    return false;
                }

                if (vehicle != null)
                {
                    detail = "player vehicle present name=" + vehicle.name + " readyFlag=false";
                    return false;
                }

                detail = "no player vehicle yet";
                return false;
            }
            catch (Exception ex)
            {
                detail = "check error: " + ex.Message;
                return false;
            }
        }

        private void Pass(string detail)
        {
            if (_phase == Phase.Done) return;
            Enter(Phase.Done, detail);
            ResultWriter.Write("pass", _phase.ToString(), detail, _seed);
        }

        private void Fail(string status, string detail)
        {
            if (_phase == Phase.Done) return;
            var stage = _phase.ToString();
            Enter(Phase.Done, detail);
            ResultWriter.Write(status, stage, detail, _seed);
        }

        private void OnDestroy()
        {
            if (_phase != Phase.Done)
                ResultWriter.Write("fail", _phase.ToString(), "controller destroyed: " + _lastDetail, _seed);
        }
    }
}
