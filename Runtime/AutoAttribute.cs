using System;
using UnityEngine;

namespace bnj.auto_fields.Runtime
{
    [AttributeUsage(AttributeTargets.Field)]
    public class AutoAttribute : PropertyAttribute
    {
        public string Path { get; set; }

        public AutoAttribute(string path = "")
        {
            Path = path;
        }
    }
}
