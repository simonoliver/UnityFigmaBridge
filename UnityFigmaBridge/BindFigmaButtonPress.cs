using System;

namespace UnityFigmaBridge
{
    /// <summary>
    /// Allows a method to be bound to a specific named Figma button
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class BindFigmaButtonPress : Attribute
    {

        public string TargetButtonName;
        
        public BindFigmaButtonPress(string buttonName)
        {
            TargetButtonName = buttonName;
        }
    }
}