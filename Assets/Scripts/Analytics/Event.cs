using System;
using UnityEngine.Serialization;

namespace Game.Analytics
{
    [Serializable]
    public class Event
    {
        public string type;
        public string data;
    }
}