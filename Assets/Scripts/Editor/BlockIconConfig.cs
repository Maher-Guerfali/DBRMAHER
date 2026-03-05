using System;
using System.Collections.Generic;
using UnityEngine;

namespace BlockSystem.Editor
{
    /// <summary>
    /// Configuration asset that maps block types to their sprite icons.
    /// Stores sprite assignments for the visual node editor.
    /// 
    /// How to use:
    /// 1. Create → BlockSystem → Block Icon Config
    /// 2. Drag your spritesheet slices into the icon list
    /// 3. Assign icons to block type names
    /// </summary>
    [CreateAssetMenu(menuName = "BlockSystem/Block Icon Config", fileName = "BlockIconConfig")]
    public class BlockIconConfig : ScriptableObject
    {
        [System.Serializable]
        public class IconMapping
        {
            public string blockTypeName;  // e.g., "MoveBlock", "DelayBlock"
            public Sprite icon;           // The sprite to display
        }


        public List<IconMapping> iconMappings = new List<IconMapping>();


        public Sprite defaultIcon;

        // Helper method to get icon for a block type
        public Sprite GetIcon(string blockTypeName)
        {
            var mapping = iconMappings.Find(m => m.blockTypeName == blockTypeName);
            return mapping?.icon ?? defaultIcon;
        }

        // Helper method to check if mapping exists
        public bool HasIcon(string blockTypeName)
        {
            return iconMappings.Exists(m => m.blockTypeName == blockTypeName);
        }
    }
}
