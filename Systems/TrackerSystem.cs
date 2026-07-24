using System;
using System.Collections.Generic;
using Colossal.Serialization.Entities;
using Game;
using Game.Objects;
using Game.Prefabs;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace FlightTracker.Systems
{
    internal partial class TrackerSystem : GameSystemBase
    {
        private PrefabSystem _prefabSystem;
        private EntityQuery _aircraftQuery;

        private readonly List<TrackedFlight> _trackedFlights = new();
        private readonly Dictionary<Entity, AircraftHistory> _aircraftHistory = new();

        private uint _updateCounter;
        private bool _gameReady;

        public IReadOnlyList<TrackedFlight> TrackedFlights => _trackedFlights;

        private struct AircraftHistory
        {
            public float PreviousAltitude;
            public string LastStatus;
        }

        protected override void OnCreate()
        {
            base.OnCreate();

            _prefabSystem =
                World.GetOrCreateSystemManaged<PrefabSystem>();

            _aircraftQuery = GetEntityQuery(
                ComponentType.ReadOnly<PrefabRef>(),
                ComponentType.ReadOnly<Transform>()
            );

            /*
             * Do not use RequireForUpdate here.
             *
             * The system can safely remain active while no aircraft exist.
             * OnUpdate will simply return when the query is empty.
             */
            _gameReady = false;
        }

        protected override void OnGamePreload(
    Purpose purpose,
    GameMode mode)
        {
            base.OnGamePreload(purpose, mode);

            _gameReady = false;
            _updateCounter = 0;

            _trackedFlights.Clear();
            _aircraftHistory.Clear();
        }

        protected override void OnGameLoadingComplete(
    Purpose purpose,
    GameMode mode)
        {
            base.OnGameLoadingComplete(purpose, mode);

            _gameReady =
                mode == GameMode.Game ||
                mode == GameMode.Editor;
        }

        protected override void OnUpdate()
        {
            if (!_gameReady)
                return;

            if (_aircraftQuery.IsEmptyIgnoreFilter)
            {
                _trackedFlights.Clear();
                _aircraftHistory.Clear();
                return;
            }

            _updateCounter++;

            if (_updateCounter % 30 != 0)
                return;

            UpdateAircraftList();
        }

        private void UpdateAircraftList()
        {
            _trackedFlights.Clear();

            using NativeArray<Entity> entities =
                _aircraftQuery.ToEntityArray(Allocator.Temp);

            using NativeArray<PrefabRef> prefabRefs =
                _aircraftQuery.ToComponentDataArray<PrefabRef>(
                    Allocator.Temp
                );

            using NativeArray<Transform> transforms =
                _aircraftQuery.ToComponentDataArray<Transform>(
                    Allocator.Temp
                );

            HashSet<Entity> currentAircraft = new();

            for (int i = 0; i < entities.Length; i++)
            {
                Entity entity = entities[i];
                PrefabRef prefabRef = prefabRefs[i];
                Transform transform = transforms[i];

                if (!TryGetAirplanePrefab(
                        prefabRef.m_Prefab,
                        out AirplanePrefab airplanePrefab))
                {
                    continue;
                }

                currentAircraft.Add(entity);

                float3 position = transform.m_Position;
                float speed = GetAircraftSpeed(entity);

                string status = DetermineStatus(
                    entity,
                    position.y,
                    speed
                );

                _trackedFlights.Add(
                    new TrackedFlight
                    {
                        EntityIndex = entity.Index,
                        EntityVersion = entity.Version,

                        Name = GetAircraftName(airplanePrefab),
                        Status = status,

                        X = position.x,
                        Y = position.y,
                        Z = position.z,

                        Altitude = position.y,
                        Speed = speed
                    }
                );
            }

            RemoveDestroyedAircraft(currentAircraft);

            _trackedFlights.Sort(
                (a, b) => string.Compare(
                    a.Name,
                    b.Name,
                    StringComparison.OrdinalIgnoreCase
                )
            );
        }

        private bool TryGetAirplanePrefab(
            Entity prefabEntity,
            out AirplanePrefab airplanePrefab)
        {
            airplanePrefab = null;

            if (prefabEntity == Entity.Null)
                return false;

            if (!_prefabSystem.TryGetPrefab(
                    prefabEntity,
                    out PrefabBase prefab))
            {
                return false;
            }

            airplanePrefab = prefab as AirplanePrefab;

            return airplanePrefab != null;
        }

        private static string GetAircraftName(
            AirplanePrefab airplanePrefab)
        {
            if (airplanePrefab == null)
                return "Unknown Aircraft";

            return string.IsNullOrWhiteSpace(airplanePrefab.name)
                ? "Unnamed Aircraft"
                : airplanePrefab.name;
        }

        private float GetAircraftSpeed(Entity entity)
        {
            if (!EntityManager.HasComponent<Moving>(entity))
                return 0f;

            Moving moving =
                EntityManager.GetComponentData<Moving>(entity);

            return math.length(moving.m_Velocity);
        }

        private string DetermineStatus(
            Entity entity,
            float altitude,
            float speed)
        {
            const float stoppedSpeed = 0.5f;
            const float taxiSpeed = 18f;
            const float airborneAltitude = 35f;
            const float altitudeTolerance = 0.25f;

            if (!_aircraftHistory.TryGetValue(
                    entity,
                    out AircraftHistory history))
            {
                history = new AircraftHistory
                {
                    PreviousAltitude = altitude,

                    LastStatus = altitude > airborneAltitude
                        ? "Airborne"
                        : "Landed"
                };
            }

            float altitudeChange =
                altitude - history.PreviousAltitude;

            string status;

            if (altitude > airborneAltitude)
            {
                if (altitudeChange > altitudeTolerance)
                {
                    status = "Departed";
                }
                else if (altitudeChange < -altitudeTolerance)
                {
                    status = "Arriving";
                }
                else
                {
                    status = "Airborne";
                }
            }
            else if (speed <= stoppedSpeed)
            {
                status =
                    history.LastStatus is "Arriving" or "Airborne"
                        ? "Landed"
                        : "At Gate";
            }
            else if (speed <= taxiSpeed)
            {
                status =
                    history.LastStatus is "Landed" or "Arriving"
                        ? "Taxiing to Gate"
                        : "Taxiing for Departure";
            }
            else
            {
                status = altitudeChange > altitudeTolerance
                    ? "Taking Off"
                    : "Landing";
            }

            _aircraftHistory[entity] = new AircraftHistory
            {
                PreviousAltitude = altitude,
                LastStatus = status
            };

            return status;
        }

        private void RemoveDestroyedAircraft(
            HashSet<Entity> currentAircraft)
        {
            if (_aircraftHistory.Count == 0)
                return;

            List<Entity> removedEntities = null;

            foreach (Entity trackedEntity in _aircraftHistory.Keys)
            {
                if (currentAircraft.Contains(trackedEntity))
                    continue;

                removedEntities ??= new List<Entity>();
                removedEntities.Add(trackedEntity);
            }

            if (removedEntities == null)
                return;

            foreach (Entity removedEntity in removedEntities)
            {
                _aircraftHistory.Remove(removedEntity);
            }
        }

        protected override void OnDestroy()
        {
            _gameReady = false;

            _trackedFlights.Clear();
            _aircraftHistory.Clear();

            base.OnDestroy();
        }
    }
}