using System;
using Newtonsoft.Json.Linq;

namespace XRScan.Core
{
    /// <summary>
    /// Response shape of the CV server /v1/florence multi-task endpoint. The concrete
    /// shape of <see cref="results"/> varies by task, so it is kept as a raw JToken.
    /// </summary>
    [Serializable]
    public class FlorenceResult
    {
        public string task;
        public JToken results;
        public string text;
    }
}
