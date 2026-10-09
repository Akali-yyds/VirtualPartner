using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace VirtualPartner.Runtime
{
    public sealed class PerformanceLatency : MonoBehaviour
    {
        private int request;
        private double submitted;
        private readonly Dictionary<string,double> times=new Dictionary<string,double>();
        public void Begin(int id) { request=id;submitted=Time.realtimeSinceStartupAsDouble;times.Clear();Mark(id,"submitted"); }
        public void Mark(int id,string phase) { if(id==request&&!times.ContainsKey(phase))times[phase]=Time.realtimeSinceStartupAsDouble-submitted; }
        public int RequestId => request;
        public string Describe(){var b=new StringBuilder("request="+request);foreach(var p in times)b.Append("\n"+p.Key+"="+p.Value.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+"s");return b.ToString();}
    }
}
