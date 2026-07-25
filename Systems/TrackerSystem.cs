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
        private bool _loggedQueryState;

        public IReadOnlyList<TrackedFlight> TrackedFlights =>
            _trackedFlights;

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

            _updateCounter = 29;

            Mod.Log.Info(
                "[FlightTracker] TrackerSystem created."
            );
        }

        protected override void OnGamePreload(
            Purpose purpose,
            GameMode mode)
        {
            base.OnGamePreload(purpose, mode);

            _trackedFlights.Clear();
            _aircraftHistory.Clear();

            _updateCounter = 29;
            _loggedQueryState = false;

            Mod.Log.Info(
                $"[FlightTracker] Game preload. Purpose={purpose}, Mode={mode}"
            );
        }

        protected override void OnGameLoadingComplete(
            Purpose purpose,
            GameMode mode)
        {
            base.OnGameLoadingComplete(purpose, mode);

            _updateCounter = 29;
            _loggedQueryState = false;

            Mod.Log.Info(
                $"[FlightTracker] Game loading complete. Purpose={purpose}, Mode={mode}"
            );
        }

        protected override void OnUpdate()
        {
            _updateCounter++;

            if (_updateCounter < 30)
                return;

            _updateCounter = 0;

            try
            {
                UpdateAircraftList();
            }
            catch (Exception exception)
            {
                Mod.Log.Error(
                    $"[FlightTracker] Aircraft update failed: {exception}"
                );
            }
        }

        private void UpdateAircraftList()
        {
            _trackedFlights.Clear();

            int queryCount =
                _aircraftQuery.CalculateEntityCount();

            if (!_loggedQueryState)
            {
                Mod.Log.Info(
                    $"[FlightTracker] PrefabRef + Transform entities: {queryCount}"
                );

                _loggedQueryState = true;
            }

            if (queryCount == 0)
            {
                _aircraftHistory.Clear();
                return;
            }

            using NativeArray<Entity> entities =
                _aircraftQuery.ToEntityArray(
                    Allocator.Temp
                );

            using NativeArray<PrefabRef> prefabRefs =
                _aircraftQuery.ToComponentDataArray<PrefabRef>(
                    Allocator.Temp
                );

            using NativeArray<Transform> transforms =
                _aircraftQuery.ToComponentDataArray<Transform>(
                    Allocator.Temp
                );

            HashSet<Entity> currentAircraft = new();

            int validPrefabCount = 0;
            int airplaneCount = 0;

            for (int i = 0; i < entities.Length; i++)
            {
                Entity entity = entities[i];
                PrefabRef prefabRef = prefabRefs[i];

                if (prefabRef.m_Prefab == Entity.Null)
                    continue;

                if (!_prefabSystem.TryGetPrefab(
                        prefabRef.m_Prefab,
                        out PrefabBase prefab))
                {
                    continue;
                }

                validPrefabCount++;

                if (prefab is not AirplanePrefab airplanePrefab)
                    continue;

                airplaneCount++;
                currentAircraft.Add(entity);

                Transform transform = transforms[i];
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

                        Name = GetAircraftName(
                            airplanePrefab
                        ),

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

            Mod.Log.Info(
                $"[FlightTracker] Query={entities.Length}, " +
                $"valid prefabs={validPrefabCount}, " +
                $"airplanes={airplaneCount}, " +
                $"tracked={_trackedFlights.Count}"
            );

            if (
                airplaneCount == 0 &&
                validPrefabCount > 0
            )
            {
                LogPrefabTypes(
                    prefabRefs,
                    20
                );
            }
        }

        private void LogPrefabTypes(
            NativeArray<PrefabRef> prefabRefs,
            int maximum)
        {
            HashSet<string> loggedTypes = new();

            for (
                int i = 0;
                i < prefabRefs.Length &&
                loggedTypes.Count < maximum;
                i++)
            {
                Entity prefabEntity =
                    prefabRefs[i].m_Prefab;

                if (prefabEntity == Entity.Null)
                    continue;

                if (!_prefabSystem.TryGetPrefab(
                        prefabEntity,
                        out PrefabBase prefab))
                {
                    continue;
                }

                string prefabType =
                    prefab?.GetType().FullName ??
                    "null";

                if (!loggedTypes.Add(prefabType))
                    continue;

                Mod.Log.Info(
                    $"[FlightTracker] Observed prefab type: {prefabType}"
                );
            }
        }

        private static string GetAircraftName(
            AirplanePrefab airplanePrefab)
        {
            if (airplanePrefab == null)
                return "Unknown Aircraft";

            return string.IsNullOrWhiteSpace(
                airplanePrefab.name
            )
                ? "Unnamed Aircraft"
                : airplanePrefab.name;
        }

        private float GetAircraftSpeed(
            Entity entity)
        {
            if (!EntityManager.HasComponent<Moving>(
                    entity
                ))
            {
                return 0f;
            }

            Moving moving =
                EntityManager.GetComponentData<Moving>(
                    entity
                );

            return math.length(
                moving.m_Velocity
            );
        }

        private string DetermineStatus(
    Entity entity,
    float altitude,
    float speed)
        {
            const float stoppedSpeed = 0.5f;
            const float minimalMovementSpeed = 5f;
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
                    LastStatus = "At Gate"
                };
            }

            float altitudeChange =
                altitude - history.PreviousAltitude;

            bool hasNoMovement =
                speed <= stoppedSpeed;

            bool hasMinimalMovement =
                speed > stoppedSpeed &&
                speed <= minimalMovementSpeed;

            bool hasNoAltitudeChange =
                math.abs(altitudeChange) <= altitudeTolerance;

            string status;

            // Do not change this: fully stopped aircraft remain At Gate.
            if (hasNoMovement && hasNoAltitudeChange)
            {
                status = "At Gate";
            }
            // Small movement without meaningful altitude change means taxiing.
            else if (hasMinimalMovement && hasNoAltitudeChange)
            {
                status =
                    history.LastStatus is
                        "Arriving" or
                        "Landed" or
                        "Taxiing to Gate"
                        ? "Taxiing to Gate"
                        : "Taxiing for Departure";
            }
            else if (altitude > airborneAltitude)
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
            else if (speed <= taxiSpeed)
            {
                status =
                    history.LastStatus is
                        "Arriving" or
                        "Landed" or
                        "Taxiing to Gate"
                        ? "Taxiing to Gate"
                        : "Taxiing for Departure";
            }
            else
            {
                status =
                    altitudeChange > altitudeTolerance
                        ? "Taking Off"
                        : "Landing";
            }

            _aircraftHistory[entity] =
                new AircraftHistory
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

            foreach (
                Entity trackedEntity in
                _aircraftHistory.Keys
            )
            {
                if (
                    currentAircraft.Contains(
                        trackedEntity
                    )
                )
                {
                    continue;
                }

                removedEntities ??=
                    new List<Entity>();

                removedEntities.Add(
                    trackedEntity
                );
            }

            if (removedEntities == null)
                return;

            foreach (
                Entity removedEntity in
                removedEntities
            )
            {
                _aircraftHistory.Remove(
                    removedEntity
                );
            }
        }

        protected override void OnDestroy()
        {
            _trackedFlights.Clear();
            _aircraftHistory.Clear();

            base.OnDestroy();
        }
    }
}