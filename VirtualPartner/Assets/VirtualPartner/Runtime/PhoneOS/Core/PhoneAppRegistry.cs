using System.Collections.Generic;
using UnityEngine;

namespace VirtualPartner.Runtime.PhoneOS
{
    [CreateAssetMenu(fileName = "PhoneAppRegistry", menuName = "Virtual Partner/Phone OS/App Registry")]
    public sealed class PhoneAppRegistry : ScriptableObject
    {
        [SerializeField] private List<PhoneAppDefinition> apps = new List<PhoneAppDefinition>();

        public IReadOnlyList<PhoneAppDefinition> Apps => apps;

        public List<PhoneAppDefinition> GetHomeScreenApps()
        {
            return FilterAndSort(app => app.ShowOnHomeScreen);
        }

        public List<PhoneAppDefinition> GetDockApps()
        {
            return FilterAndSort(app => app.ShowInDock);
        }

        private List<PhoneAppDefinition> FilterAndSort(System.Func<PhoneAppDefinition, bool> predicate)
        {
            var results = new List<PhoneAppDefinition>();
            for (var i = 0; i < apps.Count; i++)
            {
                var app = apps[i];
                if (app == null || string.IsNullOrWhiteSpace(app.AppId))
                    continue;
                if (predicate != null && !predicate(app))
                    continue;

                results.Add(app);
            }

            results.Sort((left, right) =>
            {
                var order = left.Order.CompareTo(right.Order);
                return order != 0 ? order : string.CompareOrdinal(left.AppId, right.AppId);
            });
            return results;
        }
    }
}
