using UnityEngine;

namespace VirtualPartner.Runtime
{
    [DisallowMultipleComponent]
    public sealed class AppShellBootstrap : MonoBehaviour
    {
        [SerializeField] private AppMenuUIManager menuUIManager;
        [SerializeField] private AppLifecycleController lifecycleController;

        public void Configure(VirtualPartnerStage1Bootstrap runtimeBootstrap)
        {
            EnsureComponents();
            lifecycleController.Configure(runtimeBootstrap, menuUIManager);
            menuUIManager.Configure(lifecycleController);
        }

        private void Awake()
        {
            EnsureComponents();
        }

        private void EnsureComponents()
        {
            if (menuUIManager == null)
                menuUIManager = GetComponent<AppMenuUIManager>();
            if (menuUIManager == null)
                menuUIManager = gameObject.AddComponent<AppMenuUIManager>();

            if (lifecycleController == null)
                lifecycleController = GetComponent<AppLifecycleController>();
            if (lifecycleController == null)
                lifecycleController = gameObject.AddComponent<AppLifecycleController>();
        }
    }
}
