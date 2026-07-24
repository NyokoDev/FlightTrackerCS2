using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using FlightTracker.Systems;
using Game;
using Game.Modding;
using Game.SceneFlow;

namespace FlightTracker
{
    public class Mod : IMod
    {
        public static readonly ILog Log =
            LogManager
                .GetLogger($"{nameof(FlightTracker)}.{nameof(Mod)}")
                .SetShowsErrorsInUI(false);

        private Setting m_Setting;

        public static string MODUI = "FlightTracker";


        public void OnLoad(UpdateSystem updateSystem)
        {
            Log.Info(nameof(OnLoad));

            if (GameManager.instance.modManager.TryGetExecutableAsset(
                this,
                out var asset
            ))
            {
                Log.Info($"Current mod asset at {asset.path}");
            }



            updateSystem.UpdateAfter<TrackerSystem>(
               SystemUpdatePhase.GameSimulation
           );

            updateSystem.UpdateAfter<TrackerUISystem>(
                SystemUpdatePhase.UIUpdate
            

            );
        }


   
  

        public void OnDispose()
        {
            Log.Info(nameof(OnDispose));

            if (m_Setting != null)
            {
                m_Setting.UnregisterInOptionsUI();
                m_Setting = null;
            }
        }
    }
}