using UnityEngine;

namespace VirtualPartner.Runtime
{
    [DisallowMultipleComponent]
    public sealed class AppLifecycleController : MonoBehaviour
    {
        private VirtualPartnerStage1Bootstrap runtimeBootstrap;
        private AppMenuUIManager menuUIManager;
        private bool quitting;

        public bool Quitting => quitting;

        public void Configure(VirtualPartnerStage1Bootstrap bootstrap, AppMenuUIManager menu)
        {
            runtimeBootstrap = bootstrap;
            menuUIManager = menu;
        }

        public void RequestQuit()
        {
            if (quitting)
                return;

            if (menuUIManager != null)
                menuUIManager.ShowQuitConfirmation();
            else
                ConfirmQuit();
        }

        public void CancelQuit()
        {
            if (quitting)
                return;

            if (menuUIManager != null)
                menuUIManager.ShowMainMenu();
        }

        public void ConfirmQuit()
        {
            if (quitting)
                return;

            quitting = true;
            if (menuUIManager != null)
                menuUIManager.SetInteractable(false);

            if (runtimeBootstrap != null)
                runtimeBootstrap.ShutdownRuntimeForQuit();

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
