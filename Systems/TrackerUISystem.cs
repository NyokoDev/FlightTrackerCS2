using Colossal.UI.Binding;
using Game;
using Game.Rendering;
using Game.UI;
using System;
using System.Globalization;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace FlightTracker.Systems
{
    internal partial class TrackerUISystem : ExtendedUISystemBase
    {
        public const string MOD_UI = "FlightTracker";

        private bool _followingAircraft;
        private int _followEntityIndex = -1;
        private int _followEntityVersion = -1;

        private string _selectedFlight = string.Empty;

        private float3 _smoothedFollowPosition;
        private bool _hasSmoothedFollowPosition;


        protected override void OnUpdate()
        {
            base.OnUpdate();
            ConstantFollow();
        }

        /// <summary>
        /// Follows the currently selected aircraft, if any, by updating the camera pivot to match the aircraft's position.
        /// </summary>
        private void ConstantFollow()
        {
            if (!_followingAircraft)
                return;

            TrackerSystem tracker = GetTrackerSystem();

            if (tracker == null || tracker.TrackedFlights == null)
                return;

            foreach (TrackedFlight flight in tracker.TrackedFlights)
            {
                if (flight.EntityIndex != _followEntityIndex ||
                    flight.EntityVersion != _followEntityVersion)
                {
                    continue;
                }

                if (!CameraController.TryGet(out CameraController controller))
                    return;

                float3 targetPosition = new float3(
    flight.X,
    flight.Y,
    flight.Z
);

                if (!_hasSmoothedFollowPosition)
                {
                    _smoothedFollowPosition = targetPosition;
                    _hasSmoothedFollowPosition = true;
                }
                else
                {
                    float smoothingSpeed = 4f;

                    _smoothedFollowPosition = math.lerp(
                        _smoothedFollowPosition,
                        targetPosition,
                        1f - math.exp(-smoothingSpeed * UnityEngine.Time.deltaTime)
                    );
                }

                const float PivotHeightOffset = 50f;

                controller.pivot = new float3(
                    _smoothedFollowPosition.x,
                    flight.Y + PivotHeightOffset,
                    _smoothedFollowPosition.z
                );

                return;
            }

            // The aircraft disappeared from the tracked list.
            _followingAircraft = false;
            _followEntityIndex = -1;
            _followEntityVersion = -1;
            _selectedFlight = string.Empty;
        }

        protected override void OnCreate()
        {
            base.OnCreate();

            Mod.Log.Info("[FlightTracker] TrackerUISystem created.");


            UIBindings();


            RadarBindings();

            AddUpdateBinding(
    new GetterValueBinding<string>(
        MOD_UI,
        "SelectedFlight",
        GetSelectedFlight
    )
);


            try
            {
                Mod.Log.Info(
                    "[FlightTracker] Registering TrackedFlights binding..."
                );

                AddBinding(
    new TriggerBinding<string>(
        MOD_UI,
        "FocusAircraft",
        FocusAircraft
    )
);

                AddUpdateBinding(
                    new GetterValueBinding<string[]>(
                        MOD_UI,
                        "TrackedFlights",
                        GetTrackedFlights,
                        new ArrayWriter<string>(
                            new StringWriter()
                        )
                    )
                );

                Mod.Log.Info(
                    "[FlightTracker] TrackedFlights binding registered."
                );

                Mod.Log.Info(
                    "[FlightTracker] Registering TrackedFlightCount binding..."
                );

                AddUpdateBinding(
                    new GetterValueBinding<int>(
                        MOD_UI,
                        "TrackedFlightCount",
                        GetTrackedFlightCount
                    )
                );

                Mod.Log.Info(
                    "[FlightTracker] TrackedFlightCount binding registered."
                );

                Mod.Log.Info(
                    "[FlightTracker] All UI bindings registered."
                );
            }
            catch (Exception exception)
            {
                Mod.Log.Error(
                    $"[FlightTracker] Failed to register UI bindings: {exception}"
                );

                throw;
            }
        }

        private void UIBindings()
        {
            AddUpdateBinding(
                new GetterValueBinding<bool>(
                    MOD_UI,
                    "UIEnabled",
                    () => TrackerSystem.UIEnabled
                )
            );

            AddBinding(
                new TriggerBinding(
                    MOD_UI,
                    "ToggleUIEnabled",
                    ToggleUIEnabled
                )
            );

            AddBinding(
               new TriggerBinding(
                   MOD_UI,
                   "ToggleUIFalse",
                   ToggleUIFalse
               )
           );
        
        }

        private void ToggleUIFalse()
        {
            TrackerSystem.UIEnabled = false;
        }

        private void ToggleUIEnabled()
        {
            TrackerSystem.UIEnabled = !TrackerSystem.UIEnabled;
        }

        private void RadarBindings()
        {
            AddUpdateBinding(
        new GetterValueBinding<string[]>(
            MOD_UI,
            "RadarFlights",
            GetRadarFlights,
            new ArrayWriter<string>(
                new StringWriter()
            )
        )
    );
        }

        private string[] GetRadarFlights()
        {
            TrackerSystem tracker = GetTrackerSystem();

            if (tracker?.TrackedFlights == null)
                return Array.Empty<string>(); 
                     

            string[] result = new string[tracker.TrackedFlights.Count];

            for (int i = 0; i < tracker.TrackedFlights.Count; i++)
            {
                TrackedFlight flight = tracker.TrackedFlights[i];

                float x = (i % 5) * 250f - 500f;
                float z = (i / 5) * 250f - 500f;

                result[i] = string.Join(
                    "|",
                    i.ToString(CultureInfo.InvariantCulture),
                    flight.Name ?? "Unknown Aircraft",
                    flight.Altitude.ToString(CultureInfo.InvariantCulture),
                    flight.Speed.ToString(CultureInfo.InvariantCulture),
                    flight.Status ?? "Unknown",
                    x.ToString(CultureInfo.InvariantCulture),
                    z.ToString(CultureInfo.InvariantCulture)
                );
            }

            return result;
        }

        private void FocusAircraft(string data)
        {
            if (string.IsNullOrWhiteSpace(data))
                return;

            string[] parts = data.Split('|');

            if (parts.Length != 2)
                return;

            if (!int.TryParse(parts[0], out int index))
                return;

            if (!int.TryParse(parts[1], out int version))
                return;

            if (_followingAircraft &&
    _followEntityIndex == index &&
    _followEntityVersion == version)
            {
                _followingAircraft = false;
                _followEntityIndex = -1;
                _followEntityVersion = -1;

                _selectedFlight = string.Empty;

                Mod.Log.Info("Stopped following aircraft.");
                return;
            }

            TrackerSystem tracker = GetTrackerSystem();

            if (tracker == null)
                return;

            TrackedFlight flight = default;
            bool found = false;

            foreach (TrackedFlight trackedFlight in tracker.TrackedFlights)
            {
                if (trackedFlight.EntityIndex == index &&
                    trackedFlight.EntityVersion == version)
                {
                    flight = trackedFlight;
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                Mod.Log.Warn($"Unable to locate tracked flight {index}:{version}");
                return;
            }

            _followEntityIndex = index;
            _followEntityVersion = version;
            _followingAircraft = true;
            _hasSmoothedFollowPosition = false;

            _selectedFlight = $"{index}|{version}";

            Mod.Log.Info(
    $"Now following aircraft {flight.Name}"
);

           
        }


        private string GetSelectedFlight()
        {
            return _selectedFlight;
        }

        private TrackerSystem GetTrackerSystem()
        {
            if (World == null)
            {
                Mod.Log.Info(
                    "[FlightTracker] Cannot retrieve TrackerSystem: World is null."
                );

                return null;
            }

            if (!World.IsCreated)
            {
                Mod.Log.Info(
                    "[FlightTracker] Cannot retrieve TrackerSystem: World has not been created."
                );

                return null;
            }

            try
            {
                TrackerSystem trackerSystem =
                    World.GetExistingSystemManaged<TrackerSystem>();

                if (trackerSystem == null)
                {
                    Mod.Log.Info(
                        "[FlightTracker] TrackerSystem does not exist in the current World."
                    );
                }

                return trackerSystem;
            }
            catch (Exception exception)
            {
                Mod.Log.Info(
                    $"[FlightTracker] Failed to retrieve TrackerSystem: {exception}"
                );

                return null;
            }
        }

        private int GetTrackedFlightCount()
        {
            TrackerSystem tracker = GetTrackerSystem();

            if (tracker == null)
            {
                Mod.Log.Info(
                    "[FlightTracker] Cannot get flight count because TrackerSystem is null."
                );

                return 0;
            }

            if (tracker.TrackedFlights == null)
            {
                Mod.Log.Info(
                    "[FlightTracker] Cannot get flight count because TrackedFlights is null."
                );

                return 0;
            }

            return tracker.TrackedFlights.Count;
        }

        private string[] GetTrackedFlights()
        {
            TrackerSystem tracker = GetTrackerSystem();

            if (tracker == null)
            {
                Mod.Log.Info(
                    "[FlightTracker] Cannot build tracked-flight data because TrackerSystem is null."
                );

                return Array.Empty<string>();
            }

            if (tracker.TrackedFlights == null)
            {
                Mod.Log.Info(
                    "[FlightTracker] Cannot build tracked-flight data because TrackedFlights is null."
                );

                return Array.Empty<string>();
            }

            var flights = tracker.TrackedFlights;

            if (flights.Count == 0)
                return Array.Empty<string>();

            string[] result = new string[flights.Count];

            for (int i = 0; i < flights.Count; i++)
            {
                try
                {
                    TrackedFlight flight = flights[i];

                    result[i] = string.Join(
                        "|",
                        flight.EntityIndex.ToString(
                            CultureInfo.InvariantCulture
                        ),
                        flight.EntityVersion.ToString(
                            CultureInfo.InvariantCulture
                        ),
                        EscapeValue(flight.Name),
                        EscapeValue(flight.Status),
                        FormatNumber(flight.X),
                        FormatNumber(flight.Y),
                        FormatNumber(flight.Z),
                        FormatNumber(flight.Altitude),
                        FormatNumber(flight.Speed)
                    );
                }
                catch (Exception exception)
                {
                    Mod.Log.Info(
                        $"[FlightTracker] Failed to serialize tracked flight at index {i}: {exception}"
                    );

                    result[i] = string.Empty;
                }
            }

            return result;
        }

        private static string FormatNumber(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                Mod.Log.Info(
                    $"[FlightTracker] Invalid numeric value received: {value}"
                );

                return "0.00";
            }

            return value.ToString(
                "0.00",
                CultureInfo.InvariantCulture
            );
        }

        private static string EscapeValue(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value
                .Replace("|", "/")
                .Replace("\r", " ")
                .Replace("\n", " ");
        }
    }
}